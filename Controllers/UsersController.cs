using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering; // 🆕 เพิ่มเพื่อใช้งาน SelectList
using Microsoft.AspNetCore.Http;
using DrugInventoryPro.Services;

namespace DrugInventoryPro.Controllers
{
    public class UsersController : Controller
    {
        private readonly DrugInventoryContext _context;

        public UsersController(DrugInventoryContext context)
        {
            _context = context;
        }

        // แสดงผู้ใช้ + แผนก
        public IActionResult Index()
        {
            if (_context.Users == null) return NotFound();

            // แสดงเฉพาะผู้ใช้ที่ยังใช้งานอยู่ (ผู้ที่ปิดใช้งานดูได้ที่หน้า Inactive)
            var users = _context.Users
                .Include(u => u.Departments)
                .AsNoTracking()
                .Where(u => u.Status != "Inactive")
                .OrderBy(u => u.User_id)
                .ToList();

            return View(users);
        }

        // รายชื่อผู้ใช้ที่ปิดใช้งาน
        public IActionResult Inactive()
        {
            if (_context.Users == null) return NotFound();

            var users = _context.Users
                .Include(u => u.Departments)
                .AsNoTracking()
                .Where(u => u.Status == "Inactive")
                .OrderBy(u => u.User_id)
                .ToList();

            return View(users);
        }

        // เพิ่ม (GET)
        public IActionResult Create()
        {
            string prefix = "USR";
            string nextId = $"{prefix}001";

            if (_context.Users != null)
            {
                var lastUser = _context.Users
                    .Where(u => u.User_id != null && u.User_id.StartsWith(prefix))
                    .OrderByDescending(u => u.User_id)
                    .FirstOrDefault();

                if (lastUser != null && lastUser.User_id != null)
                {
                    string numericPartStr = lastUser.User_id.Substring(prefix.Length);
                    if (int.TryParse(numericPartStr, out int lastNumber))
                    {
                        int nextNumber = lastNumber + 1;
                        nextId = $"{prefix}{nextNumber.ToString("D3")}";
                    }
                }
            }

            ViewBag.NextUserId = nextId; // 🔢 ส่งค่ารหัส เช่น USR004 ไปที่หน้าฟอร์ม

            // 🛠️ แก้ไข: แปลงให้เป็น SelectList ป้องกันตัว Tag Helper ใน View เกิดการ Binding พัง
            ViewBag.Departments = new SelectList(GetUniqueDepartments(), "Department_id", "Department_name");

            return View();
        }

