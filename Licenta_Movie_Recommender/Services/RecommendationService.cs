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

        public RecommendationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(int userId, int count = 6)
        {
            var userRatingsCount = await _context.UserActivities
                .CountAsync(ua => ua.UserId == userId && ua.Rating > 0);

            // daca nu are cel putin 5 note oprim algoritmul 
            if (userRatingsCount < 5 )
            {
                return new List<Movie>();
            }

            // extragem toate notele pt antrenament
            var allRatings = await _context.UserActivities
                .Where(ua => ua.Rating > 0)
                .Select(ua => new MovieRatingData
                {
                    UserId = ua.UserId,
                    MovieId = ua.MovieId,
                    Label = ua.Rating
                }).ToListAsync();

            // minim 10 note pt a functiona ok algoritmul
            if (allRatings.Count < 10)
            {
                return new List<Movie>();
            }

            // initializare ML.NET
            MLContext mlContext = new MLContext();

            // incarcare date
            IDataView trainingData = mlContext.Data.LoadFromEnumerable(allRatings);

            // configurare algoritm matrix factorization
            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserIdEncoded",
                MatrixRowIndexColumnName = "MovieIdEncoded",
                LabelColumnName = nameof(MovieRatingData.Label),
                NumberOfIterations = 20,
                ApproximationRank = 100
            };

            // creare pipeline pt transformare date
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "UserIdEncoded", inputColumnName: nameof(MovieRatingData.UserId))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "MovieIdEncoded", inputColumnName: nameof(MovieRatingData.MovieId)))
                .Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

            // antrenare model
            var model = pipeline.Fit(trainingData);

            // creare motor de predictie
            var predictionEngine = mlContext.Model.CreatePredictionEngine<MovieRatingData, MovieRatingPrediction>(model);

            // lista filme vazute de user
            var seenMovieIds = await _context.UserActivities
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.MovieId)
                .ToListAsync();

            
            var unseenMovies = await _context.Movies
                .Where(m => !seenMovieIds.Contains(m.Id) && !string.IsNullOrEmpty(m.PosterUrl))
                .OrderByDescending(m => m.Id) // cele mai noi adaugate
                .Take(200)
                .ToListAsync();

            // calcul predictii
            var predictions = new List<(Movie Movie, float Score)>();

            foreach (var movie in unseenMovies)
            {
                var prediction = predictionEngine.Predict(new MovieRatingData
                {
                    UserId = userId,
                    MovieId = movie.Id
                });

                // ignoram valorile invalide
                if (!float.IsNaN(prediction.Score))
                {
                    predictions.Add((movie, prediction.Score));
                }
            }

            // returnam top recomandari
            var recommendedMovies = predictions
                .OrderByDescending(p => p.Score)
                .Take(count)
                .Select(p => p.Movie)
                .ToList();

            return recommendedMovies;
        }
    }
}