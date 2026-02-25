using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Licenta_Movie_Recommender.Models
{
    public class CustomListMovie
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomListId { get; set; }

        [ForeignKey("CustomListId")]
        public CustomList CustomList { get; set; }

        [Required]
        public int MovieId { get; set; }

        [ForeignKey("MovieId")]
        public Movie Movie { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}