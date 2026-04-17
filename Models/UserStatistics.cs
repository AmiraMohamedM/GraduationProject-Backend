using System.ComponentModel.DataAnnotations;

namespace grad.Models
{
    public class UserStatistics
    {
        [Key]
        public int Id { get; set; }

        public Guid StudentId { get; set; }

        public int Absence { get; set; } 
        public int Tasks { get; set; }   
        public int Quiz { get; set; } 
    }
}