        // เพิ่ม (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(User u)
        {
            ModelState.Remove("User_id");

            // 🛡️ หน้าที่ต้องเป็น Admin / Pharmacist / Staff เท่านั้น
            var normalizedRole = RoleHelper.Normalize(u.Role);
            if (normalizedRole == null)
                ModelState.AddModelError("Role", "กรุณาเลือกหน้าที่เป็น Admin, Pharmacist หรือ Staff");
            else
                u.Role = normalizedRole;

            // 🛡️ ชื่อผู้ใช้ต้องไม่ซ้ำ (ใช้ล็อกอิน)
            if (!string.IsNullOrWhiteSpace(u.Username) &&
                _context.Users != null && _context.Users.Any(x => x.Username == u.Username))
            {
                ModelState.AddModelError("Username", "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว");
            }

            var thaiRegex = new Regex(@"^[ก-๙\s]+$");

            if (string.IsNullOrWhiteSpace(u.Firstname) || !thaiRegex.IsMatch(u.Firstname))
            {
                ModelState.AddModelError("Firstname", "ชื่อจริงต้องเป็นภาษาไทยเท่านั้น");
            }
            if (string.IsNullOrWhiteSpace(u.Lastname) || !thaiRegex.IsMatch(u.Lastname))
            {
                ModelState.AddModelError("Lastname", "นามสกุลต้องเป็นภาษาไทยเท่านั้น");
            }

            if (!string.IsNullOrEmpty(u.P_number))
            {
                u.P_number = u.P_number.Replace(" ", "").Replace("-", "");

                var phoneRegex = new Regex(@"^[0-9]+$");
                if (!phoneRegex.IsMatch(u.P_number))
                {
                    ModelState.AddModelError("P_number", "เบอร์โทรศัพท์ต้องเป็นตัวเลขเท่านั้น");
                }
                else if (u.P_number.Length > 10)
                {
                    ModelState.AddModelError("P_number", "เบอร์โทรศัพท์ต้องมีความยาวไม่เกิน 10 หลัก");
                }
            }
            else
            {
                ModelState.AddModelError("P_number", "กรุณากรอกเบอร์โทรศัพท์");
            }

            if (ModelState.IsValid)
            {
                string prefix = "USR";
                string nextId = $"{prefix}001";

                if (_context.Users != null)
                {
                    var lastUser = _context.Users
                        .Where(user => user.User_id != null && user.User_id.StartsWith(prefix))
                        .OrderByDescending(user => user.User_id)
                        .FirstOrDefault();

                    if (lastUser != null && lastUser.User_id != null)
                    {
                        string numericPartStr = lastUser.User_id.Substring(prefix.Length);
                        if (int.TryParse(numericPartStr, out int lastNumber))
                        {
                            nextId = $"{prefix}{(lastNumber + 1).ToString("D3")}";
                        }
                    }
                }

                u.User_id = nextId;
                u.Firstname = u.Firstname?.Trim();
                u.Lastname = u.Lastname?.Trim();

                _context.Users?.Add(u);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            u.Username = string.Empty;
            u.Password = string.Empty;

            string failPrefix = "USR";
            string failNextId = $"{failPrefix}001";
            if (_context.Users != null)
            {
                var failLastUser = _context.Users
                    .Where(user => user.User_id != null && user.User_id.StartsWith(failPrefix))
                    .OrderByDescending(user => user.User_id)
                    .FirstOrDefault();

                if (failLastUser != null && failLastUser.User_id != null)
                {
                    string numericPartStr = failLastUser.User_id.Substring(failPrefix.Length);
                    if (int.TryParse(numericPartStr, out int lastNumber))
                    {
                        failNextId = $"{failPrefix}{(lastNumber + 1).ToString("D3")}";
                    }
                }
            }

            ViewBag.NextUserId = failNextId;

            // 🛠️ แก้ไข: ปรับตรงนี้ให้เป็น SelectList เช่นกันในกรณีที่ Submit ฟอร์มรอบแรกไม่ผ่าน
            ViewBag.Departments = new SelectList(GetUniqueDepartments(), "Department_id", "Department_name");

            return View(u);
        }

        // แก้ไข (GET)
        public IActionResult Edit(string id)
        {
            if (_context.Users == null) return NotFound();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            // 🛠️ แก้ไข: ปรับหน้าแก้ไขให้เป็น SelectList 
            ViewBag.Departments = new SelectList(GetUniqueDepartments(user.Department_id), "Department_id", "Department_name", user.Department_id);
            return View(user);
        }

        // แก้ไข (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(User u)
        {
            // 🛡️ หน้าที่ต้องเป็น Admin / Pharmacist / Staff เท่านั้น
            var normalizedRole = RoleHelper.Normalize(u.Role);
            if (normalizedRole == null)
                ModelState.AddModelError("Role", "กรุณาเลือกหน้าที่เป็น Admin, Pharmacist หรือ Staff");
            else
                u.Role = normalizedRole;

            // 🛡️ ชื่อผู้ใช้ต้องไม่ซ้ำกับคนอื่น
            if (!string.IsNullOrWhiteSpace(u.Username) &&
                _context.Users != null && _context.Users.Any(x => x.Username == u.Username && x.User_id != u.User_id))
            {
                ModelState.AddModelError("Username", "ชื่อผู้ใช้นี้ถูกใช้งานแล้ว");
            }

            if (ModelState.IsValid)
            {
                u.Firstname = u.Firstname?.Trim();
                u.Lastname = u.Lastname?.Trim();

                var existing = _context.Users?.Find(u.User_id);
                if (existing == null) return NotFound();

                // 🛡️ คัดลอกเฉพาะฟิลด์ที่อนุญาตให้แก้ไข (ไม่แตะ Status ผ่านหน้านี้ ต้องใช้ปุ่มปิด/เปิดใช้งาน)
                existing.Username = u.Username;
                existing.Role = u.Role;
                existing.Title = u.Title;
                existing.Firstname = u.Firstname;
                existing.Lastname = u.Lastname;
                existing.P_number = u.P_number;
                existing.Em = u.Em;
                existing.Department_id = u.Department_id;

                // 🛡️ เปลี่ยนรหัสผ่านเฉพาะเมื่อกรอกใหม่ (เว้นว่าง = คงรหัสผ่านเดิม)
                if (!string.IsNullOrWhiteSpace(u.Password))
                    existing.Password = u.Password;

                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            // 🛠️ แก้ไข: ปรับตรงนี้ให้เป็น SelectList 
            ViewBag.Departments = new SelectList(GetUniqueDepartments(u.Department_id), "Department_id", "Department_name", u.Department_id);
            return View(u);
        }

        // ❌ ไม่มีการลบผู้ใช้ออกจากระบบ: ใช้ "ปิดใช้งาน" แทน เพื่อรักษาประวัติการทำรายการ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Deactivate(string id)
        {
            if (_context.Users == null) return NotFound();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            // 🛡️ ห้ามปิดใช้งานบัญชีของตัวเองที่กำลังล็อกอินอยู่
            if (!string.IsNullOrEmpty(user.Username) &&
                user.Username == HttpContext.Session.GetString("Username"))
            {
                TempData["ErrorMessage"] = "ไม่สามารถปิดใช้งานบัญชีที่กำลังใช้งานอยู่ได้";
                return RedirectToAction("Index");
            }

            // 🛡️ ต้องเหลือ Admin ที่ใช้งานได้อย่างน้อย 1 คน
            if (user.Role == "Admin" &&
                !_context.Users.Any(x => x.User_id != id && x.Role == "Admin" && x.Status != "Inactive"))
            {
                TempData["ErrorMessage"] = "ต้องมีผู้ดูแลระบบ (Admin) ที่ใช้งานอยู่อย่างน้อย 1 คน";
                return RedirectToAction("Index");
            }

            user.Status = "Inactive";
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"ปิดใช้งานผู้ใช้ {user.Title}{user.Firstname} {user.Lastname} เรียบร้อยแล้ว (ข้อมูลยังอยู่ในระบบ)";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reactivate(string id)
        {
            if (_context.Users == null) return NotFound();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            user.Status = "Active";
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"เปิดใช้งานผู้ใช้ {user.Title}{user.Firstname} {user.Lastname} อีกครั้งเรียบร้อยแล้ว";
            return RedirectToAction("Inactive");
        }

        // แผนกที่เลือกได้: เฉพาะแผนกที่ใช้งานอยู่ (และแผนกปัจจุบันของผู้ใช้ แม้ปิดใช้งานแล้ว เพื่อไม่ให้ค่าเดิมหาย)
        private List<Departments> GetUniqueDepartments(string? includeId = null)
        {
            if (_context.Departments == null) return new List<Departments>();

            return _context.Departments
                .AsEnumerable()
                .Where(d => d.Department_name != null && (d.Status != "Inactive" || d.Department_id == includeId))
                .GroupBy(d => d.Department_name!.Trim())
                .Select(g => g.First())
                .OrderBy(d => d.Department_name)
                .ToList();
        }
    }
}