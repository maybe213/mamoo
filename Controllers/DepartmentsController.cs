using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DrugInventoryPro.Controllers
{
    public class DepartmentsController : Controller
    {
        private readonly DrugInventoryContext _context;

        public DepartmentsController(DrugInventoryContext context)
        {
            _context = context;
        }

        // GET: Departments
        public async Task<IActionResult> Index()
        {
            // แสดงเฉพาะแผนกที่ใช้งานอยู่ (แผนกที่ปิดใช้งานดูได้ที่หน้า Inactive)
            var list = await _context.Departments
                .Where(d => d.Status != "Inactive")
                .OrderBy(d => d.Department_id)
                .ToListAsync();
            return View(list);
        }

        // GET: Departments/Inactive — รายการแผนกที่ปิดใช้งาน
        public async Task<IActionResult> Inactive()
        {
            var list = await _context.Departments
                .Where(d => d.Status == "Inactive")
                .OrderBy(d => d.Department_id)
                .ToListAsync();
            return View(list);
        }

        // GET: Departments/Create
        public async Task<IActionResult> Create()
        {
            // เจนรหัสอัตโนมัติแบบ Async ป้องกันหน้าเว็บค้าง
            var lastDept = await _context.Departments
                .Where(d => d.Department_id.StartsWith("DEP"))
                .OrderByDescending(d => d.Department_id)
                .FirstOrDefaultAsync();

            string nextId = "DEP001";
            if (lastDept != null)
            {
                if (int.TryParse(lastDept.Department_id.Substring(3), out int lastNumber))
                {
                    nextId = "DEP" + (lastNumber + 1).ToString("D3");
                }
            }

            ViewBag.AutoId = nextId;
            return View();
        }

        // POST: Departments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Departments department)
        {
            if (ModelState.IsValid)
            {
                // ตรวจสอบรหัสซ้ำเพื่อความปลอดภัยสูงสุด
                if (await _context.Departments.AnyAsync(d => d.Department_id == department.Department_id))
                {
                    ModelState.AddModelError("Department_id", "รหัสแผนกนี้มีในระบบแล้ว");
                    ViewBag.AutoId = department.Department_id;
                    return View(department);
                }

                department.Status = "Active";
                department.Created_at = DateTime.Now;
                department.Updated_at = DateTime.Now;

                _context.Add(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AutoId = department.Department_id;
            return View(department);
        }

        // GET: Departments/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            return View(department);
        }

        // POST: Departments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Departments department)
        {
            if (id != department.Department_id) return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.Departments.FindAsync(id);
                if (existing == null) return NotFound();

                // 🛡️ คัดลอกเฉพาะฟิลด์ที่แก้ไขได้ (คง Created_at และ Status เดิม ไม่ให้ถูกเขียนทับเป็นค่าว่าง)
                existing.Department_name = department.Department_name;
                existing.Contact_title = department.Contact_title;
                existing.Contact_firstname = department.Contact_firstname;
                existing.Contact_lastname = department.Contact_lastname;
                existing.Phone_number = department.Phone_number;
                existing.Updated_at = DateTime.Now;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }

        // ❌ ไม่มีการลบแผนกออกจากระบบ: ใช้ "ปิดใช้งาน" แทน เพื่อรักษาข้อมูลแผนกและเจ้าหน้าที่ผู้ติดต่อ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            // 🛡️ ห้ามปิดแผนกที่ยังมีผู้ใช้งานที่ใช้งานอยู่
            int activeUsers = await _context.Users.CountAsync(u => u.Department_id == id && u.Status != "Inactive");
            if (activeUsers > 0)
            {
                TempData["ErrorMessage"] = $"ไม่สามารถปิดใช้งานแผนก '{department.Department_name}' ได้ เพราะยังมีผู้ใช้งานที่ใช้งานอยู่ {activeUsers} คน กรุณาย้ายแผนกหรือปิดใช้งานผู้ใช้เหล่านั้นก่อน";
                return RedirectToAction(nameof(Index));
            }

            department.Status = "Inactive";
            department.Updated_at = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"ปิดใช้งานแผนก '{department.Department_name}' เรียบร้อยแล้ว (ข้อมูลยังอยู่ในระบบ)";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            department.Status = "Active";
            department.Updated_at = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"เปิดใช้งานแผนก '{department.Department_name}' อีกครั้งเรียบร้อยแล้ว";
            return RedirectToAction(nameof(Inactive));
        }
    }
}