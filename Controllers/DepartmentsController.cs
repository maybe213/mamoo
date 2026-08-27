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
            var list = await _context.Departments.ToListAsync();
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
                try
                {
                    department.Updated_at = DateTime.Now; // อัปเดตเวลาปัจจุบันเมื่อมีการแก้ไข
                    _context.Update(department);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Departments.AnyAsync(e => e.Department_id == department.Department_id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }

        // GET: Departments/Delete/5
        // หมายเหตุ: หน้า Index ใช้แท็ก <a> ลบข้อมูล จึงต้องเป็น GET Method
        public async Task<IActionResult> Delete(string id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department != null)
            {
                try
                {
                    _context.Departments.Remove(department);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // ดักจับ Error ในกรณีที่แผนกนี้ไปผูกกับใบเบิกยาหรือผู้ใช้งานอื่นอยู่ จะลบไม่ได้
                    TempData["ErrorMessage"] = "ไม่สามารถลบแผนกนี้ได้ เนื่องจากข้อมูลถูกนำไปใช้งานในระบบแล้ว: " + ex.Message;
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}