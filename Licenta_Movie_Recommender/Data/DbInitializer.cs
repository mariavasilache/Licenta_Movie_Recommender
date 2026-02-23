using CsvHelper;
using Licenta_Movie_Recommender.Models;
using Licenta_Movie_Recommender.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Licenta_Movie_Recommender.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, TmdbService tmdbService)
        {
            context.Database.Migrate();

            var filmeStricate = context.Movies
                .Where(m => m.TmdbId == 0 || string.IsNullOrEmpty(m.PosterUrl))
                .ToList();

            if (filmeStricate.Any())
            {
                context.Movies.RemoveRange(filmeStricate);
                await context.SaveChangesAsync();
            }

            // 1. aflam ce filme avem deja in baza pt a nu le dubla
            var filmeExistente = context.Movies.Select(m => m.MovieLensId).ToHashSet();

            var datasetPath = Path.Combine(Directory.GetCurrentDirectory(), "Dataset");

            // 2. citim links.csv
            var links = new Dictionary<int, int>();
            using (var reader = new StreamReader(Path.Combine(datasetPath, "links.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var movieId = csv.GetField<int>("movieId");
                    var tmdbString = csv.GetField("tmdbId");
                    if (!string.IsNullOrEmpty(tmdbString))
                    {
                        links[movieId] = int.Parse(tmdbString);
                    }
                }
            }

            // 3. citim filmele si adaugam ce lipseste
            int countFilmeNoi = 0;

            using (var reader = new StreamReader(Path.Combine(datasetPath, "movies.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    var originalId = csv.GetField<int>("movieId");

                    if (filmeExistente.Contains(originalId))
                    {
                        continue; 
                    }

                    var tmdbId = links.ContainsKey(originalId) ? links[originalId] : 0;

                    if (tmdbId == 0) continue;
                    var posterUrl = await tmdbService.GetPosterUrlAsync(tmdbId);
                    if (string.IsNullOrEmpty(posterUrl)) continue;

                    var movie = new Movie
                    {
                        MovieLensId = originalId,
                        TmdbId = tmdbId,
                        Title = csv.GetField("title"),
                        Genres = csv.GetField("genres"),
                        PosterUrl = posterUrl
                    };

                    context.Movies.Add(movie);
                    countFilmeNoi++;

                  

                    context.Movies.Add(movie);
                    countFilmeNoi++;

                    
                    if (countFilmeNoi % 100 == 0)
                    {
                        await context.SaveChangesAsync();
                    }
                }
            }

            
            await context.SaveChangesAsync();
        }
    }
}