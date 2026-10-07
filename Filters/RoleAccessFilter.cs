using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DrugInventoryPro.Filters
{
    /// <summary>
    /// ตัวกรองกลาง: ต้องล็อกอินก่อนใช้งานทุกหน้า (ยกเว้นหน้า Account) และจำกัดสิทธิ์ตาม Role
    /// กฎที่ไม่ได้ระบุไว้ = ผู้ที่ล็อกอินแล้วเข้าได้ทุก Role (เพื่อไม่ให้ลิงก์จาก Dashboard พัง)
    /// ลงทะเบียนใน Program.cs:
    ///   builder.Services.AddControllersWithViews(o => o.Filters.Add&lt;DrugInventoryPro.Filters.RoleAccessFilter&gt;());
    /// </summary>
    public class RoleAccessFilter : IActionFilter
    {
        private const string Admin = "Admin";
        private const string Pharmacist = "Pharmacist";
        private const string Staff = "Staff";

        // คีย์ "Controller/Action" ชนะคีย์ "Controller"
        private static readonly Dictionary<string, string[]> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            // --- ทั้ง Controller ---
            ["Users"]       = new[] { Admin },
            ["Departments"] = new[] { Admin },
            ["Settings"]    = new[] { Admin, Pharmacist },
            ["Reports"]     = new[] { Pharmacist },
            ["AdminDashboard"] = new[] { Admin },            // หน้าแรกของผู้ดูแลระบบ
            ["Dashboard"]   = new[] { Pharmacist, Staff },   // Admin ไม่มี Dashboard
            ["Trends"]      = new[] { Pharmacist, Staff },   // Admin ไม่มีหน้าแนวโน้ม
            ["Supplies"]    = new[] { Pharmacist },

            // --- รับยาเข้า ---
            ["Receive/Index"]          = new[] { Pharmacist },
            ["Receive/Preview"]        = new[] { Pharmacist },
            ["Receive/ConfirmReceive"] = new[] { Pharmacist },

            // --- อนุมัติ/ยกเลิกใบเบิก ---
            ["Dispense/Approve"]        = new[] { Pharmacist },
            ["Dispense/ConfirmApprove"] = new[] { Pharmacist },
            ["Dispense/Reject"]         = new[] { Pharmacist },
            ["Dispense/CancelApproved"] = new[] { Pharmacist },

            // --- แก้ไขข้อมูลยา (หน้าอ่านอย่างเดียวยังเปิดให้ทุก Role) ---
            ["Medicines/Create"]              = new[] { Pharmacist },
            ["Medicines/Edit"]                = new[] { Pharmacist },
            ["Medicines/Delete"]              = new[] { Pharmacist }, // รวม DeleteConfirmed (ActionName = Delete)
            ["Medicines/BulkDelete"]          = new[] { Pharmacist },
            ["Medicines/Restore"]             = new[] { Pharmacist },
            ["Medicines/DisabledList"]        = new[] { Pharmacist },
            ["Medicines/GetNextIdByCategory"] = new[] { Pharmacist },
            ["Medicines/SetReorderPoint"]     = new[] { Pharmacist },
            ["Medicines/BulkSetReorderPoint"] = new[] { Pharmacist },

            // --- ปิดใช้งานล็อตยา ---
            ["StockAlert/DisableLot"] = new[] { Pharmacist },
        };

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
            var action = context.RouteData.Values["action"]?.ToString() ?? "";

            if (controller.Equals("Account", StringComparison.OrdinalIgnoreCase)) return; // หน้าล็อกอิน/ล็อกเอาต์
            if (action.Equals("Error", StringComparison.OrdinalIgnoreCase)) return;

            var session = context.HttpContext.Session;
            var username = session.GetString("Username");
            var role = session.GetString("Role");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (!Rules.TryGetValue($"{controller}/{action}", out var allowed) &&
                !Rules.TryGetValue(controller, out allowed))
            {
                return; // ไม่มีกฎ = ผู้ล็อกอินแล้วเข้าได้
            }

            if (!allowed.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                if (context.Controller is Controller c)
                    c.TempData["Error"] = "คุณไม่มีสิทธิ์เข้าถึงหน้านี้";
                // Admin กลับหน้าแรกผู้ดูแลระบบ / เภสัชกรและเจ้าหน้าที่กลับ Dashboard / Role อื่นกลับหน้าล็อกอิน (กันวนซ้ำ)
                context.Result = role.Equals(Admin, StringComparison.OrdinalIgnoreCase) ? new RedirectToActionResult("Index", "AdminDashboard", null)
                               : (role.Equals(Pharmacist, StringComparison.OrdinalIgnoreCase) || role.Equals(Staff, StringComparison.OrdinalIgnoreCase))
                                   ? new RedirectToActionResult("Index", "Dashboard", null)
                                   : new RedirectToActionResult("Login", "Account", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
