using Licenta_Movie_Recommender.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore;

namespace Licenta_Movie_Recommender.Data
{
    
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<UserMovieActivity> UserActivities { get; set; }
        public DbSet<CustomList> CustomLists { get; set; }
        public DbSet<CustomListMovie> CustomListMovies { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            
            base.OnModelCreating(builder);
            builder.Entity<Movie>().HasQueryFilter(m => !m.IsDeleted);

        }
    }
}