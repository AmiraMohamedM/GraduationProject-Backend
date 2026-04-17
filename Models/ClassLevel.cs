namespace grad.Models
{
    public class ClassLevel
    {
        public int id { get; set; }
        public string name { get; set; }

        public int academic_level_id { get; set; }
        public AcademicLevel AcademicLevel { get; set; }
    }
}
