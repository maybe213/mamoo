using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;

namespace DrugInventoryPro.Controllers
{
    public class DashboardController : Controller
    {
        private readonly DrugInventoryContext _context;

        public DashboardController(DrugInventoryContext context)
        {
            _context = context;
        }

        #region Helpers & Data Calculations

        // 💡 ฟังก์ชันคำนวณรายการสต็อกต่ำกว่าจุดสั่งซื้อซ้ำ (ROP)
        private async Task<List<LowStockViewModel>> GetLowStockListAsync()
        {
            var today = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);
            int defaultLeadTimeDays = 7; // Lead Time จัดส่งเฉลี่ย 7 วัน

            // 1. ดึงประวัติการจ่ายยาที่อนุมัติแล้ว 30 วันย้อนหลัง
            var recentDispenseData = await _context.DispenseDetails
                .AsNoTracking()
                .Include(dd => dd.Dispense)
                .Where(dd => dd.Dispense != null &&
                             dd.Dispense.Status == "Approved" &&
                             dd.Dispense.Dispense_date >= thirtyDaysAgo)
                .GroupBy(dd => dd.Medicine_id)
                .Select(g => new
                {
                    MedicineId = g.Key,
                    TotalDispensed = g.Sum(x => x.Quantity_dispensed ?? 0)
                })
                .ToDictionaryAsync(x => x.MedicineId, x => x.TotalDispensed);

            // 2. ดึงรายการยาทั้งหมดที่ Active
            var allMedicines = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive")
                .ToListAsync();

            var alertList = new List<LowStockViewModel>();

            foreach (var med in allMedicines)
            {
                int currentStock = med.Stock ?? 0;

                // ป้องกัน ROP เป็น 0: หาก SafetyStock เป็น NULL/0 ให้ใช้ค่าจาก Quantity หรือ Default (10)
                int safetyStock = (med.SafetyStock.HasValue && med.SafetyStock.Value > 0)
                    ? med.SafetyStock.Value
                    : (med.Quantity.HasValue && med.Quantity.Value > 0 ? med.Quantity.Value : 10);

                // อัตราใช้ยารายวัน (30 วันย้อนหลัง)
                int totalDispensed30Days = recentDispenseData.TryGetValue(med.Medicine_id, out int dispensed) ? dispensed : 0;
                decimal avgDailyUsage = totalDispensed30Days / 30.0m;

                // คำนวณ ROP = (AvgDailyUsage * LeadTime) + SafetyStock
                int leadTimeDemand = (int)Math.Ceiling(avgDailyUsage * defaultLeadTimeDays);
                int rop = leadTimeDemand + safetyStock;

                // คำนวณ ROQ (ปริมาณแนะนำสั่งซื้อ)
                int maxStockLevel = Math.Max(rop * 2, safetyStock * 2);
                if (maxStockLevel == 0) maxStockLevel = 50;
                int suggestedROQ = Math.Max(0, maxStockLevel - currentStock);

                // ตรวจสอบสถานะ
                bool isCritical = currentStock <= safetyStock || currentStock == 0;
                bool isReorder = currentStock <= rop;
                bool isExpired = med.Expired_at.HasValue && med.Expired_at.Value.Date <= today;
                bool isExpiringSoon = med.Expired_at.HasValue && med.Expired_at.Value.Date > today && med.Expired_at.Value.Date <= today.AddDays(30);

                // กรองเฉพาะรายการที่ต้องแจ้งเตือน
                if (isReorder || isCritical)
                {
                    alertList.Add(new LowStockViewModel
                    {
                        Medicine = med,
                        CurrentStock = currentStock,
                        SafetyStock = safetyStock,
                        AvgDailyUsage = Math.Round(avgDailyUsage, 2),
                        LeadTimeDays = defaultLeadTimeDays,
                        ReorderPoint = rop,
                        SuggestedROQ = suggestedROQ,
                        IsCritical = isCritical,
                        IsReorder = isReorder,
                        IsExpired = isExpired,
                        IsExpiringSoon = isExpiringSoon
                    });
                }
            }

