using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Licenta_Movie_Recommender.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // pag principala dashboard
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

        //management filme
        public async Task<IActionResult> ManageMovies(string searchString, int page = 1)
        {
            int pageSize = 15;
            var query = _context.Movies.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(m => m.Title.Contains(searchString));
            }

            var totalMovies = await query.CountAsync();
            var movies = await query
                .OrderByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            return View(movies);
        }

        // stergere film prin AJAX
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