using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DrugInventoryPro.Controllers
{
    public class TrendsController : Controller
    {
        private readonly DrugInventoryContext _context;

        public TrendsController(DrugInventoryContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            ViewData["HeaderTitle"] = "แนวโน้มโรคและความเสี่ยง";
            ViewData["HeaderIcon"] = "bi bi-graph-up-arrow";

            // ==========================================
            //  วิเคราะห์แนวโน้มโรค (ย้อนหลัง 6 เดือน เฉพาะใบเบิกที่อนุมัติแล้ว)
            // ==========================================
            var trendStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);

            var rawDiseaseRows = await _context.DispenseDetails
                .AsNoTracking()
                .Where(dd => dd.Dispense != null &&
                             dd.Dispense.Status == "Approved" &&
                             dd.Dispense.Dispense_date.HasValue &&
                             dd.Dispense.Dispense_date.Value >= trendStart)
                .Select(dd => new
                {
                    RelatedDisease = dd.Medicine.Category.Related_disease,   // ✅ ใช้ related_disease แทน category_name
                    Dept = dd.Dispense.Departments.Department_name ?? "ไม่ระบุแผนก",
                    Date = dd.Dispense.Dispense_date.Value,
                    Qty = dd.Quantity_dispensed ?? 0
                })
                .ToListAsync();

            // ⚠️ related_disease อาจมีหลายโรคคั่นด้วยจุลภาค (เช่น "ไข้หวัด, ท้องเสีย, ท้องอืด")
            // จึงต้อง "แตก" แต่ละแถวออกเป็นรายโรค ก่อนนำไป Group เพื่อให้แสดงชื่อโรคจริง ไม่ใช่กลุ่มยาว
            var diseaseRows = rawDiseaseRows
                .SelectMany(x =>
                {
                    var names = string.IsNullOrWhiteSpace(x.RelatedDisease)
                        ? new[] { "ไม่ระบุโรค" }
                        : x.RelatedDisease.Split(',')
                            .Select(n => n.Trim())
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToArray();

                    return names.Select(name => new
                    {
                        Group = name,
                        Dept = x.Dept,
                        Date = x.Date,
                        Qty = x.Qty
                    });
                })
                .ToList();

            // 1) Top Diseases + ตารางรายละเอียด
            var topDiseases = diseaseRows
                .GroupBy(x => x.Group)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Qty) })
                .OrderByDescending(x => x.Total)
                .Take(8)
                .ToList();

            ViewBag.TopDiseaseLabels = topDiseases.Select(x => x.Name).ToList();
            ViewBag.TopDiseaseData = topDiseases.Select(x => x.Total).ToList();

            // 2) Disease Trends (5 โรคแรก x 6 เดือน)
            var thCulture = new System.Globalization.CultureInfo("th-TH");
            var months = Enumerable.Range(0, 6).Select(i => trendStart.AddMonths(i)).ToList();
            ViewBag.DiseaseTrendMonthLabels = months.Select(m => m.ToString("MMM yy", thCulture)).ToList();

            var trendSeries = new Dictionary<string, List<int>>();
            foreach (var name in topDiseases.Take(5).Select(x => x.Name))
            {
                trendSeries[name] = months
                    .Select(m => diseaseRows
                        .Where(x => x.Group == name && x.Date.Year == m.Year && x.Date.Month == m.Month)
                        .Sum(x => x.Qty))
                    .ToList();
            }
            ViewBag.DiseaseTrendSeries = trendSeries;

            // 3) Department Risk
            var deptRisk = diseaseRows
                .GroupBy(x => x.Dept)
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Qty) })
                .OrderByDescending(x => x.Total)
                .Take(8)
                .ToList();

            ViewBag.DepartmentRiskLabels = deptRisk.Select(x => x.Name).ToList();
            ViewBag.DepartmentRiskData = deptRisk.Select(x => x.Total).ToList();

            return View();
        }
    }
}