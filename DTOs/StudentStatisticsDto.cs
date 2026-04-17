namespace grad.DTOs
{

    public class StudentStatisticsDto
    {
        public Guid StudentId { get; set; }
        public int Absence { get; set; }
        public int Tasks { get; set; }
        public int Quiz { get; set; }
    }
}
