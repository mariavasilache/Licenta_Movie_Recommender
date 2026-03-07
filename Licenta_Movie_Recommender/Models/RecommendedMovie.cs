using System.ComponentModel.DataAnnotations.Schema;

namespace Licenta_Movie_Recommender.Models

{
    [NotMapped]
    public class RecommendedMovie
    {
        public Movie Movie { get; set; }
        public string Explanation { get; set; }
    }
}