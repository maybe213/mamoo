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
                .Where(m => m.Status != "Inactive")
                .OrderBy(m => m.Expired_at.HasValue ? m.Expired_at.Value : DateTime.MaxValue)
                .ToListAsync();

            return View(allMedicines);
        }
    }
}