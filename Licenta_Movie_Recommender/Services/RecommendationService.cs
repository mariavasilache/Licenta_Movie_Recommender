using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.EntityFrameworkCore;
using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;

namespace Licenta_Movie_Recommender.Services
{
    public class RecommendationService
    {
        private readonly ApplicationDbContext _context;

        // --- CACHE STATIC (model comun pentru toti userii) ---
        private static MLContext _mlContext = new MLContext();
        private static ITransformer? _trainedModel = null;
        private static PredictionEngine<MovieRatingData, MovieRatingPrediction>? _predictionEngine = null;
        private static DateTime _lastTrainingTime = DateTime.MinValue;
        private static readonly object _lock = new object();

        

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<RecommendedMovie>> GetRecommendationsAsync(string userId, int count = 6)
        {
            

          var currentUserActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            var ratedActivities = currentUserActivities.Where(ua => ua.Rating > 0).ToList();

            // --- PONDERI DINAMICE ALGORITM HIBRID ---
            int scoreCount = ratedActivities.Count;
            float W_ML = scoreCount >= 10 ? 0.40f : 0.15f; // scor matrix factorization
            float W_GENRE = scoreCount >= 10 ? 0.25f : 0.50f; // bonus pt genurile preferate
            float W_RECENCY = 0.15f; // genuri vizionate recent 
            float W_POPULARITY = 0.10f;
            float W_DIVERSITY = 0.10f; // penalizare pentru prea multa similaritate

            // cold start: nu avem suficiente date pentru ML
            if (ratedActivities.Count < 3)
                return new List<RecommendedMovie>();

            // reantrenam modelul daca e necesar
            if (_predictionEngine == null || DateTime.Now.Subtract(_lastTrainingTime).TotalMinutes > 20)
                await TrainModelAsync();

            // ── DATE DESPRE USER ─────────────────────────────────────────

            // genurile preferate (din filmele notate cu 4-5)
            var genreScores = BuildGenreMap(currentUserActivities);

            // genurile vizionate in ultimele 14 zile 
            var recentGenres = BuildRecencyMap(currentUserActivities, dayWindow: 14);

            // filmele deja vazute sunt excluse din recomandari
            var excludedIds = currentUserActivities.Select(ua => ua.MovieId).ToHashSet();

            // ── POPULARITATE GLOBALA ─────────────────────────────────────
            // cate activitati are fiecare film in baza de date 
            var popularityMap = await _context.UserActivities
                .Where(ua => ua.Rating > 0)
                .GroupBy(ua => ua.MovieId)
                .Select(g => new { MovieId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MovieId, x => x.Count);

            int maxPopularity = popularityMap.Values.Any() ? popularityMap.Values.Max() : 1;

            // ── FILME CANDIDATE ──────────────────────────────────────────
            var candidateMovies = await _context.Movies
                .AsNoTracking()
                .Where(m => !excludedIds.Contains(m.Id) && !string.IsNullOrEmpty(m.PosterUrl))
                .ToListAsync();

            // ── CALCUL SCOR HIBRID ───────────────────────────────────────
            var predictions = new List<(Movie Movie, float FinalScore, string Explanation)>();

            foreach (var movie in candidateMovies)
            {
                //pastram genurile pt fiecare film
                var movieGenres = string.IsNullOrEmpty(movie.Genres)
                    ? new string[0]
                    : movie.Genres.Split('|');

                // W1: scor ML matrix factorization
                float mlScore = 0f;
                if (_predictionEngine != null)
                {
                    var prediction = _predictionEngine.Predict(
                        new MovieRatingData { UserId = userId, MovieId = movie.Id });
                    mlScore = float.IsNaN(prediction.Score) ? 0f : Math.Clamp(prediction.Score / 5f, 0f, 1f);
                }

                // W2: scor gen
                // proportional cu cat de mult ii plac genurile filmului
                float genreScore = 0f;
                if (movieGenres.Any() && genreScores.Any())
                {
                    float totalGenre = movieGenres
                        .Where(g => genreScores.ContainsKey(g))
                        .Sum(g => genreScores[g]);
                    float maxGenre = genreScores.Values.Any() ? genreScores.Values.Max() * movieGenres.Length : 1f;
                    genreScore = maxGenre > 0 ? Math.Clamp(totalGenre / maxGenre, 0f, 1f) : 0f;
                }

                // W3: scor activitate recenta
                // daca userul a vazut multe filme din genul asta in ultimele 2 saptamani
                float recentScore = 0f;
                if (movieGenres.Any() && recentGenres.Any())
                {
                    float totalRecent = movieGenres
                        .Where(g => recentGenres.ContainsKey(g))
                        .Sum(g => recentGenres[g]);
                    float maxRecent = recentGenres.Values.Any() ? recentGenres.Values.Max() * movieGenres.Length : 1f;
                    recentScore = maxRecent > 0 ? Math.Clamp(totalRecent / maxRecent, 0f, 1f) : 0f;
                }

                // W4: scor popularitate
                // filmele complet nevazute primesc un mic boost 
                float popularityScore = 0f;
                if (popularityMap.TryGetValue(movie.Id, out int popCount))
                    popularityScore = (float)popCount / maxPopularity;
                else
                    popularityScore = 0.1f; // boost mic pentru filme nedescoperite

                // W5: scor diversitate (penalizare similaritate)
                // scazut daca filmul seamana prea mult cu ce a vazut deja
                float diversityPenalty = 0f;
                if (movieGenres.Any() && genreScores.Any())
                {
                    // daca toate genurile filmului sunt deja "saturate" in istoricul userului
                    int saturatedGenres = movieGenres.Count(g => genreScores.ContainsKey(g) && genreScores[g] > 0.7f);
                    diversityPenalty = movieGenres.Length > 0
                        ? (float)saturatedGenres / movieGenres.Length
                        : 0f;
                }

                // scor final ponderat
                float finalScore =
                    (W_ML * mlScore) +
                    (W_GENRE * genreScore) +
                    (W_RECENCY * recentScore) +
                    (W_POPULARITY * popularityScore) -
                    (W_DIVERSITY * diversityPenalty);

                string explanation = BuildExplanation(movieGenres, genreScores, recentGenres, mlScore, popularityScore);
                predictions.Add((movie, finalScore, explanation));
            }

            return predictions
                .OrderByDescending(p => p.FinalScore)
                .ThenBy(p => p.Movie.Id)
                .Take(count)
                .Select(p => new RecommendedMovie { Movie = p.Movie, Explanation = p.Explanation })
                .ToList();
        }



       
        //harta gen->scor bazata pe rating-uri
        private Dictionary<string, float> BuildGenreMap(List<UserMovieActivity> activities)
        {
            var map = new Dictionary<string, float>();

            foreach (var activity in activities.Where(ua => ua.Rating > 0 && ua.Movie != null))
            {
                if (string.IsNullOrEmpty(activity.Movie.Genres)) continue;

                // normalizam rating ul intre -1 si +1
                // 1 stea = -1.0, 3 stele = 0.0, 5 stele = +1.0
                float normalizedRating = (activity.Rating - 3f) / 2f;

                foreach (var genre in activity.Movie.Genres.Split('|'))
                {
                    if (!map.ContainsKey(genre)) map[genre] = 0f;
                    map[genre] += normalizedRating;
                }
            }

            // adaugam si filmele ignorate ca negative reinforcement
            foreach (var activity in activities.Where(ua => ua.Status == 3 && ua.Movie != null))
            {
                if (string.IsNullOrEmpty(activity.Movie?.Genres)) continue;
                foreach (var genre in activity.Movie.Genres.Split('|'))
                {
                    if (!map.ContainsKey(genre)) map[genre] = 0f;
                    map[genre] -= 0.5f; // penalizare moderata pentru ignore
                }
            }
            foreach (var key in map.Keys.ToList())
                map[key] = Math.Max(map[key], -2f);

            return map;
        }

        private string BuildExplanation(string[] movieGenres, Dictionary<string, float> genreScores,
    Dictionary<string, float> recentGenres, float mlScore, float popularityScore)
        {
            // genurile filmului care se potrivesc cu preferintele userului
            var likedGenres = movieGenres
                .Where(g => genreScores.ContainsKey(g) && genreScores[g] > 0.5f)
                .OrderByDescending(g => genreScores[g])
                .Take(2)
                .ToList();

            // genurile vizionate recent care se potrivesc
            var recentMatch = movieGenres
                .Where(g => recentGenres.ContainsKey(g) && recentGenres[g] > 0.3f)
                .FirstOrDefault();

            // construim explicatia
            if (likedGenres.Any() && recentMatch != null && likedGenres.Contains(recentMatch))
                return $"Pentru ca iti place si ai vizionat recent {string.Join(" și ", likedGenres)}";

            if (likedGenres.Any())
                return $"Pentru ca iti place {string.Join(" și ", likedGenres)}";

            if (recentMatch != null)
                return $"Pentru ca ai vizionat recent ({recentMatch})";

            
            if (popularityScore > 0.7f)
                return "Popular";

            return null;
        }

        private Dictionary<string, float> BuildRecencyMap(List<UserMovieActivity> activities, int dayWindow)
        {
            var map = new Dictionary<string, float>();
            var cutoff = DateTime.Now.AddDays(-dayWindow);

            var recentActivities = activities
                .Where(ua => ua.DateAdded >= cutoff && ua.Movie != null)
                .ToList();

            foreach (var activity in recentActivities)
            {
                if (string.IsNullOrEmpty(activity.Movie?.Genres)) continue;

                // filmele vizionate recent conteaza mai mult decat cele mai vechi
                float recencyWeight = 1f - (float)(DateTime.Now - activity.DateAdded).TotalDays / dayWindow;

                foreach (var genre in activity.Movie.Genres.Split('|'))
                {
                    if (!map.ContainsKey(genre)) map[genre] = 0f;
                    map[genre] += recencyWeight;
                }
            }

            return map;
        }


        // ── ANTRENARE MODEL ML
        private async Task TrainModelAsync()
        {
            lock (_lock)
            {
                if (_predictionEngine != null && DateTime.Now.Subtract(_lastTrainingTime).TotalMinutes <= 20)
                    return;

                var allRatings = _context.UserActivities
                    .Where(ua => ua.Rating > 0 || ua.Status == 3)
                    .Select(ua => new MovieRatingData
                    {
                        UserId = ua.UserId,
                        MovieId = ua.MovieId,
                        Label = ua.Status == 3 ? 1.0f : (float)ua.Rating
                    }).ToList();

                if (allRatings.Count < 5) return;

                IDataView trainingData = _mlContext.Data.LoadFromEnumerable(allRatings);

                var options = new MatrixFactorizationTrainer.Options
                {
                    MatrixColumnIndexColumnName = "UserIdEncoded",
                    MatrixRowIndexColumnName = "MovieIdEncoded",
                    LabelColumnName = "Label",
                    NumberOfIterations = 10,
                    ApproximationRank = 64
                };

                var pipeline = _mlContext.Transforms.Conversion
                    .MapValueToKey("UserIdEncoded", "UserId")
                    .Append(_mlContext.Transforms.Conversion.MapValueToKey("MovieIdEncoded", "MovieId"))
                    .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(options));

                _trainedModel = pipeline.Fit(trainingData);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<MovieRatingData, MovieRatingPrediction>(_trainedModel);
                _lastTrainingTime = DateTime.Now;
            }

            await Task.CompletedTask;
        }
    }
}