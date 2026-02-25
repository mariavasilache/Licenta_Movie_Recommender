using System.ComponentModel.DataAnnotations;

namespace Licenta_Movie_Recommender.Models
{
    public class CustomList
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        
        public ICollection<CustomListMovie> Movies { get; set; } = new List<CustomListMovie>();
    }
}