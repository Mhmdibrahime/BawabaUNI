using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    public class Advertisement : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; }

        [MaxLength(500)]
        [Url]
        public string Link { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Range(0, int.MaxValue)]
        public int? ClickCount { get; set; } = 0;
    }
}