            return alertList.OrderBy(x => x.CurrentStock).ToList();
        }

        #endregion

        #region Actions

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var sevenDaysAgo = today.AddDays(-7);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var startOfYear = new DateTime(today.Year, 1, 1);

            // ดึงข้อมูลหลักจากฐานข้อมูลแบบ AsNoTracking เพื่อประสิทธิภาพสูงสุด
            var allMedicines = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.MedicineUnit)
                .Include(m => m.Category)
                .Where(m => m.Status != "Inactive")
                .ToListAsync();

            // ==========================================
            // [ภาค 1] ตัวแปรสถิติภาพรวม & จุดสั่งซื้อซ้ำ (ROP)
            // ==========================================
            ViewBag.TotalMedicines = allMedicines.Count;
            ViewBag.ExpiredCount = allMedicines.Count(m => m.Expired_at.HasValue && m.Expired_at.Value.Date < today);
            ViewBag.TotalRemainingStock = allMedicines.Sum(m => m.Stock ?? 0);

            // คำนวณ ROP ผ่านฟังก์ชันกลาง GetLowStockListAsync()
            var lowStockViewModels = await GetLowStockListAsync();

            // ส่งข้อมูลรายการยาที่สต็อกต่ำกว่า ROP ให้ View
            ViewBag.LowStock = lowStockViewModels.Select(x => x.Medicine).ToList();

            // ส่งจำนวนรายการจริงไปแสดงบน Card (ตัวเลขตรงกันกับหน้า LowStock)
            ViewBag.LowStockCount = lowStockViewModels.Count;

            // รวมวันที่มียาหมดอายุเพื่อส่งไปแสดงบนปฏิทิน
            ViewBag.ExpiredDatesJson = allMedicines
                .Where(m => m.Expired_at.HasValue)
                .Select(m => m.Expired_at.Value.ToString("yyyy-MM-dd"))
                .Distinct()
                .ToList();

            // ส่งข้อมูลรายละเอียดรายการยาหมดอายุสำหรับแสดง Modal
            ViewBag.ExpiringMedicineDetails = allMedicines
                .Where(m => m.Expired_at.HasValue)
                .Select(m => new
                {
                    name = m.Medicine_name ?? "ไม่ระบุชื่อ",
                    dateStr = m.Expired_at.Value.ToString("yyyy-MM-dd"),
                    isExpired = m.Expired_at.Value.Date < today,
                    stock = m.Stock ?? 0,
                    unit = m.MedicineUnit?.Unit_name ?? "ชิ้น"
                })
                .ToList();

            // ==========================================
            // [ภาค 2] ยาที่เบิกจ่ายมากที่สุด และน้อยที่สุด 5 อันดับ (เฉพาะที่อนุมัติแล้ว)
            // ==========================================
            ViewBag.Top5MostDispensed = await _context.DispenseDetails
                .AsNoTracking()
                .Where(x => x.Dispense != null && x.Dispense.Status == "Approved")
                .GroupBy(x => x.Medicine.Medicine_name)
                .Select(g => new
                {
                    Name = g.Key ?? "ไม่ระบุชื่อ",
                    TotalQuantity = g.Sum(x => x.Quantity_dispensed ?? 0)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            ViewBag.Top5LeastDispensed = await _context.DispenseDetails
                .AsNoTracking()
                .Where(x => x.Dispense != null && x.Dispense.Status == "Approved")
                .GroupBy(x => x.Medicine.Medicine_name)
                .Select(g => new
                {
                    Name = g.Key ?? "ไม่ระบุชื่อ",
                    TotalQuantity = g.Sum(x => x.Quantity_dispensed ?? 0)
                })
                .OrderBy(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            // ==========================================
            // [ภาค 3] ข้อมูล Real-time แผงควบคุมใบเบิกจ่าย
            // ==========================================
            ViewBag.PendingDispense = await _context.Dispense.CountAsync(x => x.Status == "Pending");
            ViewBag.ApprovedDispense = await _context.Dispense.CountAsync(x => x.Status == "Approved");
            ViewBag.RejectedDispense = await _context.Dispense.CountAsync(x => x.Status == "Rejected");

            ViewBag.CurrentMonthDispenseTotal = await _context.Dispense
                .CountAsync(d => d.Dispense_date.HasValue &&
                                 d.Dispense_date.Value.Month == today.Month &&
                                 d.Dispense_date.Value.Year == today.Year);

            // ==========================================
            // [ภาค 4] รายงานประจำงวด 3 ระยะเวลา (สัปดาห์ / เดือน / ปี)
            // ==========================================
            var weeklyDispenses = await _context.DispenseDetails
                .AsNoTracking()
                .Include(dd => dd.Dispense)
                .Where(dd => dd.Dispense.Dispense_date.HasValue && dd.Dispense.Dispense_date.Value >= sevenDaysAgo)
                .ToListAsync();

            var weeklyInbounds = await _context.ReceiveDetails
                .AsNoTracking()
                .Include(rd => rd.Receive)
                .Where(rd => rd.Receive.Receive_date >= sevenDaysAgo)
                .ToListAsync();

            ViewBag.WeeklyInboundCount = weeklyInbounds.Sum(rd => (int?)rd.Quantity_received ?? 0);
            ViewBag.WeeklyOutboundCount = weeklyDispenses.Where(dd => dd.Dispense.Status == "Approved").Sum(dd => dd.Quantity_dispensed ?? 0);
            ViewBag.WeeklyNetStock = ViewBag.WeeklyInboundCount - ViewBag.WeeklyOutboundCount;

            var monthlyDispenses = await _context.DispenseDetails
                .AsNoTracking()
                .Include(dd => dd.Dispense)
                .Where(dd => dd.Dispense.Dispense_date.HasValue && dd.Dispense.Dispense_date.Value >= startOfMonth)
                .ToListAsync();

            var monthlyInbounds = await _context.ReceiveDetails
                .AsNoTracking()
                .Include(rd => rd.Receive)
                .Where(rd => rd.Receive.Receive_date >= startOfMonth)
                .ToListAsync();

            ViewBag.MonthlyInboundCount = monthlyInbounds.Sum(rd => (int?)rd.Quantity_received ?? 0);
            ViewBag.MonthlyOutboundCount = monthlyDispenses.Where(dd => dd.Dispense.Status == "Approved").Sum(dd => dd.Quantity_dispensed ?? 0);
            ViewBag.MonthlyNetStock = ViewBag.MonthlyInboundCount - ViewBag.MonthlyOutboundCount;

            var yearlyDispenses = await _context.DispenseDetails
                .AsNoTracking()
                .Include(dd => dd.Dispense)
                .Where(dd => dd.Dispense.Dispense_date.HasValue && dd.Dispense.Dispense_date.Value >= startOfYear)
                .ToListAsync();

            var yearlyInbounds = await _context.ReceiveDetails
                .AsNoTracking()
                .Include(rd => rd.Receive)
                .Where(rd => rd.Receive.Receive_date >= startOfYear)
                .ToListAsync();

            ViewBag.YearlyInboundCount = yearlyInbounds.Sum(rd => (int?)rd.Quantity_received ?? 0);
            ViewBag.YearlyOutboundCount = yearlyDispenses.Where(dd => dd.Dispense.Status == "Approved").Sum(dd => dd.Quantity_dispensed ?? 0);
            ViewBag.YearlyNetStock = ViewBag.YearlyInboundCount - ViewBag.YearlyOutboundCount;

            // ==========================================
            // [ภาค 5] ข้อมูลกราฟสถิติย้อนหลัง 6 เดือน
            // ==========================================
            var monthlyUsageData = new List<int>();
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = today.AddMonths(-i);
                int monthCount = await _context.Dispense
                    .CountAsync(d => d.Dispense_date.HasValue &&
                                     d.Dispense_date.Value.Month == targetMonth.Month &&
                                     d.Dispense_date.Value.Year == targetMonth.Year);
                monthlyUsageData.Add(monthCount);
            }
            ViewBag.MonthlyUsage = monthlyUsageData;

            return View();
        }

        // ==========================================
        // Export Excel รายงาน บัญชีรับ-จ่ายเวชภัณฑ์ (รบ 301)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ExportDrugstoreReport(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = endDate ?? DateTime.Today;

            var medicines = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.MedicineUnit)
                .ToListAsync();

            var receivesInRange = await _context.ReceiveDetails
                .AsNoTracking()
                .Include(r => r.Receive)
                .Where(r => r.Receive.Receive_date != null &&
                            r.Receive.Receive_date.Value.Date >= start.Date &&
                            r.Receive.Receive_date.Value.Date <= end.Date)
                .ToListAsync();

            var dispensesInRange = await _context.DispenseDetails
                .AsNoTracking()
                .Include(d => d.Dispense)
                .Where(d => d.Dispense.Dispense_date != null &&
                            d.Dispense.Dispense_date.Value.Date >= start.Date &&
                            d.Dispense.Dispense_date.Value.Date <= end.Date &&
                            d.Dispense.Status == "Approved")
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("rdrugstore_all");

                ws.Cell("F1").Value = "บัญชีรับ-จ่ายเวชภัณฑ์";
                ws.Cell("F1").Style.Font.Bold = true;
                ws.Cell("F1").Style.Font.FontSize = 14;
                ws.Cell("J1").Value = "แบบ รบ 301 (สรุปรวม)";

                ws.Cell("B3").Value = "สถานบริการ PCC ท่าทองใหม่ อ.กาญจนดิษฐ์(09156) ต.ท่าทองใหม่ อ.กาญจนดิษฐ์ จ.สุราษฎร์ธานี";
                ws.Cell("B5").Value = "วันที่ตัดยอดยาฯ ระหว่างวันที่";
                ws.Cell("D5").Value = $"( {start:dd MMM yyyy} - {end:dd MMM yyyy} )";

                int headerRow = 7;
                var headers = new[] { "ลำดับ", "ชื่อยา-เวชภัณฑ์", "ยอดยกมา", "มูลค่ายกมา", "รับ", "มูลค่ารับ", "จ่าย", "มูลค่าจ่าย", "รวม (คงเหลือ)", "มูลค่ารวม", "หน่วย" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(headerRow, i + 1).Value = headers[i];
                }

                var rngTableHead = ws.Range(headerRow, 1, headerRow, 11);
                rngTableHead.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                rngTableHead.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                rngTableHead.Style.Font.Bold = true;
                rngTableHead.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                int currentRow = 8;
                int index = 1;

                foreach (var med in medicines)
                {
                    var totalReceived = receivesInRange.Where(r => r.Medicine_id == med.Medicine_id).Sum(r => (int?)r.Quantity_received ?? 0);
                    var totalDispensed = dispensesInRange.Where(d => d.Medicine_id == med.Medicine_id).Sum(d => (int?)d.Quantity_dispensed ?? 0);
                    var price = med.Price ; // ใส่ Fallback ?? 0 ป้องกัน Null Reference

                    ws.Cell(currentRow, 1).Value = index++;
                    ws.Cell(currentRow, 2).Value = med.Medicine_name;
                    ws.Cell(currentRow, 3).Value = 0;
                    ws.Cell(currentRow, 4).Value = 0.00;
                    ws.Cell(currentRow, 5).Value = totalReceived;
                    ws.Cell(currentRow, 6).Value = totalReceived * price;
                    ws.Cell(currentRow, 7).Value = totalDispensed;
                    ws.Cell(currentRow, 8).Value = totalDispensed * price;
                    ws.Cell(currentRow, 9).Value = med.Stock ?? 0;
                    ws.Cell(currentRow, 10).Value = (med.Stock ?? 0) * price;
                    ws.Cell(currentRow, 11).Value = med.MedicineUnit?.Unit_name ?? "-";

                    currentRow++;
                }

                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                $"Report_รบ301_{start:yyyyMMdd}_to_{end:yyyyMMdd}.xlsx");
                }
            }
        }

        #endregion
    }
}