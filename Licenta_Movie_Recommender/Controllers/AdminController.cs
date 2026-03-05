using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 

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
                RecentMovies = await _context.Movies.OrderByDescending(m => m.Id).Take(5).ToListAsync(),
                RecentUsers = await _context.Users.Take(5).ToListAsync()
            };

            return View(model);
        }

        public IActionResult ManageMovies()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetManageMoviesData(int page = 1, string searchString = "", string filter = "all")
        {
            int pageSize = 18;
            var query = _context.Movies.IgnoreQueryFilters().AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(m => m.Title.Contains(searchString));
            }

            //filtrare pe tab-rui
            if (filter == "active") query = query.Where(m => !m.IsDeleted);
            else if (filter == "deleted") query = query.Where(m => m.IsDeleted);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            //selectare date
            var movies = await query
                .OrderByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new {
                    id = m.Id,
                    title = m.Title,
                    posterUrl = m.PosterUrl,
                    genres = m.Genres,
                    isDeleted = m.IsDeleted,


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

            
            return Json(movies);
        }

        //pagina editare
        [HttpGet]
        public async Task<IActionResult> EditMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            return View(movie); 
        }

        //salvare modificari
        [HttpPost]
        [ValidateAntiForgeryToken] // Protectie impotriva atacurilor de tip CSRF
        public async Task<IActionResult> EditMovie(int id, Movie model)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingMovie = await _context.Movies.FindAsync(id);
                    if (existingMovie == null) return NotFound();

                    existingMovie.Title = model.Title;
                    existingMovie.PosterUrl = model.PosterUrl;
                    existingMovie.Genres = model.Genres;
                    

                    await _context.SaveChangesAsync();

                    TempData["SuccessToast"] = "Modificările au fost salvate cu succes!";
                    return RedirectToAction(nameof(ManageMovies));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Eroare la salvare: " + ex.Message);
                }
            }
            return View(model);
        }

        // --- STERGERE (SOFT DELETE) ---
        [HttpPost]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            movie.IsDeleted = true; //ascundem film, nu se sterge din bd
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // --- UNDO (RESTORE) ---
        [HttpPost]
        public async Task<IActionResult> RestoreMovie(int id)
        {
            var movie = await _context.Movies.IgnoreQueryFilters()
        .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null) return NotFound();

            movie.IsDeleted = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}