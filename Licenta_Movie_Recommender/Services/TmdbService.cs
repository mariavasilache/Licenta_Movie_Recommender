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

                // cautam calea catre poster in raspunsul lor
                if (doc.RootElement.TryGetProperty("poster_path", out var posterPath) && posterPath.ValueKind != JsonValueKind.Null)
                {
                    return $"https://image.tmdb.org/t/p/w500{posterPath.GetString()}";
                }
            }
            return "https://via.placeholder.com/500x750?text=Fara+Poza";
        }
    }
}