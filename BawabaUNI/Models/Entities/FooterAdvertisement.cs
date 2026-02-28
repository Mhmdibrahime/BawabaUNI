using BawabaUNI.Models.Data;
using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.Entities
{
    public class FooterAdvertisement : BaseEntity
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

        [MaxLength(500)]
        [Url]
        public string Link { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        
    }
}
