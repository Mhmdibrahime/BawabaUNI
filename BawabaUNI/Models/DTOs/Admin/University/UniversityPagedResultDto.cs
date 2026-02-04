namespace BawabaUNI.Models.DTOs.Admin.University
{
    public class UniversitySimpleDto
    {
        public string NameArabic { get; set; } = string.Empty;
        public string NameEnglish { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? StudentsNumber { get; set; }
        public int AvailableHousingCount { get; set; }
        public int FacultiesCount { get; set; }
        public int FoundingYear { get; set; }
        public string Location { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Governate { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
