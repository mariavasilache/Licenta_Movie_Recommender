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

       
        private async Task<List<Movie>> FetchRandomMoviesAsync(List<int> excludedIds)
        {
            int total = await _context.Movies.CountAsync();
            int skip = Random.Shared.Next(0, Math.Max(0, total - 20));

            return await _context.Movies
                .AsNoTracking()
                .Where(m => !excludedIds.Contains(m.Id) && !string.IsNullOrEmpty(m.PosterUrl) && m.PosterUrl.StartsWith("http"))
                .OrderBy(m => Guid.NewGuid())
                .Skip(skip)
                .Take(12)
                .ToListAsync();
        }

        public async Task<IActionResult> Index(bool refreshDiscover = false)
        {
            List<Movie> discoverMovies = new List<Movie>();
            List<int> excludedIds = new List<int>();
            int userId = 0;

            //activitate user
            if (User.Identity.IsAuthenticated)
            {
                userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var activities = await _context.UserActivities.Where(ua => ua.UserId == userId).ToListAsync();
                ViewBag.UserActivities = activities;
                excludedIds = activities.Select(ua => ua.MovieId).ToList();
            }

            //logica cookie
            if (!refreshDiscover && Request.Cookies.TryGetValue("DiscoverMovies", out string cookieValue))
            {
                var movieIds = cookieValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(id => int.TryParse(id, out var i) ? i : 0)
                                          .Where(i => i != 0 && !excludedIds.Contains(i)).ToList();

                if (movieIds.Any())
                {
                    discoverMovies = await _context.Movies.Where(m => movieIds.Contains(m.Id)).ToListAsync();
                }
            }

            
            if (refreshDiscover || discoverMovies.Count < 12)
            {
                discoverMovies = await FetchRandomMoviesAsync(excludedIds); 

               
                var newIds = string.Join(",", discoverMovies.Select(m => m.Id));
                Response.Cookies.Append("DiscoverMovies", newIds, new CookieOptions { MaxAge = TimeSpan.FromMinutes(60), HttpOnly = true });
            }

            ViewBag.DiscoverMovies = discoverMovies;

            //recomandari
            if (userId > 0)
            {
                var pool = await _recService.GetRecommendationsAsync(userId, 20);
                var topRecommendations = pool.Take(6).ToList();
                return View(topRecommendations);
            }

            return View(new List<Movie>());
        }

        // --- METODA PT BUTONUL DE REFRESH AJAX ---
        [HttpGet]
        public async Task<IActionResult> GetDiscoverMovies()
        {
            List<int> excludedIds = new List<int>();

            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var activities = await _context.UserActivities.AsNoTracking().Where(ua => ua.UserId == userId).ToListAsync();
                ViewBag.UserActivities = activities;
                excludedIds = activities.Select(ua => ua.MovieId).ToList();
            }

           
            var discoverMovies = await FetchRandomMoviesAsync(excludedIds);

            return PartialView("_DiscoverMovies", discoverMovies);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}