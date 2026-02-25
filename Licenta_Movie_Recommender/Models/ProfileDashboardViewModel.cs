using System.Collections.Generic;

namespace Licenta_Movie_Recommender.Models
{
    public class ProfileDashboardViewModel
    {
        // --- statistici generale ---
        public int TotalWatched { get; set; }
        public int TotalWatchlist { get; set; }
        public double AverageRating { get; set; }
        public string FavoriteGenre { get; set; }

        // --- preview uri ultimele 6 filme ---
        public List<UserMovieActivity> RecentWatched { get; set; } = new List<UserMovieActivity>();
        public List<UserMovieActivity> RecentWatchlist { get; set; } = new List<UserMovieActivity>();

        // --- listele custom independente ---
        public List<CustomList> CustomLists { get; set; } = new List<CustomList>();
    }
}