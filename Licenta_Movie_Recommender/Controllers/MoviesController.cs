using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Licenta_Movie_Recommender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] string sortOrder, [FromQuery] string genreFilter, [FromQuery] int page = 1)
        {
            int pageSize = 24;

            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentGenre = genreFilter;

            
            ViewBag.Genres = new List<string> { "Action", "Adventure", "Animation", "Comedy", "Crime", "Documentary", "Drama", "Fantasy", "Horror", "Mystery", "Romance", "Sci-Fi", "Thriller" };

            var moviesQuery = _context.Movies.AsQueryable();

           
            if (!string.IsNullOrEmpty(genreFilter))
            {
                moviesQuery = moviesQuery.Where(m => m.Genres.Contains(genreFilter));
            }

            
            if (sortOrder == "title_asc")
            {
                moviesQuery = moviesQuery.OrderBy(m => m.Title);
            }
            else if (sortOrder == "title_desc")
            {
                moviesQuery = moviesQuery.OrderByDescending(m => m.Title);
            }
            else if (sortOrder == "oldest")
            {
                moviesQuery = moviesQuery.OrderBy(m => m.Id);
            }
            else
            {
                // implicit cele mai noi
                moviesQuery = moviesQuery.OrderByDescending(m => m.Id);
            }

            var totalMovies = await moviesQuery.CountAsync();

            var movies = await moviesQuery
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

            // toate activitatile userului pentru statistici
            var allActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .OrderByDescending(ua => ua.DateAdded)
                .ToListAsync();

            // separam listele principale
            var watchedMovies = allActivities.Where(a => a.Status == 2 || a.Rating > 0).ToList();
            var watchlistMovies = allActivities.Where(a => a.Status == 1 && a.Rating == 0).ToList();

            // calcul statistici
            var avgRating = watchedMovies.Any(w => w.Rating > 0)
                ? Math.Round(watchedMovies.Where(w => w.Rating > 0).Average(w => w.Rating), 1)
                : 0;

            var topGenre = watchedMovies
                .Where(w => w.Movie != null && !string.IsNullOrEmpty(w.Movie.Genres))
                .SelectMany(w => w.Movie.Genres.Split('|'))
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            //liste custom
            var userLists = await _context.CustomLists
                .Include(cl => cl.Movies)
                .ThenInclude(clm => clm.Movie)
                .Where(cl => cl.UserId == userId)
                .OrderByDescending(cl => cl.CreatedAt)
                .ToListAsync();

            
            var model = new ProfileDashboardViewModel
            {
                TotalWatched = watchedMovies.Count,
                TotalWatchlist = watchlistMovies.Count,
                AverageRating = avgRating,
                FavoriteGenre = topGenre ?? "Nespecificat",

               
                RecentWatched = watchedMovies.Take(6).ToList(),
                RecentWatchlist = watchlistMovies.Take(6).ToList(),

                CustomLists = userLists
            };

            return View(model);
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

        // adaugare lista custom noua din profil
        [HttpPost]
        public async Task<IActionResult> CreateCustomList(string name, string description)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            //eroare nume gol
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Eroare: Numele listei este obligatoriu și nu poate fi gol!";
                return RedirectToAction("MyProfile");
            }

            var userId = int.Parse(userIdString);

            var newList = new CustomList
            {
                UserId = userId,
                Name = name,
                Description = description ?? "",
                CreatedAt = DateTime.Now
            };

            _context.CustomLists.Add(newList);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Lista '{name}' a fost creată cu succes!";
            return RedirectToAction("MyProfile");
        }
    }

}