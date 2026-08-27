using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Departments")] // 🟢 ระบุชื่อตารางใน Database
    public class Departments
    {
        [Key]
        [Display(Name = "รหัสแผนก")]
        public string Department_id { get; set; } = null!;

        [Required(ErrorMessage = "กรุณากรอกชื่อแผนก")]
        [Display(Name = "ชื่อแผนก")]
        public string Department_name { get; set; } = null!;

        [Required(ErrorMessage = "กรุณาเลือกคำนำหน้า")]
        [Display(Name = "คำนำหน้า")]
        public string Contact_title { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกชื่อ")]
        [RegularExpression(@"^[ก-๙]+$", ErrorMessage = "กรุณากรอกชื่อเป็นภาษาไทยเท่านั้น")]
        [Display(Name = "ชื่อ")]
        public string Contact_firstname { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกนามสกุล")]
        [RegularExpression(@"^[ก-๙]+$", ErrorMessage = "กรุณากรอกนามสกุลเป็นภาษาไทยเท่านั้น")]
        [Display(Name = "นามสกุล")]
        public string Contact_lastname { get; set; } = string.Empty;

        [Display(Name = "เบอร์โทรศัพท์")]
        public string? Phone_number { get; set; }

        [Display(Name = "สถานะ")]
        public string Status { get; set; } = "Active";

        [Display(Name = "วันที่สร้างข้อมูล")]
        public DateTime? Created_at { get; set; }

        [Display(Name = "แก้ไขล่าสุด")]
        public DateTime? Updated_at { get; set; }
    }
}