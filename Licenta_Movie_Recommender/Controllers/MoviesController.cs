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

        public async Task<IActionResult> Index()
        {
            var movies = await _context.Movies.ToListAsync();
            return View(movies);
        }

        //search bar
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return RedirectToAction("Index");
            }

            // cauta filmele care contin textul introdus in titlu
            var rezultate = await _context.Movies
                .Where(m => m.Title.Contains(term))
                .Take(20) // Luam primele 20 de rezultate
                .ToListAsync();

            // le punem posterele de pe TMDB
            foreach (var film in rezultate)
            {
                film.PosterUrl = await _tmdbService.GetPosterUrlAsync(film.TmdbId);
            }

            ViewBag.SearchTerm = term;
            return View("Index", rezultate); // refolosim pagina de index pentru a afisa rezultatele
        }

        //pagina detalii film
        public async Task<IActionResult> Details(int id)
        {
            // cauta filmul in baza de date
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);

            if (movie == null)
            {
                return NotFound(); // daca filmul nu exista, eroare 404
            }

            //aduce posterul de pe net
            movie.PosterUrl = await _tmdbService.GetPosterUrlAsync(movie.TmdbId);

            return View(movie);
        }

        //logica rating

        [HttpPost]
        public async Task<IActionResult> SaveRating(int movieId, int rating)
        {
            // luam id utilizator logat din cookie
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            // verificam daca exista deja o activitate pentru acest film
            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                // daca nu a mai dat rating, cream o inregistrare noua
                activity = new UserMovieActivity
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = rating,
                    Status = 2, // 2= vazut (utilizator da recenzie = film vazut)
                    DateAdded = DateTime.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                // daca a mai dat rating, update nota si status
                activity.Rating = rating;
                activity.Status = 2;
                activity.DateAdded = DateTime.Now;
            }

            // salvam modificarile in baza de date
            await _context.SaveChangesAsync();

            // refresh pagina filmului
            return RedirectToAction("Details", new { id = movieId });
        }

        //pagina profil
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            // 1. aflam cine este logat
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            // 2. luam din baza de date toate activitatile acestui user
            // folosim .Include() pt a aduce si toate detaliile filmului
            var userActivities = await _context.UserActivities
                .Include(ua => ua.Movie)
                .Where(ua => ua.UserId == userId)
                .OrderByDescending(ua => ua.DateAdded)
                .ToListAsync();

            // 3. aducem pozele pentru filmele din liste
            foreach (var activity in userActivities)
            {
                activity.Movie.PosterUrl = await _tmdbService.GetPosterUrlAsync(activity.Movie.TmdbId);
            }

            // 4. trimitem datele catre pagina HTML
            return View(userActivities);
        }

        //lista filme vazute
        [HttpPost]
        public async Task<IActionResult> MarkAsWatched(int movieId)
        {
            // luam id utilizator
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return RedirectToAction("Login", "Account");

            var userId = int.Parse(userIdString);

            var activity = await _context.UserActivities
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.MovieId == movieId);

            if (activity == null)
            {
                // daca nu exista, il adaugam ca vazut (status 2) cu nota 0
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
                // daca era in watchlist, il mutam la vazute
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
                // 1. daca nu exista deloc in istoric -> il adaugam in Watchlist
                activity = new UserMovieActivity
                {
                    UserId = userId,
                    MovieId = movieId,
                    Rating = 0,
                    Status = 1, // Watchlist
                    DateAdded = DateTime.Now
                };
                _context.UserActivities.Add(activity);
            }
            else
            {
                // 2. daca exista deja, verificam daca are nota 
                if (activity.Rating == 0)
                {
                    // il mutam inapoi in watchlist (Status 1)
                
                    activity.Status = 1;
                    activity.DateAdded = DateTime.Now;
                }
                
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = movieId });
        }

    }
}