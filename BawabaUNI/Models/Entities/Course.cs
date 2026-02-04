using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class Course : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string NameArabic { get; set; }

        [Required]
        [MaxLength(200)]
        public string NameEnglish { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Description { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Range(0, 100)]
        public decimal? Discount { get; set; }

        [Range(1, int.MaxValue)]
        public int LessonsNumber { get; set; }

        [Range(0, int.MaxValue)]
        public int? HoursNumber { get; set; }

        [Required]
        [MaxLength(500)]
        public string PosterImage { get; set; }

        [Required]
        [MaxLength(200)]
        public string Classification { get; set; }

        [Required]
        [MaxLength(100)]
        public string InstructorName { get; set; }

        [Required]
        [MaxLength(500)]
        public string InstructorImage { get; set; }

        [MaxLength(2000)]
        public string InstructorDescription { get; set; }

        public virtual ICollection<LessonLearned> LessonsLearned { get; set; }
        public virtual ICollection<Video> Videos { get; set; }
        public virtual ICollection<StudentCourse> StudentCourses { get; set; }
        public virtual ICollection<CourseFeedback> CourseFeedbacks { get; set; }
    }
}
