using System.ComponentModel.DataAnnotations;

namespace Licenta_Movie_Recommender.Models
{
    public class UserMovieActivity
    {
        public int Id { get; set; }

        //cine face actiunea
        public int UserId { get; set; }
        public User User { get; set; }

        //la ce film se refera
        public int MovieId { get; set; }
        public Movie Movie { get; set; }

        //ce nota a dat (0-5, 0 = fara nota)
        public int Rating { get; set; }

        // status: 0 = nimic, 1 = plan to watch , 2 = watched 
        public int Status { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}