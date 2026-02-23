using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Services;
using Licenta_Movie_Recommender.Models;

namespace Licenta_Movie_Recommender.Controllers
{
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TmdbService _tmdbService;

        public MoviesController(ApplicationDbContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 24;

            var totalMovies = await _context.Movies.CountAsync();

            var movies = await _context.Movies
                .OrderByDescending(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMovies / pageSize);

            return View(movies);
        }

        //search bar
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return RedirectToAction("Index");
            }

            var rezultate = await _context.Movies
                .Where(m => m.Title.Contains(term))
                .Take(20)
                .ToListAsync();

            ViewBag.SearchTerm = term;
            return View("Index", rezultate);
        }

        //pagina detalii film
        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);

            if (movie == null)
            {
                return NotFound();
            }

            if (movie.TmdbId > 0)
            {
                var extraDetails = await _tmdbService.GetExtraDetailsAsync(movie.TmdbId);
                ViewBag.Overview = extraDetails.Overview;
                ViewBag.TmdbRating = extraDetails.Rating;
                ViewBag.ReleaseDate = extraDetails.ReleaseDate;
            }

            return View(movie);
        }

        //logica rating
        [HttpPost]
        public async Task<IActionResult> SaveRating(int movieId, int rating)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                activity = new UserMovieActivity
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = rating,
                    Status = 2,
                    DateAdded = DateTime.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                activity.Rating = rating;
                activity.Status = 2;
                activity.DateAdded = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = movieId });
        }

        //pagina profil
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var userActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .OrderByDescending(ua => ua.DateAdded)
                .ToListAsync();

            return View(userActivities);
        }

        //lista filme vazute
        [HttpPost]
        public async Task<IActionResult> MarkAsWatched(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                activity = new UserMovieActivity
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = 0,
                    Status = 2,
                    DateAdded = DateTime.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                activity.Status = 2;
                activity.DateAdded = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = movieId });
        }

        //adaugare film in watchlist
        [HttpPost]
        public async Task<IActionResult> AddToWatchlist(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                activity = new UserMovieActivity
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = 0,
                    Status = 1,
                    DateAdded = DateTime.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                if (activity.Rating == 0)
                {
                    activity.Status = 1;
                    activity.DateAdded = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = movieId });
        }
    }
}