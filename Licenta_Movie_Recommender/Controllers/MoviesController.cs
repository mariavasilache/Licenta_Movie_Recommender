using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;
using Licenta_Movie_Recommender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Licenta_Movie_Recommender.Controllers
{
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TmdbService _tmdbService;
        private readonly RecommendationService _recommendationService;

        public MoviesController(ApplicationDbContext context, TmdbService tmdbService, RecommendationService recommendationService)
        {
            _context = context;
            _tmdbService = tmdbService;
            _recommendationService = recommendationService;
        }

        #region 1. CATALOG SI CAUTARE (INFINITE SCROLL)

        [HttpGet]
        public IActionResult Index(string sortOrder, string genreFilter, string searchString)
        {

            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentGenre = genreFilter;
            ViewBag.CurrentSearch = searchString;
            ViewBag.Genres = new List<string> { "Action", "Adventure", "Animation", "Comedy", "Crime", "Documentary", "Drama", "Fantasy", "Horror", "Mystery", "Romance", "Sci-Fi", "Thriller" };

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCatalogData(string sortOrder, string genreFilter, string searchString, int page = 1)
        {
            int pageSize = 24;
            var moviesQuery = _context.Movies.AsNoTracking().AsQueryable();

            // 1. Filtrare după căutare (dacă utilizatorul vine de pe bara de search)
            if (!string.IsNullOrEmpty(searchString))
            {
                moviesQuery = moviesQuery.Where(m => m.Title.Contains(searchString));
            }

            // 2. Filtrare după gen
            if (!string.IsNullOrEmpty(genreFilter))
            {
                moviesQuery = moviesQuery.Where(m => m.Genres != null && m.Genres.Contains(genreFilter));
            }

            // 3. Sortare
            moviesQuery = sortOrder switch
            {
                "title_asc" => moviesQuery.OrderBy(m => m.Title),
                "title_desc" => moviesQuery.OrderByDescending(m => m.Title),
                "oldest" => moviesQuery.OrderBy(m => m.Id),
                _ => moviesQuery.OrderByDescending(m => m.Id), // Implicit: cele mai noi
            };

            var movies = await moviesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new { id = m.Id, title = m.Title, posterUrl = m.PosterUrl })
                .ToListAsync();

            return Json(movies);
        }

        [HttpGet]
        public async Task<IActionResult> SearchPreview(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new { movies = new List<object>(), totalCount = 0 });

            var totalCount = await _context.Movies.Where(m => m.Title.Contains(q)).CountAsync();
            var movies = await _context.Movies
                .AsNoTracking()
                .Where(m => m.Title.Contains(q))
                .Take(5)
                .Select(m => new { id = m.Id, title = m.Title, posterUrl = m.PosterUrl })
                .ToListAsync();

            return Json(new { movies, totalCount });
        }

        #endregion

        #region 2. RECOMANDARI 

        [HttpGet]
        public async Task<IActionResult> Recommendations()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            ViewBag.UserActivities = await _context.UserActivities
                .AsNoTracking()
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            var recommendedMovies = await _recommendationService.GetRecommendationsAsync(userId, 12);
            return View(recommendedMovies);
        }

        [HttpPost]
        public async Task<IActionResult> Ignore(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var userId = int.Parse(userIdString);
            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == id);

            if (activity == null)
            {
                activity = new UserMovieActivity { UserId = userId, MovieId = id };
                _context.UserActivities.Add(activity);
            }

            activity.Status = 3;
            await _context.SaveChangesAsync();
            return Ok();
        }

        #endregion

        #region 3. DETALII FILM

        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null) return NotFound();

            if (movie.TmdbId > 0)
            {
                var extraDetails = await _tmdbService.GetExtraDetailsAsync(movie.TmdbId);
                ViewBag.Overview = extraDetails.Overview;
                ViewBag.TmdbRating = extraDetails.Rating;
                ViewBag.ReleaseDate = extraDetails.ReleaseDate;
            }

            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdString != null)
                {
                    var userId = int.Parse(userIdString);
                    ViewBag.UserLists = await _context.CustomLists.Where(cl => cl.UserId == userId).ToListAsync();
                    ViewBag.ListsContainingMovie = await _context.CustomListMovies
                        .Where(clm => clm.MovieId == id && clm.CustomList.UserId == userId)
                        .Select(clm => clm.CustomListId)
                        .ToListAsync();

                    var userActivity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.MovieId == id && ua.UserId == userId);
                    ViewBag.UserRating = userActivity?.Rating ?? 0;
                    ViewBag.UserStatus = userActivity?.Status ?? 0;
                }
            }

            return View(movie);
        }

        #endregion

        #region 4. ACTIVITATI UTILIZATOR (AJAX & FORMS)

        // --- AJAX (butoane carduri) ---
        [HttpPost]
        public async Task<IActionResult> ToggleWatchlist(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var userId = int.Parse(userIdString);
            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == id);
            bool inWatchlist = false;

            if (activity == null)
            {
                _context.UserActivities.Add(new UserMovieActivity { UserId = userId, MovieId = id, Status = 1, DateAdded = DateTime.Now });
                inWatchlist = true;
            }
            else
            {
                if (activity.Status == 1 && activity.Rating == 0) _context.UserActivities.Remove(activity);
                else { activity.Status = 1; inWatchlist = true; }
            }
            await _context.SaveChangesAsync();
            return Json(new { inWatchlist });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleWatched(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var userId = int.Parse(userIdString);
            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == id);
            bool isWatched = false;

            if (activity == null)
            {
                _context.UserActivities.Add(new UserMovieActivity { UserId = userId, MovieId = id, Status = 2, DateAdded = DateTime.Now });
                isWatched = true;
            }
            else
            {
                if (activity.Status == 2 && activity.Rating == 0) _context.UserActivities.Remove(activity);
                else { activity.Status = 2; isWatched = true; }
            }
            await _context.SaveChangesAsync();
            return Json(new { isWatched });
        }

        // --- FORMS CLASICE pag detalii ---
        [HttpPost]
        public async Task<IActionResult> AddToWatchlist(int movieId)
        {
            await ToggleWatchlist(movieId);
            TempData["Success"] = "Statusul Watchlist a fost actualizat!";
            return RedirectToAction("Details", new { id = movieId });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsWatched(int movieId)
        {
            await ToggleWatched(movieId);
            TempData["Success"] = "Statusul Vizionat a fost actualizat!";
            return RedirectToAction("Details", new { id = movieId });
        }

        [HttpPost]
        public async Task<IActionResult> SaveRating(int movieId, int rating)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);
            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                _context.UserActivities.Add(new UserMovieActivity { UserId = userId, MovieId = movieId, Rating = rating, Status = 2, DateAdded = DateTime.Now });

            }
            else
            {
                activity.Rating = rating;
                activity.Status = 2;
                activity.DateAdded = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Ai acordat nota {rating} / 5 acestui film!";
            return Json(new { success = true, rating = rating, status = 2 });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveActivityStatus(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == int.Parse(userIdString) && ua.MovieId == movieId);

            if (activity != null)
            {

                bool hadRating = activity.Rating > 0;
                bool hadStatus = activity.Status > 0;

                _context.UserActivities.Remove(activity);
                await _context.SaveChangesAsync();


                if (hadRating && hadStatus)
                    TempData["Success"] = "Statusul și nota au fost eliminate cu succes!";
                else if (hadRating)
                    TempData["Success"] = "Nota a fost eliminată!";
                else
                    TempData["Success"] = "Filmul a fost scos din listă!";
            }

            return RedirectToAction("Details", new { id = movieId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveRatingOnly(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == int.Parse(userIdString) && ua.MovieId == movieId);
            if (activity != null)
            {
                activity.Rating = 0;
                if (activity.Status == 0) _context.UserActivities.Remove(activity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Nota a fost ștearsă!";
            }
            return Json(new { success = true });
        }

        #endregion

        #region 5. LISTE CUSTOM (COLECTII)

        [HttpPost]
        public async Task<IActionResult> CreateCustomList(string name, string description, int? sourceMovieId = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Eroare: Numele listei este obligatoriu și nu poate fi gol!";
                return sourceMovieId.HasValue ? RedirectToAction("Details", new { id = sourceMovieId.Value }) : RedirectToAction("MyProfile");
            }

            _context.CustomLists.Add(new CustomList { UserId = int.Parse(userIdString), Name = name, Description = description ?? "", CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Lista '{name}' a fost creată cu succes!";
            return sourceMovieId.HasValue ? RedirectToAction("Details", new { id = sourceMovieId.Value }) : RedirectToAction("MyProfile");
        }

        [HttpGet]
        public async Task<IActionResult> CustomListDetails(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var customList = await _context.CustomLists.Include(cl => cl.Movies).ThenInclude(clm => clm.Movie)
                .FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == int.Parse(userIdString));

            if (customList == null) return NotFound();
            return View(customList);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomList(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var listToDelete = await _context.CustomLists.Include(cl => cl.Movies).FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == int.Parse(userIdString));
            if (listToDelete != null)
            {
                if (listToDelete.Movies.Any()) _context.CustomListMovies.RemoveRange(listToDelete.Movies);
                _context.CustomLists.Remove(listToDelete);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Lista '{listToDelete.Name}' a fost ștearsă definitiv!";
            }
            return RedirectToAction("MyProfile");
        }

        [HttpPost]
        public async Task<IActionResult> AddMovieToCustomList(int customListId, int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var listExists = await _context.CustomLists.AnyAsync(cl => cl.Id == customListId && cl.UserId == int.Parse(userIdString));
            if (!listExists) return Unauthorized();

            var alreadyInList = await _context.CustomListMovies.AnyAsync(clm => clm.CustomListId == customListId && clm.MovieId == movieId);
            if (!alreadyInList)
            {
                _context.CustomListMovies.Add(new CustomListMovie { CustomListId = customListId, MovieId = movieId, AddedAt = DateTime.Now });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Filmul a fost adăugat în lista ta!";
            }
            else TempData["Error"] = "Filmul se află deja în această listă.";

            return RedirectToAction("Details", new { id = movieId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMovieFromCustomList(int customListId, int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var clm = await _context.CustomListMovies.FirstOrDefaultAsync(x => x.CustomListId == customListId && x.MovieId == movieId && x.CustomList.UserId == int.Parse(userIdString));
            if (clm != null)
            {
                _context.CustomListMovies.Remove(clm);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Filmul a fost șters din listă!";
            }
            return RedirectToAction("Details", new { id = movieId });
        }

        #endregion

        #region 6. DASHBOARD SI ISTORIC

        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);
            var allActivities = await _context.UserActivities.Include(ua => ua.Movie).Where(ua => ua.UserId == userId).OrderByDescending(ua => ua.DateAdded).ToListAsync();

            var watchedMovies = allActivities.Where(a => a.Status == 2 || a.Rating > 0).ToList();
            var watchlistMovies = allActivities.Where(a => a.Status == 1 && a.Rating == 0).ToList();
            var avgRating = watchedMovies.Any(w => w.Rating > 0) ? Math.Round(watchedMovies.Where(w => w.Rating > 0).Average(w => w.Rating), 1) : 0;

            var topGenre = watchedMovies.Where(w => w.Movie != null && !string.IsNullOrEmpty(w.Movie.Genres))
                .SelectMany(w => w.Movie.Genres.Split('|')).GroupBy(g => g).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

            var userLists = await _context.CustomLists.Include(cl => cl.Movies).ThenInclude(clm => clm.Movie).Where(cl => cl.UserId == userId).OrderByDescending(cl => cl.CreatedAt).ToListAsync();

            return View(new ProfileDashboardViewModel
            {
                TotalWatched = watchedMovies.Count,
                TotalWatchlist = watchlistMovies.Count,
                AverageRating = avgRating,
                FavoriteGenre = topGenre ?? "Nespecificat",
                RecentWatched = watchedMovies.Take(6).ToList(),
                RecentWatchlist = watchlistMovies.Take(6).ToList(),
                CustomLists = userLists
            });
        }

        [HttpGet]
        public IActionResult WatchHistory()
        {
            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == null) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryData(int tab = 1, int page = 1)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var query = _context.UserActivities.Include(ua => ua.Movie).Where(ua => ua.UserId == int.Parse(userIdString));

            if (tab == 1) query = query.Where(a => a.Status == 2 || a.Rating > 0);
            else if (tab == 2) query = query.Where(a => a.Status == 1 && a.Rating == 0);
            else if (tab == 0) query = query.Where(a => a.Status != 3);

            var activities = await query.OrderByDescending(ua => ua.DateAdded).Skip((page - 1) * 18).Take(18)
                .Select(a => new
                {
                    movieId = a.MovieId,
                    title = a.Movie != null ? a.Movie.Title : "Necunoscut",
                    posterUrl = a.Movie != null ? a.Movie.PosterUrl : "",
                    rating = a.Rating,
                    status = a.Status,
                    dateAdded = a.DateAdded.ToString("dd MMM yyyy")
                }).ToListAsync();

            return Json(activities);
        }

        #endregion
    }
}