using System;
using System.Linq;
using System.Threading.Tasks;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Controllers
{
    public class StockAlertController(DrugInventoryContext context) : Controller
    {
        public async Task<IActionResult> Index()
        {
            // ดึงข้อมูลยาและเวชภัณฑ์ทั้งหมดในระบบ (ที่ใช้งานอยู่)
            var allMedicines = await context.Medicines
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status == "Active")
                .OrderBy(m => m.Expired_at.HasValue ? m.Expired_at.Value : DateTime.MaxValue)
                .ToListAsync();

            return View(allMedicines);
        }

        // ปิดใช้งานล็อตยาจริง (งดจ่าย) — อนุญาตเฉพาะล็อตที่หมดอายุแล้ว หรือใกล้หมดอายุภายใน 1 เดือนเท่านั้น
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableLot(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "ไม่พบรหัสยาที่ต้องการปิดใช้งาน";
                return RedirectToAction("Index");
            }

            var medicine = await context.Medicines.FindAsync(id);
            if (medicine == null)
            {
                TempData["Error"] = "ไม่พบรายการยานี้ในระบบ";
                return RedirectToAction("Index");
            }

            // ตรวจสอบซ้ำฝั่ง Server: อนุญาตให้ปิดใช้งานได้เฉพาะรายการที่หมดอายุแล้ว หรือใกล้หมดอายุภายใน 1 เดือน
            var oneMonthFromNow = DateTime.Today.AddMonths(1);
            bool isEligibleToDisable = medicine.Expired_at.HasValue && medicine.Expired_at.Value.Date <= oneMonthFromNow;

            if (!isEligibleToDisable)
            {
                TempData["Error"] = $"ไม่สามารถปิดใช้งาน {medicine.Medicine_name} ได้ เนื่องจากยังไม่อยู่ในเกณฑ์ใกล้หมดอายุภายใน 1 เดือน";
                return RedirectToAction("Index");
            }

            medicine.Status = "Inactive";
            await context.SaveChangesAsync();

            TempData["Success"] = $"ปิดใช้งานล็อต {medicine.Lot} ({medicine.Medicine_name}) เรียบร้อยแล้ว ระบบจะงดจ่ายยาล็อตนี้ทันที";
            return RedirectToAction("Index");
        }
    }
}