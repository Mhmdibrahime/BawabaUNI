using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class VideoAttachment : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [Required]
        [MaxLength(100)]
        public string FileType { get; set; } // "image" or "pdf"

        [Required]
        [MaxLength(500)]
        public string FileUrl { get; set; }

        

        public int VideoId { get; set; }

        [ForeignKey("VideoId")]
        public virtual Video Video { get; set; }
    }
}
