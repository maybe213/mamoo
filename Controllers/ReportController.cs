using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models; // 👈 เรียกใช้งาน Namespace ที่เก็บดีไซน์คลาสโมเดล
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DrugInventoryPro.Controllers
{
    public class ReportController : Controller
    {
        private readonly DrugInventoryContext _context;

        public ReportController(DrugInventoryContext context)
        {
            _context = context;
        }

        // GET: /Report/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            // 1. คิวรีรวบรวมข้อมูลสถิติจริงจากคลังข้อมูล
            var reportData = await _context.Dispense
                .Where(d => d.Dispense_date >= startOfMonth && d.Dispense_date <= endOfMonth)
                .SelectMany(d => d.DispenseDetails)
                .Join(_context.Medicines,
                    detail => detail.Medicine_id,
                    m => m.Medicine_id,
                    (detail, m) => new { detail, m })
                .Join(_context.Categories,
                    combined => combined.m.Category_id,
                    c => c.Category_id,
                    (combined, c) => new { combined.detail, combined.m, c })
                .GroupBy(x => new
                {
                    DiseaseName = x.c.Related_disease ?? "ทั่วไป/ไม่ระบุโรค",
                    CategoryName = x.c.Category_name ?? "ไม่ระบุหมวดหมู่"
                })
                .Select(g => new
                {
                    DiseaseName = g.Key.DiseaseName,
                    CategoryName = g.Key.CategoryName,
                    TotalQuantityUsed = g.Sum(x => x.detail.Quantity_dispensed ?? 0),
                    MedicineCount = g.Select(x => x.m.Medicine_id).Distinct().Count()
                })
                .OrderByDescending(r => r.TotalQuantityUsed)
                .ToListAsync();

            // 2. วิเคราะห์ประเมินค่าข้อมูลกลุ่มเสี่ยงหลักใส่ใน ViewBag
            var topRiskDisease = reportData.FirstOrDefault();
            ViewBag.TopRiskDisease = topRiskDisease?.DiseaseName ?? "ไม่มีข้อมูลการจ่ายยาในเดือนนี้";
            ViewBag.TopRiskQuantity = topRiskDisease?.TotalQuantityUsed ?? 0;
            ViewBag.CurrentMonthText = DateTime.Today.ToString("MMMM yyyy", new System.Globalization.CultureInfo("th-TH"));

            // 3. 🛠️ แก้ไข: แมพแมทช์ข้อมูลตรงเข้าหาโครงสร้างคลาสดีไซน์ที่แท้จริง (Strongly-typed list)
            var viewModel = reportData.Select(r => new DiseaseReportViewModel
            {
                DiseaseName = r.DiseaseName,
                CategoryName = r.CategoryName,
                TotalQuantityUsed = r.TotalQuantityUsed,
                MedicineCount = r.MedicineCount
            }).ToList();

            return View(viewModel); // ส่งผลลัพธ์คอลเลกชันประเภทจัดเจนไปยังหน้า View
        }
    }
}