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

        // --- ZONA CACHE STATIC (Memorie comuna server) ---
        private static MLContext _mlContext = new MLContext();
        private static ITransformer? _trainedModel = null;
        private static PredictionEngine<MovieRatingData, MovieRatingPrediction>? _predictionEngine = null;
        private static DateTime _lastTrainingTime = DateTime.MinValue;
        private static readonly object _lock = new object();

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(int userId, int count = 6)
        {
            // activitat user curent
            var currentUserActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            if (currentUserActivities.Count(ua => ua.Rating > 0) < 3)
            {
                return new List<Movie>(); // cold start: nu are destule note
            }

            // LOGICA CACHE: reantrenam doar daca nu avem model sau daca a expirat (20 min)
            if (_predictionEngine == null || DateTime.Now.Subtract(_lastTrainingTime).TotalMinutes > 20)
            {
                await TrainModelAsync();
            }

            // calcul genuri preferate  (bonus scor)
            var favoriteGenres = currentUserActivities
                 .Where(ua => ua.Rating >= 4 || ua.Status == 1)
                 .Where(ua => ua.Movie != null && !string.IsNullOrEmpty(ua.Movie.Genres))
                 .SelectMany(ua => ua.Movie.Genres.Split('|'))
                 .GroupBy(g => g)
                 .OrderByDescending(g => g.Count())
                 .Take(3)
                 .Select(g => g.Key)
                 .ToList();

            var excludedMovieIds = currentUserActivities.Select(ua => ua.MovieId).ToHashSet();

            
            var candidateMovies = await _context.Movies
                .Where(m => !excludedMovieIds.Contains(m.Id) && !string.IsNullOrEmpty(m.PosterUrl))
                .OrderByDescending(m => m.Id)
                .Take(400) // limita pt viteza 
                .ToListAsync();

            var predictions = new List<(Movie Movie, float FinalScore)>();

            foreach (var movie in candidateMovies)
            {
                float aiScore = 0;

                // folosire motor predictie din cache (fara reantrenare)
                if (_predictionEngine != null)
                {
                    var prediction = _predictionEngine.Predict(new MovieRatingData { UserId = userId, MovieId = movie.Id });
                    aiScore = float.IsNaN(prediction.Score) ? 0 : prediction.Score;
                }

                float genreBonus = 0f;
                if (!string.IsNullOrEmpty(movie.Genres) && favoriteGenres.Any())
                {
                    var movieGenres = movie.Genres.Split('|');
                    genreBonus = movieGenres.Count(g => favoriteGenres.Contains(g)) * 1.5f;
                }

                predictions.Add((movie, aiScore + genreBonus));
            }

            return predictions
    .OrderByDescending(p => p.FinalScore)
    .ThenBy(p => p.Movie.Id)
    .Take(count)
    .Select(p => p.Movie)
    .ToList();
        }



        private async Task TrainModelAsync()
        {
            // lock pentru prevenire antrenare simultana de 2 useri
            lock (_lock)
            {
                // double check locking
                if (_predictionEngine != null && DateTime.Now.Subtract(_lastTrainingTime).TotalMinutes <= 20)
                    return;

                var allRatings = _context.UserActivities
                    .Where(ua => ua.Rating > 0)
                    .Select(ua => new MovieRatingData
                    {
                        UserId = ua.UserId,
                        MovieId = ua.MovieId,
                        Label = (float)ua.Rating
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

                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("UserIdEncoded", "UserId")
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