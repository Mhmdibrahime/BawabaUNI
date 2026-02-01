namespace BawabaUNI.Models.DTOs.Admin.GenralDTOS
{
    public class PartnerResponseDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        public string? Link { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string ImageUrl { get; set; }
    }
}
