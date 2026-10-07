using System;
using System.Linq;

namespace DrugInventoryPro.Services
{
    /// <summary>ระบบมีหน้าที่ (Role) แค่ 3 แบบ: Admin, Pharmacist, Staff</summary>
    public static class RoleHelper
    {
        public static readonly string[] Roles = { "Admin", "Pharmacist", "Staff" };

        /// <summary>ตัดช่องว่างและจับคู่แบบไม่สนตัวพิมพ์เล็กใหญ่ คืนค่ารูปแบบมาตรฐาน หรือ null ถ้าไม่ใช่ 3 แบบนี้</summary>
        public static string? Normalize(string? role)
        {
            var r = (role ?? "").Trim();
            return Roles.FirstOrDefault(x => string.Equals(x, r, StringComparison.OrdinalIgnoreCase));
        }
    }
}
