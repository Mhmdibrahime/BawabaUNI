using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    // 1. Hero Image
    public class HeroImage : BaseEntity
    {
        [Required]
        [MaxLength(500)]
        public string? MobileImagePath { get; set; }
        [Required]
        [MaxLength(500)]
        public string? DesktobImagePath { get; set; }
        [Required]
        [MaxLength(500)]
        public string? TabletImagePath { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

       
    }
}
