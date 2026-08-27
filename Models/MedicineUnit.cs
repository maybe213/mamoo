using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace DrugInventoryPro.Models
{
    [Table("MedicineUnits")]
    public class MedicineUnit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Unit_id { get; set; }

        [Required]
        [StringLength(50)]
        public string? Unit_name { get; set; }

        [Required]
        [StringLength(100)]
        public string? Group_name { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string? Status { get; set; } = "Active";

        // Navigation Property: เชื่อมความสัมพันธ์แบบ One-to-Many ไปยังตารางยาหลัก
        // เพื่อให้ระบบรู้ว่ามีรายการยาใดบ้างที่กำลังใช้งานหน่วยนับนี้อยู่
        public ICollection<Medicines> Medicines { get; set; }
    }
}