namespace BawabaUNI.Models.DTOs.Admin.AdvertisementsDTOs
{
    public class AdvertisementUpdateRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public IFormFile? Image { get; set; }
        public string Link { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
