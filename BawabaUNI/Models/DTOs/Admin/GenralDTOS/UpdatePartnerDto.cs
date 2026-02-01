using System.ComponentModel.DataAnnotations;

namespace BawabaUNI.Models.DTOs.Admin.GenralDTOS
{
    public class UpdatePartnerDto
    {
        public IFormFile? ImageFile { get; set; }

        [MaxLength(500, ErrorMessage = "Link cannot exceed 500 characters")]
        [Url(ErrorMessage = "Please enter a valid URL")]
        public string? Link { get; set; }

       
    }
}
