using System.Collections.Generic;

namespace DrugInventoryPro.Models
{
    public class AdminDepartmentSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int MemberCount { get; set; }        // ผู้ใช้ที่ใช้งานอยู่ในแผนกนี้
        public int AdminCount { get; set; }
        public int PharmacistCount { get; set; }
        public int StaffCount { get; set; }
        public int InactiveMemberCount { get; set; } // ผู้ใช้ที่ปิดใช้งานในแผนกนี้
    }

    public class AdminDashboardViewModel
    {
        // ภาพรวมผู้ใช้
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }

        // ภาพรวมแผนก
        public int ActiveDepartments { get; set; }
        public int InactiveDepartments { get; set; }

        // แยกตามหน้าที่ (Role) เฉพาะผู้ใช้ที่ใช้งานอยู่
        public int AdminCount { get; set; }
        public int PharmacistCount { get; set; }
        public int StaffCount { get; set; }
        public int OtherRoleCount { get; set; }
        public Dictionary<string, int> OtherRoles { get; set; } = new(); // ชื่อหน้าที่ที่ไม่ใช่ 3 แบบหลัก => จำนวน

        public List<AdminDepartmentSummary> Departments { get; set; } = new();
        public int UsersWithoutDepartment { get; set; }

        // แผนกที่เลือกดูรายชื่อ ("none" = ผู้ใช้ที่ไม่ได้ระบุแผนก)
        public string? SelectedDepartmentId { get; set; }
        public string SelectedDepartmentName { get; set; } = string.Empty;
        public AdminDepartmentSummary? SelectedDepartment { get; set; }
        public List<User> Members { get; set; } = new();
    }
}