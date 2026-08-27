using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        public string? User_id { get; set; }

        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }

        //  แยกข้อมูลชื่อ-นามสกุลของผู้สมัครสมาชิก
        public string? Title { get; set; }     // คำนำหน้าชื่อ
        public string? Firstname { get; set; } // ชื่อจริง (ภาษาไทย)
        public string? Lastname { get; set; }  // นามสกุล (ภาษาไทย)

        public string? P_number { get; set; }  // เบอร์โทรศัพท์ (คงเดิมไว้)
        public string? Em { get; set; }        // อีเมล (คงเดิมไว้)

        //  Connection to Department
        public string? Department_id { get; set; }

        [ForeignKey("Department_id")] // ผูกให้ตรงกับชื่อ Property ด้านบนครับ
        public Departments? Departments { get; set; }
    }
}