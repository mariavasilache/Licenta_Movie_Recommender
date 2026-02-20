using System.ComponentModel.DataAnnotations;

namespace MovieRecommenderApp.Models
{
    public class Rating
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } // legatura cu userul

        public int MovieId { get; set; }
        public Movie Movie { get; set; } // legatura cu filmul

        [Range(1, 5)]
        public float Score { get; set; } // nota de la 1 la 5
    }
}