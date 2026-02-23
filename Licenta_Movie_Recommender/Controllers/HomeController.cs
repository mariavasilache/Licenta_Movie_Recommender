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

        public async Task<IActionResult> Index()
        {
           
            int totalValidMovies = await _context.Movies.CountAsync(m => !string.IsNullOrEmpty(m.PosterUrl));
            int randomSkip = 0;

            if (totalValidMovies > 6)
            {
                randomSkip = new Random().Next(0, totalValidMovies - 6);
            }

           
            var trendingMovies = await _context.Movies
                .Where(m => !string.IsNullOrEmpty(m.PosterUrl))
                .Skip(randomSkip)
                .Take(6)
                .ToListAsync();

            ViewBag.TrendingMovies = trendingMovies;

           
            var recommendations = new List<Movie>();
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdString != null)
                {
                    recommendations = await _recService.GetRecommendationsAsync(int.Parse(userIdString));
                }
            }

            return View(recommendations);
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