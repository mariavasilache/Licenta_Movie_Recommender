using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Licenta_Movie_Recommender.Models
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

        [NotMapped] // nu se salveaza in baza de date, doar pt afisare
        public string PosterUrl { get; set; }
    }
}