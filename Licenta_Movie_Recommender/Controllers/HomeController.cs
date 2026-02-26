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

            if (!refreshDiscover && Request.Cookies.TryGetValue("DiscoverMovies", out string cookieValue))
            {
                var movieIds = new List<int>();
                if (!string.IsNullOrEmpty(cookieValue))
                {
                    foreach (var idStr in cookieValue.Split(','))
                    {
                        if (int.TryParse(idStr, out int id)) movieIds.Add(id);
                    }

                    if (movieIds.Any())
                    {
                        
                        discoverMovies = await _context.Movies
                            .Where(m => movieIds.Contains(m.Id))
                            .ToListAsync();
                    }
                }
            }

            if (discoverMovies.Count < 12)
            {
                discoverMovies = await _context.Movies
                    .Where(m => m.PosterUrl != null && m.PosterUrl.StartsWith("http"))
                    .OrderBy(m => Guid.NewGuid()) // Zarul!
                    .Take(12)
                    .ToListAsync();

                
                var newIds = string.Join(",", discoverMovies.Select(m => m.Id));
                Response.Cookies.Append("DiscoverMovies", newIds, new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(60),
                    HttpOnly = true
                });
            }

            // discover section
            ViewBag.DiscoverMovies = await _context.Movies
                .Where(m => m.PosterUrl != null && m.PosterUrl != "")
                .OrderBy(m => Guid.NewGuid()) // Random / Surpriza
                .Take(12)
                .ToListAsync();

            // recomandari
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdString != null)
                {
                    var userId = int.Parse(userIdString);

                    //activitate user pt butoane
                    ViewBag.UserActivities = await _context.UserActivities
                        .Where(ua => ua.UserId == userId)
                        .ToListAsync();

                    var pool = await _recService.GetRecommendationsAsync(userId, 20);
                    var randomRecommendations = pool.OrderBy(x => Guid.NewGuid()).Take(6).ToList();

                    return View(randomRecommendations);
                }
            }

            return View(new List<Movie>());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}