using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using DrugInventoryPro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Controllers
{
    /// <summary>หน้าแรกของผู้ดูแลระบบ (Admin): ภาพรวมผู้ใช้ หน้าที่ และแผนก พร้อมเลือกดูรายชื่อรายแผนก</summary>
    public class AdminDashboardController : Controller
    {
        private readonly DrugInventoryContext _context;

        public AdminDashboardController(DrugInventoryContext context)
        {
            _context = context;
        }

        private static bool IsRole(User u, string role) =>
            RoleHelper.Normalize(u.Role) == role;

        public async Task<IActionResult> Index(string? dept)
        {
            ViewData["HeaderTitle"] = "หน้าแรกผู้ดูแลระบบ";
            ViewData["HeaderIcon"] = "bi bi-speedometer2";

            var allDepartments = await _context.Departments.AsNoTracking().ToListAsync();
            var allUsers = await _context.Users.AsNoTracking().ToListAsync();

            var activeDepartments = allDepartments
                .Where(d => d.Status != "Inactive")
                .OrderBy(d => d.Department_name)
                .ToList();
            var activeDeptIds = activeDepartments.Select(d => d.Department_id).ToHashSet();

            var activeUsers = allUsers.Where(u => u.Status != "Inactive").ToList();
            var inactiveUsers = allUsers.Where(u => u.Status == "Inactive").ToList();

            var vm = new AdminDashboardViewModel
            {
                ActiveUsers = activeUsers.Count,
                InactiveUsers = inactiveUsers.Count,
                ActiveDepartments = activeDepartments.Count,
                InactiveDepartments = allDepartments.Count - activeDepartments.Count,
                AdminCount = activeUsers.Count(u => IsRole(u, "Admin")),
                PharmacistCount = activeUsers.Count(u => IsRole(u, "Pharmacist")),
                StaffCount = activeUsers.Count(u => IsRole(u, "Staff")),
            };
            vm.OtherRoleCount = vm.ActiveUsers - vm.AdminCount - vm.PharmacistCount - vm.StaffCount;
            vm.OtherRoles = activeUsers
                .Where(u => !IsRole(u, "Admin") && !IsRole(u, "Pharmacist") && !IsRole(u, "Staff"))
                .GroupBy(u => string.IsNullOrWhiteSpace(u.Role) ? "(ไม่ระบุ)" : u.Role!.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var d in activeDepartments)
            {
                var members = activeUsers.Where(u => u.Department_id == d.Department_id).ToList();
                vm.Departments.Add(new AdminDepartmentSummary
                {
                    Id = d.Department_id,
                    Name = d.Department_name ?? d.Department_id,
                    ContactName = $"{d.Contact_title}{d.Contact_firstname} {d.Contact_lastname}".Trim(),
                    Phone = d.Phone_number,
                    MemberCount = members.Count,
                    AdminCount = members.Count(u => IsRole(u, "Admin")),
                    PharmacistCount = members.Count(u => IsRole(u, "Pharmacist")),
                    StaffCount = members.Count(u => IsRole(u, "Staff")),
                    InactiveMemberCount = inactiveUsers.Count(u => u.Department_id == d.Department_id)
                });
            }

            // ผู้ใช้ที่ใช้งานอยู่แต่ไม่ได้ระบุแผนก หรือแผนกถูกปิดใช้งานไปแล้ว
            var noDeptUsers = activeUsers
                .Where(u => string.IsNullOrEmpty(u.Department_id) || !activeDeptIds.Contains(u.Department_id))
                .ToList();
            vm.UsersWithoutDepartment = noDeptUsers.Count;

            // รายชื่อสมาชิกของแผนกที่เลือก
            if (!string.IsNullOrEmpty(dept))
            {
                vm.SelectedDepartmentId = dept;
                if (dept == "none")
                {
                    vm.SelectedDepartmentName = "ไม่ระบุแผนก / แผนกถูกปิดใช้งาน";
                    vm.Members = noDeptUsers;
                }
                else
                {
                    var summary = vm.Departments.FirstOrDefault(x => x.Id == dept);
                    if (summary != null)
                    {
                        vm.SelectedDepartment = summary;
                        vm.SelectedDepartmentName = summary.Name;
                        vm.Members = activeUsers.Where(u => u.Department_id == dept).ToList();
                    }
                    else
                    {
                        vm.SelectedDepartmentId = null; // รหัสแผนกไม่ถูกต้อง/ถูกปิดใช้งาน
                    }
                }

                vm.Members = vm.Members
                    .OrderBy(u => u.Role == "Admin" ? 0 : u.Role == "Pharmacist" ? 1 : 2)
                    .ThenBy(u => u.Firstname)
                    .ToList();
            }

            return View(vm);
        }
    }
}