using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

    namespace BawabaUNI.Models.DTOs
    {
    public class FacultyFormModel
    {
        // البيانات الأساسية
        public string NameArabic { get; set; }
        public string? NameEnglish { get; set; }
        public string Description { get; set; }
        public int ProgramsNumber { get; set; }
        public string DurationOfStudy { get; set; }
        public int StudentsNumber { get; set; }
        public bool RequireAcceptanceTests { get; set; }

        // التخصصات
        public List<string>? SpecializationNames { get; set; }
        public List<int>? SpecializationYearsNumbers { get; set; }
        public List<string>? SpecializationDescriptions { get; set; }

        // السنوات الدراسية
        public List<string> YearNames { get; set; }
        public List<bool> YearHasSpecialization { get; set; }

        // الفصول الدراسية
        public List<string> SemesterNames { get; set; }
        public List<int> SemesterYearIndices { get; set; }

        // مواد الفصول (بدون أقسام)
        public List<string>? SemesterMaterialNames { get; set; }
        public List<int>? SemesterMaterialSemesterIndices { get; set; }
        public List<string>? SemesterMaterialCodes { get; set; }

        // الأقسام (الآن تابعة للسنة)
        public List<string>? SectionNames { get; set; }
        public List<int>? SectionYearIndices { get; set; } // تغيير من SectionSemesterIndices
        public List<string>? SectionCodes { get; set; }

        // مواد الأقسام مع تحديد الفصل
        public List<string>? SectionMaterialNames { get; set; }
        public List<int>? SectionMaterialSectionIndices { get; set; }
        public List<int>? SectionMaterialSemesterIndices { get; set; } // جديد
        public List<string>? SectionMaterialCodes { get; set; }

        // الوسائط
        public List<string>? MediaTypes { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }
        public List<int>? MediaYearIndices { get; set; }

        // فرص العمل
        public List<string>? JobOpportunityNames { get; set; }
        public List<string>? JobOpportunityDescriptions { get; set; }
    }
}
