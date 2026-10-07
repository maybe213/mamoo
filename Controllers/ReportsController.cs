using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Controllers
{
    /// <summary>รายงานการรับ-จ่ายเวชภัณฑ์ประจำเดือน / ประจำปี / ปีงบประมาณ พร้อมส่งออก Excel ให้ฝ่ายบัญชี</summary>
    public class ReportsController : Controller
    {
        private readonly DrugInventoryContext _context;
        private static readonly CultureInfo Th = new CultureInfo("th-TH");
        private const string OrgName = "สถานบริการ PCC ท่าทองใหม่ อ.กาญจนดิษฐ์(09156) ต.ท่าทองใหม่ อ.กาญจนดิษฐ์ จ.สุราษฎร์ธานี";

        public ReportsController(DrugInventoryContext context)
        {
            _context = context;
        }

        private bool CanAccess()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Pharmacist";
        }

        [HttpGet]
        public async Task<IActionResult> Index(string type = "monthly", string? period = null, int? year = null, int? month = null)
        {
            if (!CanAccess()) return RedirectToAction("Index", "Dashboard");

            ViewData["HeaderTitle"] = "รายงานประจำเดือน / ประจำปี";
            ViewData["HeaderIcon"] = "bi bi-file-earmark-bar-graph";

            var vm = await BuildReportAsync(type, period, year, month);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string type = "monthly", int? year = null, int? month = null)
        {
            if (!CanAccess()) return RedirectToAction("Index", "Dashboard");

            var vm = await BuildReportAsync(type, null, year, month);

            using var workbook = new XLWorkbook();

            // ---------- Sheet 1: สรุปรายงาน ----------
            var ws = workbook.Worksheets.Add("สรุปรายงาน");
            ws.Cell("A1").Value = vm.Title;
            ws.Range("A1:C1").Merge();
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("A2").Value = OrgName;
            ws.Range("A2:C2").Merge();
            ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("A3").Value = vm.PeriodLabel;
            ws.Range("A3:C3").Merge();
            ws.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("A4").Value = $"พิมพ์เมื่อ {vm.GeneratedAt.ToString("d MMMM yyyy HH:mm", Th)} น.  โดย {vm.PreparedBy}";
            ws.Range("A4:C4").Merge();
            ws.Cell("A4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("A4").Style.Font.FontColor = XLColor.Gray;

            ws.Cell("A6").Value = "รายการ";
            ws.Cell("B6").Value = "จำนวนรายการยา";
            ws.Cell("C6").Value = "มูลค่า (บาท)";
            var head = ws.Range("A6:C6");
            head.Style.Font.Bold = true;
            head.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            head.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell("A7").Value = "ยอดยกมา";
            ws.Cell("A8").Value = "รับเข้าในงวด";
            ws.Cell("A9").Value = "จ่ายออกในงวด";
            ws.Cell("A10").Value = "ยอดคงเหลือยกไป";
            ws.Cell("B7").Value = vm.Rows.Count(r => r.OpeningQty != 0);
            ws.Cell("B8").Value = vm.Rows.Count(r => r.ReceivedQty != 0);
            ws.Cell("B9").Value = vm.Rows.Count(r => r.DispensedQty != 0);
            ws.Cell("B10").Value = vm.Rows.Count(r => r.ClosingQty != 0);
            ws.Cell("C7").Value = vm.TotalOpeningValue;
            ws.Cell("C8").Value = vm.TotalReceivedValue;
            ws.Cell("C9").Value = vm.TotalDispensedValue;
            ws.Cell("C10").Value = vm.TotalClosingValue;
            ws.Range("C7:C10").Style.NumberFormat.Format = "#,##0.00";
            ws.Range("A10:C10").Style.Font.Bold = true;
            ws.Range("A6:C10").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range("A6:C10").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.Cell("A12").Value = "หมายเหตุ: คำนวณจากใบรับเข้าและใบเบิกที่อนุมัติแล้วซึ่งบันทึกในระบบเท่านั้น มูลค่าใช้ราคาต่อหน่วยปัจจุบันในทะเบียนยา";
            ws.Cell("A12").Style.Font.FontColor = XLColor.Gray;

            ws.Cell("A15").Value = "ผู้จัดทำ ................................";
            ws.Cell("B15").Value = "ผู้ตรวจสอบ ................................";
            ws.Cell("C15").Value = "ผู้อนุมัติ ................................";
            ws.Cell("A16").Value = "(................................)";
            ws.Cell("B16").Value = "(................................)";
            ws.Cell("C16").Value = "(................................)";
            ws.Range("A15:C16").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(1).Width = 34; ws.Column(2).Width = 34; ws.Column(3).Width = 34;

            // ---------- Sheet 2: รายละเอียดรายการยา ----------
            var wd = workbook.Worksheets.Add("รายละเอียดรายการยา");
            var headers = new[] { "ลำดับ", "รหัสยา", "ชื่อยา-เวชภัณฑ์", "หน่วย", "ราคา/หน่วย",
                                  "ยอดยกมา", "มูลค่ายกมา", "รับ", "มูลค่ารับ", "จ่าย", "มูลค่าจ่าย", "คงเหลือ", "มูลค่าคงเหลือ" };
            for (int i = 0; i < headers.Length; i++) wd.Cell(1, i + 1).Value = headers[i];
            var hr = wd.Range(1, 1, 1, headers.Length);
            hr.Style.Font.Bold = true;
            hr.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            hr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2, idx = 1;
            foreach (var r in vm.Rows)
            {
                wd.Cell(row, 1).Value = idx++;
                wd.Cell(row, 2).Value = r.MedicineId;
                wd.Cell(row, 3).Value = r.Name;
                wd.Cell(row, 4).Value = r.Unit;
                wd.Cell(row, 5).Value = r.Price;
                wd.Cell(row, 6).Value = r.OpeningQty;
                wd.Cell(row, 7).FormulaA1 = $"F{row}*E{row}";
                wd.Cell(row, 8).Value = r.ReceivedQty;
                wd.Cell(row, 9).FormulaA1 = $"H{row}*E{row}";
                wd.Cell(row, 10).Value = r.DispensedQty;
                wd.Cell(row, 11).FormulaA1 = $"J{row}*E{row}";
                wd.Cell(row, 12).FormulaA1 = $"F{row}+H{row}-J{row}";
                wd.Cell(row, 13).FormulaA1 = $"L{row}*E{row}";
                row++;
            }
            int last = row - 1;
            if (last >= 2)
            {
                wd.Cell(row, 3).Value = "รวมมูลค่าทั้งสิ้น";
                foreach (var c in new[] { 7, 9, 11, 13 })
                {
                    var col = XLHelper.GetColumnLetterFromNumber(c);
                    wd.Cell(row, c).FormulaA1 = $"SUM({col}2:{col}{last})";
                }
                wd.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
                wd.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Double;
                wd.Range(2, 5, row, 5).Style.NumberFormat.Format = "#,##0.00";
                foreach (var c in new[] { 7, 9, 11, 13 })
                    wd.Range(2, c, row, c).Style.NumberFormat.Format = "#,##0.00";
                foreach (var c in new[] { 6, 8, 10, 12 })
                    wd.Range(2, c, last, c).Style.NumberFormat.Format = "#,##0";
            }
            wd.SheetView.FreezeRows(1);
            wd.Columns().AdjustToContents();

            // ---------- Sheet 3: สรุปรายเดือน (เฉพาะรายงานประจำปี/ปีงบประมาณ) ----------
            if (vm.Months.Count > 1)
            {
                var wm = workbook.Worksheets.Add("สรุปรายเดือน");
                var mh = new[] { "เดือน", "มูลค่ายกมา", "มูลค่ารับ", "มูลค่าจ่าย", "มูลค่าคงเหลือ" };
                for (int i = 0; i < mh.Length; i++) wm.Cell(1, i + 1).Value = mh[i];
                var mhr = wm.Range(1, 1, 1, mh.Length);
                mhr.Style.Font.Bold = true;
                mhr.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                mhr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                int mr = 2;
                foreach (var m in vm.Months)
                {
                    wm.Cell(mr, 1).Value = m.Label;
                    wm.Cell(mr, 2).Value = m.OpeningValue;
                    wm.Cell(mr, 3).Value = m.ReceivedValue;
                    wm.Cell(mr, 4).Value = m.DispensedValue;
                    wm.Cell(mr, 5).Value = m.ClosingValue;
                    mr++;
                }
                wm.Range(2, 2, mr - 1, 5).Style.NumberFormat.Format = "#,##0.00";
                wm.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            string fileKey = vm.Type == "monthly" ? $"Monthly_{vm.Start:yyyy-MM}"
                           : vm.Type == "fiscal" ? $"FiscalYear_{vm.Year + 543}"
                           : $"Annual_{vm.Year + 543}";
            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"InventoryReport_{fileKey}.xlsx");
        }

        // =====================================================================
        // คำนวณจากรายการที่บันทึกจริงในระบบเท่านั้น (ไม่อิงตัวเลขสต็อกปัจจุบัน ไม่มีค่าสมมุติ)
        //   ยอดยกมา = ใบรับเข้าทั้งหมดก่อนต้นงวด - ใบเบิกที่อนุมัติแล้วก่อนต้นงวด
        //   คงเหลือ = ยกมา + รับในงวด - จ่ายในงวด
        // แสดงเฉพาะยาที่มีรับ/จ่ายในงวด และเลือกได้เฉพาะเดือนที่มีรายการจริง
        // =====================================================================
        private async Task<InventoryReportViewModel> BuildReportAsync(string type, string? period, int? year, int? month)
        {
            var today = DateTime.Today;
            var cutoff = today.AddDays(1); // ไม่นับรายการที่ลงวันที่ล่วงหน้า
            type = (type ?? "monthly").ToLowerInvariant();
            if (type != "annual" && type != "fiscal") type = "monthly";

            var meds = await _context.Medicines.AsNoTracking().Include(m => m.MedicineUnit).ToListAsync();

            var recv = await _context.ReceiveDetails.AsNoTracking()
                .Where(r => r.Receive != null && r.Receive.Receive_date.HasValue && r.Receive.Receive_date.Value < cutoff)
                .Select(r => new { r.Medicine_id, Date = r.Receive.Receive_date.Value, Qty = (int?)r.Quantity_received ?? 0 })
                .ToListAsync();

            var disp = await _context.DispenseDetails.AsNoTracking()
                .Where(d => d.Dispense != null && d.Dispense.Status == "Approved" &&
                            d.Dispense.Dispense_date.HasValue && d.Dispense.Dispense_date.Value < cutoff)
                .Select(d => new { d.Medicine_id, Date = d.Dispense.Dispense_date.Value, Qty = d.Quantity_dispensed ?? 0 })
                .ToListAsync();

            // เดือนที่มีรายการรับ/จ่ายจริง (ใช้เป็นตัวเลือกในหน้ารายงาน)
            var availMonths = recv.Select(x => x.Date).Concat(disp.Select(x => x.Date))
                .Select(d => new DateTime(d.Year, d.Month, 1)).Distinct().OrderBy(d => d).ToList();
            var latest = availMonths.Count > 0 ? availMonths.Last() : new DateTime(today.Year, today.Month, 1);

            if (!string.IsNullOrEmpty(period) &&
                DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var pd))
            {
                year = pd.Year; month = pd.Month;
            }

            int y, mon;
            if (type == "monthly") { y = year ?? latest.Year; mon = Math.Clamp(month ?? latest.Month, 1, 12); }
            else if (type == "fiscal") { y = year ?? (latest.Month >= 10 ? latest.Year + 1 : latest.Year); mon = latest.Month; }
            else { y = year ?? latest.Year; mon = latest.Month; }
            if (y < 2000 || y > 2200) y = latest.Year;

            DateTime start, end;
            if (type == "monthly") { start = new DateTime(y, mon, 1); end = start.AddMonths(1).AddDays(-1); }
            else if (type == "fiscal") { start = new DateTime(y - 1, 10, 1); end = new DateTime(y, 9, 30); }
            else { start = new DateTime(y, 1, 1); end = new DateTime(y, 12, 31); }

            var slotStarts = new List<DateTime>();
            if (type == "monthly") slotStarts.Add(start);
            else for (int i = 0; i < 12; i++) slotStarts.Add(start.AddMonths(i));
            int n = slotStarts.Count;

            var recvBy = recv.ToLookup(x => x.Medicine_id);
            var dispBy = disp.ToLookup(x => x.Medicine_id);

            int SlotIndex(DateTime d) => (d.Year - start.Year) * 12 + d.Month - start.Month;

            var rows = new List<InventoryReportRow>();
            var monthOpen = new decimal[n]; var monthRecv = new decimal[n];
            var monthDisp = new decimal[n]; var monthClose = new decimal[n];

            foreach (var med in meds)
            {
                var recvSlot = new int[n]; var dispSlot = new int[n];
                int opening0 = 0;

                foreach (var x in recvBy[med.Medicine_id])
                {
                    var dt = x.Date.Date;
                    if (dt < start) opening0 += x.Qty;
                    else if (dt <= end) { int i = SlotIndex(dt); if (i >= 0 && i < n) recvSlot[i] += x.Qty; }
                }
                foreach (var x in dispBy[med.Medicine_id])
                {
                    var dt = x.Date.Date;
                    if (dt < start) opening0 -= x.Qty;
                    else if (dt <= end) { int i = SlotIndex(dt); if (i >= 0 && i < n) dispSlot[i] += x.Qty; }
                }

                // แสดงเฉพาะรายการที่มีการรับหรือจ่ายในงวดนั้น
                if (recvSlot.Sum() == 0 && dispSlot.Sum() == 0) continue;

                var opening = new int[n]; var closing = new int[n];
                for (int i = 0; i < n; i++)
                {
                    opening[i] = i == 0 ? opening0 : closing[i - 1];
                    closing[i] = opening[i] + recvSlot[i] - dispSlot[i];
                }

                rows.Add(new InventoryReportRow
                {
                    MedicineId = med.Medicine_id,
                    Name = med.Medicine_name ?? "ไม่ระบุชื่อยา",
                    Unit = med.MedicineUnit?.Unit_name ?? "-",
                    Price = med.Price,
                    OpeningQty = opening[0],
                    ReceivedQty = recvSlot.Sum(),
                    DispensedQty = dispSlot.Sum()
                });

                for (int i = 0; i < n; i++)
                {
                    monthOpen[i] += opening[i] * med.Price;
                    monthRecv[i] += recvSlot[i] * med.Price;
                    monthDisp[i] += dispSlot[i] * med.Price;
                    monthClose[i] += closing[i] * med.Price;
                }
            }

            var vm = new InventoryReportViewModel
            {
                Type = type,
                Year = y,
                Month = mon,
                Start = start,
                End = end,
                IsOpenPeriod = end >= today,
                AvailableMonths = availMonths,
                PreparedBy = HttpContext.Session.GetString("FullName") ?? "ผู้ใช้งานระบบ",
                GeneratedAt = DateTime.Now,
                Rows = rows.OrderBy(r => r.MedicineId, StringComparer.OrdinalIgnoreCase).ToList()
            };

            vm.Title = type == "monthly" ? $"รายงานการรับ-จ่ายเวชภัณฑ์ประจำเดือน {start.ToString("MMMM yyyy", Th)}"
                     : type == "fiscal" ? $"รายงานการรับ-จ่ายเวชภัณฑ์ประจำปีงบประมาณ พ.ศ. {y + 543}"
                     : $"รายงานการรับ-จ่ายเวชภัณฑ์ประจำปี พ.ศ. {y + 543}";
            vm.PeriodLabel = $"ระหว่างวันที่ {start.ToString("d MMMM yyyy", Th)} ถึงวันที่ {end.ToString("d MMMM yyyy", Th)}";

            if (n > 1)
            {
                for (int i = 0; i < n; i++)
                {
                    vm.Months.Add(new InventoryReportMonth
                    {
                        Label = slotStarts[i].ToString("MMMM yyyy", Th),
                        OpeningValue = monthOpen[i],
                        ReceivedValue = monthRecv[i],
                        DispensedValue = monthDisp[i],
                        ClosingValue = monthClose[i]
                    });
                }
            }

            return vm;
        }
    }
}