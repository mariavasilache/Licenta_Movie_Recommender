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
    }
}