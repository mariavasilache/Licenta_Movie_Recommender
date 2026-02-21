using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Microsoft.EntityFrameworkCore;


namespace Licenta_Movie_Recommender.Services
{
    public class RecommendationService
    {
        private readonly ApplicationDbContext _context;

        public RecommendationService(ApplicationDbContext context) => _context = context;

        public async Task<List<Movie>> GetRecommendationsAsync(int userId)
        {
            var ratedMovies = await _context.UserActivities
                .Where(ua => ua.UserId == userId && ua.Rating >= 4)
                .Include(ua => ua.Movie)
                .Select(ua => ua.Movie)
                .ToListAsync();

            if (!ratedMovies.Any()) return new List<Movie>();

            var favoriteGenres = ratedMovies
                .Where(m => !string.IsNullOrEmpty(m.Genres))
                .SelectMany(m => m.Genres.Split(','))
                .Select(g => g.Trim())
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(3)
                .ToList();

            var seenMovieIds = await _context.UserActivities
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.MovieId)
                .ToListAsync();

            var allMovies = await _context.Movies
                .Where(m => !seenMovieIds.Contains(m.Id))
                .ToListAsync();

            return allMovies
                .Where(m => !string.IsNullOrEmpty(m.Genres) && favoriteGenres.Any(fg => m.Genres.Contains(fg)))
                .OrderBy(x => Guid.NewGuid())
                .Take(6)
                .ToList();
        }
    }
}