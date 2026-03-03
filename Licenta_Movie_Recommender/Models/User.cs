using System.ComponentModel.DataAnnotations;

namespace Licenta_Movie_Recommender.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        public string Email { get; set; }
    }
}