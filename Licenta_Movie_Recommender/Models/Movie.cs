using System.ComponentModel.DataAnnotations;

namespace MovieRecommenderApp.Models
{
    public class Movie
    {
        [Key]
        public int Id { get; set; } 

        public int TmdbId { get; set; } // id film tmdb pt afisare poze
        public int MovieLensId { get; set; } // id-ul original din fisierul csv

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public string Genres { get; set; } 
    }
}