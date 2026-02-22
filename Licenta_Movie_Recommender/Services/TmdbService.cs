using System.Text.Json;

namespace Licenta_Movie_Recommender.Services
{
    public class TmdbService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public TmdbService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["TmdbApiKey"];
        }

        public async Task<string> GetPosterUrlAsync(int tmdbId)
        {
            if (tmdbId == 0) return "https://via.placeholder.com/500x750?text=Fara+Poza";

            // cerem datele filmului de la TMDB
            var response = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                // cautam calea catre poster in raspuns
                if (doc.RootElement.TryGetProperty("poster_path", out var posterPath) && posterPath.ValueKind != JsonValueKind.Null)
                {
                    return $"https://image.tmdb.org/t/p/w500{posterPath.GetString()}";
                }
            }
            return "https://via.placeholder.com/500x750?text=Fara+Poza";
        }

        public async Task<(string Overview, double Rating, string ReleaseDate)> GetExtraDetailsAsync(int tmdbId)
        {
            if (tmdbId == 0) return ("Descriere indisponibilă.", 0, "");

            // cerem datele in limba romana 
            var response = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=ro-RO");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                string overview = root.TryGetProperty("overview", out var ov) ? ov.GetString() : "Descriere indisponibilă.";
                double rating = root.TryGetProperty("vote_average", out var va) ? va.GetDouble() : 0;
                string releaseDate = root.TryGetProperty("release_date", out var rd) ? rd.GetString() : "";

                // daca nu exista traducere in romana
                if (string.IsNullOrEmpty(overview)) overview = "Descriere indisponibilă în limba română.";

                return (overview, rating, releaseDate);
            }
            return ("Descriere indisponibilă.", 0, "");
        }
    }
}