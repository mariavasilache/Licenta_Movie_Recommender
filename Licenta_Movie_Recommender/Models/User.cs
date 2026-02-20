using System.ComponentModel.DataAnnotations;

namespace MovieRecommenderApp.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        public string Email { get; set; }
    }
}