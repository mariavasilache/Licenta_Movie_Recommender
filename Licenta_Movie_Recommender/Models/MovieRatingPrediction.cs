namespace Licenta_Movie_Recommender.Models
{
    //date 
    public class MovieRatingData
    {
        public float UserId { get; set; }
        public float MovieId { get; set; }
        public float Label { get; set; } //nota
    }

    //raspunsul primit / predictia
    public class MovieRatingPrediction
    {
        public float Label { get; set; }
        public float Score { get; set; } //nota prezisa de ai
    }
}