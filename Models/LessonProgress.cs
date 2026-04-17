using System.ComponentModel.DataAnnotations.Schema;

namespace grad.Models
{
    public class LessonProgress
    {
    
            public int Id { get; set; }

            public Guid StudentId { get; set; }

            public int CourseSessionId { get; set; }

        [ForeignKey("CourseSessionId")]
        public CourseSession CourseSession { get; set; } = null!;

        public int Views { get; set; }

            public int MaxViews { get; set; }

            public double ProgressPercent { get; set; }

        public int LessonId { get; set; }

        public DateTime LastWatched { get; set; }
        }
    }

