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

            if (!string.IsNullOrEmpty(searchString))
                moviesQuery = moviesQuery.Where(m => m.Title.Contains(searchString));

            if (!string.IsNullOrEmpty(genreFilter))
                moviesQuery = moviesQuery.Where(m => m.Genres != null && m.Genres.Contains(genreFilter));

            moviesQuery = sortOrder switch
            {
                "title_asc" => moviesQuery.OrderBy(m => m.Title),
                "title_desc" => moviesQuery.OrderByDescending(m => m.Title),
                "oldest" => moviesQuery.OrderBy(m => m.Id),
                _ => moviesQuery.OrderByDescending(m => m.Id),
            };

            var moviesData = await moviesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new { m.Id, m.Title, m.PosterUrl, m.Genres })
                .ToListAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var movieIds = moviesData.Select(m => m.Id).ToList();

            var activities = new List<UserMovieActivity>();
            if (!string.IsNullOrEmpty(userId) && movieIds.Any())
            {
                activities = await _context.UserActivities
                    .Where(ua => ua.UserId == userId && movieIds.Contains(ua.MovieId))
                    .ToListAsync();
            }

            var result = moviesData.Select(m => {
                var act = activities.FirstOrDefault(a => a.MovieId == m.Id);
                return new
                {
                    id = m.Id,
                    title = m.Title,
                    posterUrl = m.PosterUrl,
                    genres = m.Genres,
                    status = act != null ? act.Status : 0,
                    rating = act != null ? act.Rating : 0
                };
            });

            return Json(result);
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account");

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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId != null)
                {
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

        private async Task<IActionResult> ToggleActivity(int movieId, int targetStatus)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();
                if (movieId == 0) return BadRequest(new { error = "ID film lipsă." });

                var activity = await _context.UserActivities
                    .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

                bool isActive = false;

                if (activity == null)
                {
                    _context.UserActivities.Add(new UserMovieActivity
                    {
                        UserId = userId,
                        MovieId = movieId,
                        Status = targetStatus,
                        DateAdded = DateTime.Now
                    });
                    isActive = true;
                }
                else
                {
                    if (activity.Status == targetStatus && activity.Rating == 0)
                        _context.UserActivities.Remove(activity);
                    else
                    {
                        activity.Status = targetStatus;
                        isActive = true;
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { isActive, status = targetStatus });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.InnerException?.Message ?? ex.Message }); }
        }

        [HttpPost]
        public Task<IActionResult> ToggleWatchlist([FromForm] int? movieId, [FromForm] int? id)
            => ToggleActivity(movieId ?? id ?? 0, 1);

        [HttpPost]
        public Task<IActionResult> ToggleWatched([FromForm] int? movieId, [FromForm] int? id)
            => ToggleActivity(movieId ?? id ?? 0, 2);

        [HttpPost]
        public async Task<IActionResult> SaveRating([FromForm] int movieId, [FromForm] int rating)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

                if (activity == null)
                {
                    _context.UserActivities.Add(new UserMovieActivity { UserId = userId, MovieId = movieId, Rating = rating, Status = 2, DateAdded = DateTime.Now });
                }
                else
                {
                    activity.Rating = rating;
                    activity.Status = 2; // Daca ii da nota, e clar ca l-a vazut
                    activity.DateAdded = DateTime.Now;
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true, rating, status = 2 });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.InnerException?.Message ?? ex.Message }); }
        }

        [HttpPost] public async Task<IActionResult> AddToWatchlist([FromForm] int movieId) => await ToggleWatchlist(movieId, null);
        [HttpPost] public async Task<IActionResult> MarkAsWatched([FromForm] int movieId) => await ToggleWatched(movieId, null);

        [HttpPost]
        public async Task<IActionResult> RemoveRatingOnly([FromForm] int movieId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);
                if (activity != null)
                {
                    activity.Rating = 0; 
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveActivityStatus([FromForm] int movieId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var activity = await _context.UserActivities.FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);
                if (activity != null)
                {
                    _context.UserActivities.Remove(activity); 
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
        #endregion

        #region 5. LISTE CUSTOM (COLECTII)

        [HttpPost]
        public async Task<IActionResult> CreateCustomList(string name, string description, int? sourceMovieId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Eroare: Numele listei este obligatoriu.";
                return RedirectToAction("Details", new { id = sourceMovieId.Value });
            }

            _context.CustomLists.Add(new CustomList { UserId = userId, Name = name, Description = description ?? "", CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Lista a fost creata!";
            return RedirectToAction("Details", new { id = sourceMovieId.Value });
        }

        [HttpGet]
        public async Task<IActionResult> CustomListDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account");

            var customList = await _context.CustomLists.Include(cl => cl.Movies).ThenInclude(clm => clm.Movie).FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == userId);
            if (customList == null) return NotFound();
            return View(customList);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomList(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account");

            var listToDelete = await _context.CustomLists.Include(cl => cl.Movies).FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == userId);
            if (listToDelete != null)
            {
                if (listToDelete.Movies.Any()) _context.CustomListMovies.RemoveRange(listToDelete.Movies);
                _context.CustomLists.Remove(listToDelete);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("MyProfile");
        }

        [HttpPost]
        public async Task<IActionResult> AddMovieToCustomList(int customListId, int movieId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var alreadyInList = await _context.CustomListMovies.AnyAsync(clm => clm.CustomListId == customListId && clm.MovieId == movieId);
                if (!alreadyInList)
                {
                    _context.CustomListMovies.Add(new CustomListMovie { CustomListId = customListId, MovieId = movieId, AddedAt = DateTime.Now });
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true, message = "Adaugat in lista!" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMovieFromCustomList(int customListId, int movieId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null) return Unauthorized();

                var clm = await _context.CustomListMovies.FirstOrDefaultAsync(x => x.CustomListId == customListId && x.MovieId == movieId && x.CustomList.UserId == userId);
                if (clm != null)
                {
                    _context.CustomListMovies.Remove(clm);
                    await _context.SaveChangesAsync();
                }
                return Json(new { success = true, message = "Sters din lista!" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        #endregion


        #region 6. DASHBOARD SI ISTORIC

        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account");

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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var query = _context.UserActivities.Include(ua => ua.Movie).Where(ua => ua.UserId == userId);

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