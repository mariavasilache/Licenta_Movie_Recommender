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

            if (context.Movies.Any()) return;

            var datasetPath = Path.Combine(Directory.GetCurrentDirectory(), "Dataset");

            // 1.citim links.csv
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

            // 2.citim filmele si luam pozele
            using (var reader = new StreamReader(Path.Combine(datasetPath, "movies.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                int count = 0;
                while (csv.Read() && count < 200) 
                {
                    var originalId = csv.GetField<int>("movieId");
                    var tmdbId = links.ContainsKey(originalId) ? links[originalId] : 0;

                    var movie = new Movie
                    {
                        MovieLensId = originalId,
                        TmdbId = tmdbId,
                        Title = csv.GetField("title"),
                        Genres = csv.GetField("genres")
                    };

                    // cerem url poza
                    if (tmdbId > 0)
                    {
                        movie.PosterUrl = await tmdbService.GetPosterUrlAsync(tmdbId);
                    }

                    context.Movies.Add(movie);
                    count++;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}