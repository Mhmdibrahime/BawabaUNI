using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    public class Book : BaseEntity
    {
        [Required]
        [MaxLength(500)]
        public string Title { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(200)]
        public string FacultyName { get; set; }

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        [MaxLength(500)]
        public string CoverImageUrl { get; set; }

        [Required]
        [MaxLength(1000)]
        public string BookLink { get; set; } // Backblaze URL

        public int UploadsNum { get; set; } = 0;
        public int ReadingNum { get; set; } = 0;

        public DateTime? UploadedAt { get; set; }
    }
}
