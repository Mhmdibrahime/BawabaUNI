using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class CourseActivationCode : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsUsed { get; set; } = false;

        public DateTime? UsedAt { get; set; }

        public int? UsedByStudentId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("UsedByStudentId")]
        public virtual Student? UsedByStudent { get; set; }
    }
}
