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


        //----------------------------------- PAGINA DETALII FILM ----------------------------------
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


            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdString != null)
                {
                    var userId = int.Parse(userIdString);

                    
                    ViewBag.UserLists = await _context.CustomLists
                        .Where(cl => cl.UserId == userId)
                        .ToListAsync();

                    //listele in care e filmul
                    ViewBag.ListsContainingMovie = await _context.CustomListMovies
                        .Where(clm => clm.MovieId == id && clm.CustomList.UserId == userId)
                        .Select(clm => clm.CustomListId)
                        .ToListAsync();

                    // activitate user pt film
                    var userActivity = await _context.UserActivities
                        .FirstOrDefaultAsync(ua => ua.MovieId == id && ua.UserId == userId);

                    ViewBag.UserRating = userActivity?.Rating ?? 0;
                    ViewBag.UserStatus = userActivity?.Status ?? 0; // 0 = Nimic, 1 = Watchlist, 2 = Vazut
                }
            }

            return View(movie);
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
            TempData["Success"] = "Filmul a fost adăugat în Watchlist!";
            return RedirectToAction("Details", new { id = movieId });
        }

        //adaugare in "Vazute"
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
            TempData["Success"] = "Ai marcat acest film ca vizionat!";
            return RedirectToAction("Details", new { id = movieId });
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
            TempData["Success"] = $"Ai acordat nota {rating} / 5 acestui film!";
            return RedirectToAction("Details", new { id = movieId });
        }


        // sterge complet activitatea (scoate din vazute/watchlist si sterge nota)
        [HttpPost]
        public async Task<IActionResult> RemoveActivityStatus(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity != null)
            {
                // Stergem complet randul. Asta reseteaza atat Status cat si Nota
                _context.UserActivities.Remove(activity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Statusul și nota au fost eliminate cu succes!";
            }

            return RedirectToAction("Details", new { id = movieId });
        }


        //sterge DOAR nota, dar pastreaza filmul in istoric
        [HttpPost]
        public async Task<IActionResult> RemoveRatingOnly(int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity != null)
            {
                activity.Rating = 0; 

                
                if (activity.Status == 0)
                {
                    _context.UserActivities.Remove(activity);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Nota a fost ștearsă!";
            }
            return RedirectToAction("Details", new { id = movieId });
        }


        // adaugare film in lista custom
        [HttpPost]
        public async Task<IActionResult> AddMovieToCustomList(int customListId, int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

           
            var listExists = await _context.CustomLists.AnyAsync(cl => cl.Id == customListId && cl.UserId == userId);
            if (!listExists) return Unauthorized();

            
            var alreadyInList = await _context.CustomListMovies
                .AnyAsync(clm => clm.CustomListId == customListId && clm.MovieId == movieId);

            if (!alreadyInList)
            {
                _context.CustomListMovies.Add(new CustomListMovie
                {
                    CustomListId = customListId,
                    MovieId = movieId,
                    AddedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Filmul a fost adăugat în lista ta!";
            }
            else
            {
                TempData["Error"] = "Filmul se află deja în această listă.";
            }

            return RedirectToAction("Details", new { id = movieId });
        }

        // Stergere film din lista custom
        [HttpPost]
        public async Task<IActionResult> RemoveMovieFromCustomList(int customListId, int movieId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");
            var userId = int.Parse(userIdString);

            // Cautam legatura exacta
            var clm = await _context.CustomListMovies
                .FirstOrDefaultAsync(x => x.CustomListId == customListId && x.MovieId == movieId && x.CustomList.UserId == userId);

            if (clm != null)
            {
                _context.CustomListMovies.Remove(clm);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Filmul a fost șters din listă!";
            }

            return RedirectToAction("Details", new { id = movieId });
        }


        //----------------------------------- PAGINA PROFIL ----------------------------------
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

        


        // creare lista custom 
        [HttpPost]
        public async Task<IActionResult> CreateCustomList(string name, string description, int? sourceMovieId = null)
        {
           var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            // Validare backend
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Eroare: Numele listei este obligatoriu și nu poate fi gol!";
                
                return sourceMovieId.HasValue 
                    ? RedirectToAction("Details", new { id = sourceMovieId.Value }) 
                    : RedirectToAction("MyProfile");
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
            
            
            return sourceMovieId.HasValue 
                ? RedirectToAction("Details", new { id = sourceMovieId.Value }) 
                : RedirectToAction("MyProfile");
        }

        //pagina individuala lista custom
        [HttpGet]
        public async Task<IActionResult> CustomListDetails(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var customList = await _context.CustomLists
                .Include(cl => cl.Movies)
                .ThenInclude(clm => clm.Movie)
                .FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == userId);

            if (customList == null) return NotFound();

            return View(customList);
        }

        //stergere completa lista custom 
        [HttpPost]
        public async Task<IActionResult> DeleteCustomList(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            
            var listToDelete = await _context.CustomLists
                .Include(cl => cl.Movies)
                .FirstOrDefaultAsync(cl => cl.Id == id && cl.UserId == userId);

            if (listToDelete != null)
            {
                if (listToDelete.Movies.Any())
                {
                    _context.CustomListMovies.RemoveRange(listToDelete.Movies);
                }

                _context.CustomLists.Remove(listToDelete);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Lista '{listToDelete.Name}' a fost ștearsă definitiv!";
            }

            return RedirectToAction("MyProfile");
        }

        // ----------------------------------- WATCHLIST / ISTORIC COMPLET (INFINITE SCROLL) ----------------------------------

        [HttpGet]
        public IActionResult WatchHistory()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            return View(); 
        }

       
        [HttpGet]
        public async Task<IActionResult> GetHistoryData(int tab = 1, int page = 1)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            var userId = int.Parse(userIdString);
            int pageSize = 18; // Incarcam cate 18 filme la fiecare scroll

            var query = _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId);

            // tab 1 = Vazute, tab 2 = Watchlist
            if (tab == 1)
            {
                query = query.Where(a => a.Status == 2 || a.Rating > 0);
            }
            else
            {
                query = query.Where(a => a.Status == 1 && a.Rating == 0);
            }

            var activities = await query
                .OrderByDescending(ua => ua.DateAdded)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    movieId = a.MovieId,
                    title = a.Movie != null ? a.Movie.Title : "Necunoscut",
                    posterUrl = a.Movie != null ? a.Movie.PosterUrl : "",
                    rating = a.Rating,
                    status = a.Status,
                    dateAdded = a.DateAdded.ToString("dd MMM yyyy")
                })
                .ToListAsync();

            return Json(activities); 
        }
    }

}