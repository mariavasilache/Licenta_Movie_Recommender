using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.EntityFrameworkCore;
using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using System.Linq;

namespace Licenta_Movie_Recommender.Services
{
    public class RecommendationService
    {
        private readonly ApplicationDbContext _context;

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(int userId, int count = 6)
        {
            // 1. extragem toata activitatea userului 
            var currentUserActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            var ratedActivities = currentUserActivities.Where(ua => ua.Rating > 0).ToList();

            // daca nu are cel putin 5 note oprim algoritmul 
            if (ratedActivities.Count < 5)
            {
                return new List<Movie>();
            }

            // 2. extragem toate notele din toata baza de date pt antrenament (cold start ML.NET)
            var allRatings = await _context.UserActivities
                .Where(ua => ua.Rating > 0)
                .Select(ua => new MovieRatingData
                {
                    UserId = ua.UserId,
                    MovieId = ua.MovieId,
                    Label = ua.Rating
                }).ToListAsync();

            if (allRatings.Count < 10)
            {
                return new List<Movie>();
            }

            // 3. initializare si antrenare ML.NET (collaborative filtering)
            MLContext mlContext = new MLContext();
            IDataView trainingData = mlContext.Data.LoadFromEnumerable(allRatings);

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserIdEncoded",
                MatrixRowIndexColumnName = "MovieIdEncoded",
                LabelColumnName = nameof(MovieRatingData.Label),
                NumberOfIterations = 20,
                ApproximationRank = 100
            };

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "UserIdEncoded", inputColumnName: nameof(MovieRatingData.UserId))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "MovieIdEncoded", inputColumnName: nameof(MovieRatingData.MovieId)))
                .Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

            var model = pipeline.Fit(trainingData);
            var predictionEngine = mlContext.Model.CreatePredictionEngine<MovieRatingData, MovieRatingPrediction>(model);


            // --- scor personalizat (bonus de gen / bonus watchlist)  ---
            
            // top 3 genuri preferate din filmele notate min 4 
            var topGenres = ratedActivities
                .Where(ua => ua.Rating >= 4 && ua.Movie != null && !string.IsNullOrEmpty(ua.Movie.Genres))
                .SelectMany(ua => ua.Movie.Genres.Split('|'))
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            // daca e vazut il excludem
            var seenMovieIds = currentUserActivities
                .Where(ua => ua.Status == 2 || ua.Rating > 0)
                .Select(ua => ua.MovieId)
                .ToHashSet();

            // daca e in watchlist primeste bonus de scor
            var watchlistMovieIds = currentUserActivities
                .Where(ua => ua.Status == 1 && ua.Rating == 0)
                .Select(ua => ua.MovieId)
                .ToHashSet();


            var candidateMovies = await _context.Movies
                .Where(m => !seenMovieIds.Contains(m.Id) && !string.IsNullOrEmpty(m.PosterUrl))
                .OrderByDescending(m => m.Id)
                .Take(1000)
                .ToListAsync();

            // calcul scor final
            var predictions = new List<(Movie Movie, float FinalScore)>();

            foreach (var movie in candidateMovies)
            {
                //factorul 1: scor ai
                var prediction = predictionEngine.Predict(new MovieRatingData
                {
                    UserId = userId,
                    MovieId = movie.Id
                });

                float aiScore = float.IsNaN(prediction.Score) ? 0 : prediction.Score;

                //factorul 2: bonus watchlist
                float watchlistBonus = watchlistMovieIds.Contains(movie.Id) ? 1.5f : 0f;

                //factorul 3: bonus de gen
                float genreBonus = 0f;
                if (!string.IsNullOrEmpty(movie.Genres) && topGenres.Any())
                {
                    var movieGenres = movie.Genres.Split('|');
                    // +0.5p pt fiecare gen preferat regasit in acest film
                    int matches = movieGenres.Count(g => topGenres.Contains(g));
                    genreBonus = matches * 0.5f;
                }

                //
                float finalHybridScore = aiScore + watchlistBonus + genreBonus;

                predictions.Add((movie, finalHybridScore));
            }

            //top recomandari bazate pe scorul hibrid
            return predictions
                .OrderByDescending(p => p.FinalScore)
                .Take(count)
                .Select(p => p.Movie)
                .ToList();
        }
    }
}