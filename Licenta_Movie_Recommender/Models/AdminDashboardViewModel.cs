using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;


namespace Licenta_Movie_Recommender.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalMovies { get; set; }
        public int TotalUsers { get; set; }
        public int TotalRatings { get; set; }
        public int TotalWatchlist { get; set; }
        public List<Movie> RecentMovies { get; set; }
        public List<IdentityUser> RecentUsers { get; set; }
    }
}