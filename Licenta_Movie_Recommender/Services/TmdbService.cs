using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Licenta_Movie_Recommender.Services
{
    public class TmdbService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IMemoryCache _cache;

        public TmdbService(HttpClient httpClient, IConfiguration config, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _apiKey = config["TmdbApiKey"];
            _cache = cache;
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

            string cacheKey = $"tmdb_{tmdbId}";
            if (_cache.TryGetValue(cacheKey, out (string, double, string) cached))
                return cached;

            // cerem datele in limba romana 
            var response = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=ro-RO");
           
            if (!response.IsSuccessStatusCode)
                return ("Descriere indisponibilă.", 0, "");

           
                var content = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                string overview = root.TryGetProperty("overview", out var ov) ? ov.GetString() ?? "" : "";
                double rating = root.TryGetProperty("vote_average", out var va) ? va.GetDouble() : 0;
                string releaseDate = root.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "";

                // daca nu exista traducere in romana cerem in eng
                if (string.IsNullOrEmpty(overview))
                {
                    var responseEn = await _httpClient.GetAsync($"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_apiKey}&language=en-US");
                    if (responseEn.IsSuccessStatusCode)
                    {
                        var contentEn = await responseEn.Content.ReadAsStringAsync();
                        using var docEn = System.Text.Json.JsonDocument.Parse(contentEn);

                       
                        overview = docEn.RootElement.TryGetProperty("overview", out var ovEn) ? ovEn.GetString() : "";
                    }
                }
            var result = (overview, rating, releaseDate);

            //salvare rez 24h
            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));

            return result;

        }
    }
}