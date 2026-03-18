using System.Diagnostics;
using Licenta_Movie_Recommender.Models;
using Microsoft.AspNetCore.Mvc;
using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Licenta_Movie_Recommender.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecommendationService _recService;

        public HomeController(ApplicationDbContext context, RecommendationService recService)
        {
            _context = context;
            _recService = recService;
        }

        // Metoda fetch movies for discover section
        private async Task<List<Movie>> FetchAndStoreDiscoverMoviesAsync(List<int> excludedIds)
        {
            int maxId = await _context.Movies.MaxAsync(m => (int?)m.Id) ?? 5000;
            var randomIds = new List<int>();

            int modernStart = Math.Max(1, maxId - 2500);
            for (int i = 0; i < 20; i++)
            {
                randomIds.Add(Random.Shared.Next(modernStart, maxId + 1));
            }

            for (int i = 0; i < 15; i++)
            {
                randomIds.Add(Random.Shared.Next(1, maxId + 1));
            }

            var discoverMovies = await _context.Movies
                 .AsNoTracking()
                 .Where(m => randomIds.Contains(m.Id) &&
                             !excludedIds.Contains(m.Id) &&
                             !string.IsNullOrEmpty(m.PosterUrl) &&
                             m.PosterUrl.StartsWith("http"))
                 .OrderBy(m => Guid.NewGuid())
                 .Take(12)
                 .ToListAsync();

            // Daca nu avem destule filme in baza
            if (discoverMovies.Count < 12)
            {
                var currentIds = discoverMovies.Select(dm => dm.Id).ToList();
                var extra = await _context.Movies
                    .AsNoTracking()
                    .Where(m => !excludedIds.Contains(m.Id) &&
                                !currentIds.Contains(m.Id) &&
                                !string.IsNullOrEmpty(m.PosterUrl))
                    .OrderBy(m => Guid.NewGuid())
                    .Take(12 - discoverMovies.Count)
                    .ToListAsync();
                discoverMovies.AddRange(extra);
            }

            // Actualizare cookies discover
            var newIds = string.Join(",", discoverMovies.Select(m => m.Id));
            Response.Cookies.Append("DiscoverMovies", newIds, new CookieOptions
            {
                MaxAge = TimeSpan.FromMinutes(60),
                HttpOnly = true
            });

            return discoverMovies;
        }

        public async Task<IActionResult> Index(bool refreshDiscover = false)
        {
            List<Movie> discoverMovies = new List<Movie>();
            List<int> excludedIds = new List<int>();
            string userId = null; 

            if (User.Identity.IsAuthenticated)
            {
                userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var activities = await _context.UserActivities
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();

                ViewBag.UserActivities = activities;
                excludedIds = activities.Select(ua => ua.MovieId).ToList();
            }

            // Incarcare filme din cookie daca exista
            if (!refreshDiscover && Request.Cookies.TryGetValue("DiscoverMovies", out string cookieValue))
            {
                var movieIds = cookieValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(id => int.TryParse(id, out var i) ? i : 0)
                                          .Where(i => i != 0 && !excludedIds.Contains(i)).ToList();

                if (movieIds.Count != 0)
                {
                    discoverMovies = await _context.Movies.Where(m => movieIds.Contains(m.Id)).ToListAsync();
                }
            }

            // Daca e refresh sau lista e goala
            if (refreshDiscover || discoverMovies.Count < 12)
            {
                discoverMovies = await FetchAndStoreDiscoverMoviesAsync(excludedIds);
            }

            ViewBag.DiscoverMovies = discoverMovies;

            var stripPosters = await _context.Movies
     .AsNoTracking()
     .Where(m => !string.IsNullOrEmpty(m.PosterUrl) &&
                 m.PosterUrl.StartsWith("https://image.tmdb.org"))
     .OrderBy(m => Guid.NewGuid())
     .Take(40)
     .Select(m => m.PosterUrl)
     .ToListAsync();
            ViewBag.StripPosters = stripPosters;



            // Logica recomandari pentru user logat
            if (!string.IsNullOrEmpty(userId))
            {
                var pool = await _recService.GetRecommendationsAsync(userId, 20);
                return View(pool.Take(6).ToList());
            }

            return View(new List<RecommendedMovie>());
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscoverMovies()
        {
            List<int> excludedIds = [];

            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var activities = await _context.UserActivities
                    .AsNoTracking()
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();

                excludedIds = activities.Select(ua => ua.MovieId).ToList();
            }

            var discoverMovies = await FetchAndStoreDiscoverMoviesAsync(excludedIds);
            return PartialView("_DiscoverMovies", discoverMovies);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}