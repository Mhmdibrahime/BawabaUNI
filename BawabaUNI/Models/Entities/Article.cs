using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BawabaUNI.Models.Entities
{
    public class Article : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; }

        [Required]
        [MaxLength(100)]
        public string AuthorName { get; set; }

        [MaxLength(500)]
        public string AuthorImage { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Content { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Range(1, 120)]
        public int ReadTime { get; set; }

        [MaxLength(200)]
        public string Tags { get; set; }
    }
}
