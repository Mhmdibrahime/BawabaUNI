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
        public decimal Expenses { get; set; }
        [Range(50, 100, ErrorMessage = "التنسيق يجب أن يكون بين 50 و 100")]
        public decimal Coordination { get; set; }

        [MaxLength(500)]
        public string? GroupLink { get; set; }
        [MaxLength(500,ErrorMessage = "العنوان لا يجب ان يتخطى 500 حرف ")]
        public string? Address { get; set; }

        public IFormFile? Image { get; set; }
        [MaxLength(500,ErrorMessage ="الوصف لا يجب ان يتخطى 500 حرف")]
        public string? DescriptionOfStudyPlan { get; set; }

        // التخصصات
        public List<string>? SpecializationNames { get; set; }
        public List<int>? SpecializationYearsNumbers { get; set; }
        public List<string>? SpecializationDescriptions { get; set; }
        public List<int>? DeletedSpecializationIds { get; set; } // IDs التخصصات المطلوب حذفها


        // السنوات الدراسية
        public List<int>? YearNumbers { get; set; } = new List<int>();      // الأرقام: 1, 2, 3, 4
        public List<string>? YearNames { get; set; }
        public List<bool>? YearHasSpecialization { get; set; }
        // 🆕 لحذف السنوات الدراسية المحددة
        public List<int>? DeletedYearIds { get; set; }
        // الخاصية دي مهمة عشان تحديث السنوات الموجودة
        public List<int>? ExistingYearIds { get; set; } = new List<int>();
        // الفصول الدراسية
        public List<string>? SemesterNames { get; set; }
        public List<int>? SemesterYearIndices { get; set; }

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
        public List<string>? MediaVisitLinks { get; set; }
        public List<IFormFile>? MediaFiles { get; set; }
        public List<int>? MediaYearIndices { get; set; }
        // 🆕 فقط للتعامل مع حذف الوسائط
        public List<int>? DeletedMediaIds { get; set; }
       
        // فرص العمل
        public List<string>? JobOpportunityNames { get; set; }
        public List<string>? JobOpportunityDescriptions { get; set; }
        public List<int>? DeletedJobOpportunityIds { get; set; } // IDs فرص العمل المطلوب حذفها

    }
}
