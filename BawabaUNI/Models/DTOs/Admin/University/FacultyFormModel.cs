using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

    namespace BawabaUNI.Models.DTOs
    {
    public class FacultyFormModel
    {
        // ============================================
        // 📌 الخطوة 1: نظرة عامة
        // ============================================
        [Required] public string NameArabic { get; set; } = string.Empty;
        public string NameEnglish { get; set; } = string.Empty;
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public int? ProgramsNumber { get; set; }
        [Required] public string DurationOfStudy { get; set; } = string.Empty;
        [Required] public int? StudentsNumber { get; set; }
        public bool RequireAcceptanceTests { get; set; }

        // ============================================
        // 📌 الخطوة 2: التخصصات
        // ============================================
        public List<string> SpecializationNames { get; set; } = new();
        public List<int> SpecializationYearsNumbers { get; set; } = new();
        public List<string> SpecializationDescriptions { get; set; } = new();

        // ============================================
        // 📌 الخطوة 3: خطة الدراسة (نظام جديد مبسط)
        // ============================================

        // 🔸 السنوات الدراسية (مثل: العام الدراسي 1)
        public List<string> YearNames { get; set; } = new(); // "العام الدراسي 1"
        public List<bool> YearHasSpecialization { get; set; } = new(); // true = عام تخصص

        // 🔸 الفصول الدراسية لكل سنة
        public List<string> SemesterNames { get; set; } = new(); // "الفصل الدراسي 1"
        public List<int> SemesterYearIndices { get; set; } = new(); // أي سنة ينتمي لها الفصل

        public List<string> SemesterMaterialNames { get; set; } = new();
        public List<int> SemesterMaterialSemesterIndices { get; set; } = new();
        public List<string> SemesterMaterialCodes { get; set; } = new(); // ⬅️ جديد

        // 🔸 أقسام الفصول - مع الكود
        public List<string> SectionNames { get; set; } = new();
        public List<int> SectionSemesterIndices { get; set; } = new();
        public List<string> SectionCodes { get; set; } = new(); // ⬅️ جديد

        // 🔸 مواد الأقسام - مع الكود
        public List<string> SectionMaterialNames { get; set; } = new();
        public List<int> SectionMaterialSectionIndices { get; set; } = new();
        public List<string> SectionMaterialCodes { get; set; } = new(); // ⬅️ جديد

        // 🔸 وسائط لكل سنة
        public List<string> MediaTypes { get; set; } = new();
        public List<IFormFile> MediaFiles { get; set; } = new();
        public List<int> MediaYearIndices { get; set; } = new(); // أي سنة مرتبطة به الوسائط

        // ============================================
        // 📌 الخطوة 4: فرص العمل
        // ============================================
        public List<string> JobOpportunityNames { get; set; } = new();
        public List<string> JobOpportunityDescriptions { get; set; } = new();
    }
}
