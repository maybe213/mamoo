using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DrugInventoryPro.Controllers
{
    public class SettingsController : Controller
    {
        private readonly DrugInventoryContext _context;
        public SettingsController(DrugInventoryContext context) => _context = context;

        // ─── GET: /Settings ───────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            // 1. ดึงหมวดหมู่ทั้งหมดจากตาราง Categories
            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.Category_id)
                .ToListAsync() ?? new List<Category>();

            // 2. ดึงข้อมูลหน่วยนับโดยตรงจากตาราง MedicineUnits เรียงตามรหัสหลัก
            var unitIdsList = await _context.MedicineUnits
                .OrderBy(u => u.Unit_id)
                .ToListAsync();

            return View(unitIdsList);
        }

        // ─── POST: เพิ่มหมวดหมู่ใหม่ ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string Name, string Description, string RelatedDisease)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                TempData["Error"] = "กรุณากรอกชื่อหมวดหมู่";
                return RedirectToAction(nameof(Index));
            }

            string cleanedName = Name.Trim();

            // 🛡️ ป้องกันชื่อหมวดหมู่ซ้ำซ้อนในระบบ
            bool isDuplicate = await _context.Categories.AnyAsync(c => c.Category_name == cleanedName);
            if (isDuplicate)
            {
                TempData["Error"] = $"หมวดหมู่ '{cleanedName}' มีอยู่ในระบบแล้ว";
                return RedirectToAction(nameof(Index));
            }

            // เจนเนอเรตรหัสอัตโนมัติ (C001, C002, ...)
            var last = await _context.Categories
                .Where(c => c.Category_id.StartsWith("C"))
                .OrderByDescending(c => c.Category_id)
                .FirstOrDefaultAsync();

            string newId = "C001";
            if (last != null && last.Category_id?.Length > 1)
            {
                if (int.TryParse(last.Category_id.Substring(1), out int n))
                    newId = $"C{(n + 1):D3}";
            }

            _context.Categories.Add(new Category
            {
                Category_id = newId,
                Category_name = cleanedName,
                Description = Description?.Trim(),
                Related_disease = RelatedDisease?.Trim(),
                Status = "Active"
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"เพิ่มหมวดหมู่ '{cleanedName}' (รหัส {newId}) เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Index));
        }

        // ─── POST: แก้ไขหมวดหมู่ ───────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(string id, string Name, string Description, string RelatedDisease)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(Name))
            {
                TempData["Error"] = "ข้อมูลไม่ถูกต้อง";
                return RedirectToAction(nameof(Index));
            }

            var cat = await _context.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            string cleanedName = Name.Trim();

            // 🛡️ ตรวจสอบว่าชื่อใหม่ไม่ซ้ำกับหมวดหมู่อื่นในระบบ
            bool isDuplicate = await _context.Categories.AnyAsync(c => c.Category_name == cleanedName && c.Category_id != id);
            if (isDuplicate)
            {
                TempData["Error"] = $"ไม่สามารถแก้ไขได้ เนื่องจากชื่อหมวดหมู่ '{cleanedName}' ซ้ำกับระบบ";
                return RedirectToAction(nameof(Index));
            }

            cat.Category_name = cleanedName;
            cat.Description = Description?.Trim();
            cat.Related_disease = RelatedDisease?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "แก้ไขหมวดหมู่เรียบร้อยแล้ว";
            return RedirectToAction(nameof(Index));
        }

        // ─── GET: ลบหมวดหมู่ ───────────────────────────────────────────────
        public async Task<IActionResult> DeleteCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            bool hasLinked = await _context.Medicines.AnyAsync(m => m.Category_id == id);
            if (hasLinked)
            {
                TempData["Error"] = "ไม่สามารถลบได้ เนื่องจากมียาที่ใช้หมวดหมู่นี้อยู่";
                return RedirectToAction(nameof(Index));
            }

            var cat = await _context.Categories.FindAsync(id);
            if (cat != null)
            {
                _context.Categories.Remove(cat);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"ลบหมวดหมู่ '{cat.Category_name}' เรียบร้อยแล้ว";
            }
            return RedirectToAction(nameof(Index));
        }

        // ─── 🛠️ จัดการข้อมูลหลักตาราง MedicineUnits ───────────────────

        // ─── POST: เพิ่มหน่วยนับใหม่เข้าฐานข้อมูล ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnit_id(
    string Unit_idName,
    string GroupName,
    string? Description)
        {
            if (string.IsNullOrWhiteSpace(Unit_idName))
            {
                TempData["Error"] = "กรุณากรอกชื่อหน่วยนับ";
                return RedirectToAction(nameof(Index));
            }

            bool duplicate = await _context.MedicineUnits
                .AnyAsync(x =>
                 x.Unit_name == Unit_idName.Trim()
                 && x.Group_name == GroupName);

            if (duplicate)
            {
                TempData["Error"] =
                    $"หน่วยนับ '{Unit_idName}' ในกลุ่มนี้มีอยู่แล้ว";

                return RedirectToAction(nameof(Index));
            }

            var unit = new MedicineUnit
            {
                Unit_name = Unit_idName.Trim(),
                Group_name = GroupName,
                Description = Description?.Trim(),
                Status = "Active"
            };

            _context.MedicineUnits.Add(unit);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"เพิ่มหน่วยนับ '{Unit_idName}' สำเร็จ";

            return RedirectToAction(nameof(Index));
        }

        // ─── GET: ลบหน่วยนับออกจากคลังหลัก (ลบด้วยตัวเลขรหัส id) ──────────────────────────
        public async Task<IActionResult> DeleteUnit_id(int id)
        {
            var unit = await _context.MedicineUnits.FindAsync(id);
            if (unit == null) return NotFound();

            // 🛡️ ดักจับความสัมพันธ์เชิงโครงข้อมูล (int กับ int?): เช็คว่ามียาชิ้นใดใช้งานรหัสหน่วยนับนี้อยู่หรือไม่
            bool isUsed = await _context.Medicines.AnyAsync(m => m.Unit_id == unit.Unit_id);
            if (isUsed)
            {
                TempData["Error"] = $"ไม่สามารถลบได้ เนื่องจากรหัสหน่วยนับ '{unit.Unit_id}' มีการผูกใช้งานจริงกับรายการยาในระบบคลัง";
                return RedirectToAction(nameof(Index));
            }

            _context.MedicineUnits.Remove(unit);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"ลบรายการรหัสหน่วยนับ '{unit.Unit_id}' ออกจากฐานข้อมูลสำเร็จ";
            return RedirectToAction(nameof(Index));
        }
    }
}