using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieRecommenderApp.Data;
using MovieRecommenderApp.Services;

namespace MovieRecommenderApp.Controllers
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
            // luam 12 filme 
            var filme = await _context.Movies.Take(12).ToListAsync();

            // aducem poza pentru fiecare
            foreach (var film in filme)
            {
                film.PosterUrl = await _tmdbService.GetPosterUrlAsync(film.TmdbId);
            }

            return View(filme);
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

    }
}