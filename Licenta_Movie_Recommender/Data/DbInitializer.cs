using System.Globalization;
using CsvHelper;
using MovieRecommenderApp.Models;

namespace MovieRecommenderApp.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // daca baza e deja plina cu filme, oprim scriptul sa nu avem dubluri
            if (context.Movies.Any())
            {
                return;
            }

            var datasetPath = Path.Combine(Directory.GetCurrentDirectory(), "Dataset");

            // 1. citim id-urile pt poze din links.csv
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

                    int tmdbId = 0;
                    if (!string.IsNullOrEmpty(tmdbString))
                    {
                        tmdbId = int.Parse(tmdbString);
                    }

                    links[movieId] = tmdbId;
                }
            }

            // 2. citim filmele din movies.csv
            var moviesList = new List<Movie>();
            using (var reader = new StreamReader(Path.Combine(datasetPath, "movies.csv")))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var originalId = csv.GetField<int>("movieId");

                    moviesList.Add(new Movie
                    {
                        MovieLensId = originalId, // pastram id-ul lor pentru recenzii
                        TmdbId = links.ContainsKey(originalId) ? links[originalId] : 0,
                        Title = csv.GetField("title"),
                        Genres = csv.GetField("genres")
                    });
                }
            }

            // 3. salvam filmele in baza de date sql
            context.Movies.AddRange(moviesList);
            context.SaveChanges();
        }
    }
}