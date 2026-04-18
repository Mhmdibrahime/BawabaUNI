using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class StudentCourse : BaseEntity
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
        public string EnrollmentStatus { get; set; } = "Active";
        public DateTime? CompletionDate { get; set; }
        public decimal? ProgressPercentage { get; set; } = 0;

        // Add these two properties
        public string? DeviceToken { get; set; }  // Store the allowed device token
        public DateTime? LastAccessAt { get; set; }  // Track last access time

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
