using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class Video : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int DurationInMinutes { get; set; }

        [Required]
        public bool IsPaid { get; set; }

        [MaxLength(2000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string VideoLink { get; set; }

        // ✅ ADD THIS NEW PROPERTY:
        public string? PlayerEmbedUrl { get; set; }

        // Optional but helpful:
        public string? VimeoId { get; set; }

        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
