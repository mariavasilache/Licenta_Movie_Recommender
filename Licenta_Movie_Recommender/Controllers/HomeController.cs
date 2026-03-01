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

        public async Task<IActionResult> Index(bool refreshDiscover = false)
        {
            List<Movie> discoverMovies = new List<Movie>();

            // logica discover
            if (!refreshDiscover && Request.Cookies.TryGetValue("DiscoverMovies", out string cookieValue))
            {
                var movieIds = cookieValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(id => int.TryParse(id, out var i) ? i : 0)
                                          .Where(i => i != 0).ToList();

                if (movieIds.Any())
                {
                    discoverMovies = await _context.Movies
                        .Where(m => movieIds.Contains(m.Id))
                        .ToListAsync();
                }
            }

            // daca nu sunt filme in cookie sau s-a dat refresh,  12 filme noi
            if (refreshDiscover || discoverMovies.Count < 12)
            {
                discoverMovies = await _context.Movies
                    .Where(m => !string.IsNullOrEmpty(m.PosterUrl) && m.PosterUrl.StartsWith("http"))
                    .OrderBy(m => Guid.NewGuid())
                    .Take(12)
                    .ToListAsync();

                var newIds = string.Join(",", discoverMovies.Select(m => m.Id));
                Response.Cookies.Append("DiscoverMovies", newIds, new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(60),
                    HttpOnly = true
                });
            }

           
            ViewBag.DiscoverMovies = discoverMovies;

            //activitate user si Recomandari
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdString != null)
                {
                    var userId = int.Parse(userIdString);

                    ViewBag.UserActivities = await _context.UserActivities
                        .Where(ua => ua.UserId == userId)
                        .ToListAsync();

                    var pool = await _recService.GetRecommendationsAsync(userId, 20);
                    var topRecommendations = pool.Take(6).ToList();

                    return View(topRecommendations);
                }
            }

            return View(new List<Movie>());
        }

        // --- METODA OPTIMIZATA REFRESH RAPID (AJAX) ---
        [HttpGet]
        public async Task<IActionResult> GetDiscoverMovies()
        {
            
            int randomSkip = Random.Shared.Next(0, 500);

            var discoverMovies = await _context.Movies
                .AsNoTracking() 
                .Where(m => !string.IsNullOrEmpty(m.PosterUrl) && m.PosterUrl.StartsWith("http"))
                .OrderBy(m => m.Id)
                .Skip(randomSkip)
                .Take(12)
                .ToListAsync();

           
            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                ViewBag.UserActivities = await _context.UserActivities
                    .AsNoTracking()
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();
            }

            return PartialView("_DiscoverMovies", discoverMovies);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}