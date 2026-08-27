using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering; // 🆕 เพิ่มเพื่อใช้งาน SelectList

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

            var users = _context.Users
                .Include(u => u.Departments)
                .AsNoTracking()
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
            ViewBag.Departments = new SelectList(GetUniqueDepartments(), "Department_id", "Department_name", user.Department_id);
            return View(user);
        }

        // แก้ไข (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(User u)
        {
            if (ModelState.IsValid)
            {
                u.Firstname = u.Firstname?.Trim();
                u.Lastname = u.Lastname?.Trim();

                _context.Users?.Update(u);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }

            // 🛠️ แก้ไข: ปรับตรงนี้ให้เป็น SelectList 
            ViewBag.Departments = new SelectList(GetUniqueDepartments(), "Department_id", "Department_name", u.Department_id);
            return View(u);
        }

        // ลบ
        public IActionResult Delete(string id)
        {
            if (_context.Users == null) return NotFound();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            try
            {
                _context.Users.Remove(user);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            catch (DbUpdateException)
            {
                string userFullName = $"{user.Title}{user.Firstname} {user.Lastname}";
                TempData["ErrorMessage"] = $"ผู้ใช้งานรหัส {id} ({userFullName}) มีประวัติผูกอยู่กับเอกสารธุรกรรมภายในคลัง จึงไม่สามารถลบออกจากระบบได้ เพื่อรักษาความถูกต้องของข้อมูลประวัติ";
                return RedirectToAction("Index");
            }
        }

        private List<Departments> GetUniqueDepartments()
        {
            if (_context.Departments == null) return new List<Departments>();

            return _context.Departments
                .AsEnumerable()
                .Where(d => d.Department_name != null)
                .GroupBy(d => d.Department_name!.Trim())
                .Select(g => g.First())
                .OrderBy(d => d.Department_name)
                .ToList();
        }
    }
}