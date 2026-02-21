using Licenta_Movie_Recommender.Models; 
using Microsoft.EntityFrameworkCore;

namespace Licenta_Movie_Recommender.Data 
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<UserMovieActivity> UserActivities { get; set; }
    }
}