namespace grad.DTOs
{
    public class StudentStatisticsDto
    {
        public Guid StudentId { get; set; }
        public decimal Absence { get; set; }
        public decimal Tasks { get; set; }
        public decimal Quiz { get; set; }
    }
}