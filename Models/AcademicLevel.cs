namespace grad.Models
{
    public class AcademicLevel
    {
        public int id { get; set; }
        public string name { get; set; }

        public ICollection<ClassLevel> ClassLevels { get; set; }
    }
}
