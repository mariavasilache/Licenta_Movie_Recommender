using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Adăugat pentru a lua ID-ul adminului curent

namespace Licenta_Movie_Recommender.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // dashboard principal 
        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                TotalMovies = await _context.Movies.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalRatings = await _context.UserActivities.CountAsync(ua => ua.Rating > 0),
                TotalWatchlist = await _context.UserActivities.CountAsync(ua => ua.Status == 1),
                RecentMovies = await _context.Movies.OrderByDescending(m => m.Id).Take(5).ToListAsync()
            };

            return View(model);
        }

        public IActionResult ManageMovies()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetManageMoviesData(int page = 1, string searchString = "")
        {
            int pageSize = 18; 
            var query = _context.Movies.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(m => m.Title.Contains(searchString));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var movies = await query
                .OrderByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new {
                    id = m.Id,
                    title = m.Title,
                    posterUrl = m.PosterUrl,
                    genres = m.Genres,
                    
                    status = _context.UserActivities
                                .Where(ua => ua.MovieId == m.Id && ua.UserId == userId)
                                .Select(ua => ua.Status)
                                .FirstOrDefault(),
                    rating = _context.UserActivities
                                .Where(ua => ua.MovieId == m.Id && ua.UserId == userId)
                                .Select(ua => ua.Rating)
                                .FirstOrDefault()
                })
                .ToListAsync();

            // returnare lista ca JSON, cum asteapta createMovieCardHtml in site.js
            return Json(movies);
        }

        // stergere film AJAX 
        [HttpPost]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            var activities = _context.UserActivities.Where(ua => ua.MovieId == id);
            _context.UserActivities.RemoveRange(activities);

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}