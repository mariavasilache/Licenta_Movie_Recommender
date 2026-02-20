using Microsoft.EntityFrameworkCore;
using MovieRecommenderApp.Models;

namespace MovieRecommenderApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // tabelele care ajung in baza de date
        public DbSet<Movie> Movies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Rating> Ratings { get; set; }
    }
}