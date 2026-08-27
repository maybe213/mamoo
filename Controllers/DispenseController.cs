using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace DrugInventoryPro.Controllers
{
    public class DispenseController : Controller
    {
        private readonly DrugInventoryContext _context;

        public DispenseController(DrugInventoryContext context)
        {
            _context = context;
        }

        // หน้าประวัติการจ่ายยา
        public async Task<IActionResult> Index()
        {
            var history = await _context.Dispense
                .Include(d => d.Departments) // 🟢 ดึงข้อมูลแผนกมาแสดงในหน้า Index
                .OrderByDescending(x => x.Dispense_id)
                .ToListAsync();
            return View(history);
        }

        // Helper จัดกลุ่มแยก "ยา" และ "เวชภัณฑ์" ใส่ ViewBag ให้ View "Catalog" ดึงไปใช้
        private void PopulateCatalogViewBag(List<Medicines> allItems)
        {
            ViewBag.Medicines = allItems
                .Where(m => m.Account_Type != "เวชภัณฑ์" &&
                            !(m.Category != null && m.Category.Category_name != null && m.Category.Category_name.Contains("เวชภัณฑ์")))
                .ToList();

            ViewBag.Supplies = allItems
                .Where(m => m.Account_Type == "เวชภัณฑ์" ||
                            (m.Category != null && m.Category.Category_name != null && m.Category.Category_name.Contains("เวชภัณฑ์")))
                .ToList();
        }

        // Helper วิเคราะห์ Prefix จาก Category หรือ Account_Type (MED / SUP)
        private async Task<string> ResolvePrefixAsync(string? categoryId, string? accountType = null)
        {
            if (!string.IsNullOrEmpty(accountType) && accountType.Trim().Equals("เวชภัณฑ์", StringComparison.OrdinalIgnoreCase))
                return "SUP";

            if (!string.IsNullOrEmpty(categoryId) && int.TryParse(categoryId, out int catId))
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Category_id == catId.ToString());

                if (category?.Category_name != null && category.Category_name.Contains("เวชภัณฑ์", StringComparison.OrdinalIgnoreCase))
                {
                    return "SUP";
                }
            }
            return "MED";
        }
        // หน้าดูรายละเอียดใบเบิกยา
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var mainData = await _context.Dispense
                .Include(d => d.Departments)
                .Include(d => d.DispenseDetails)
                    .ThenInclude(dd => dd.Medicine)
                .FirstOrDefaultAsync(d => d.Dispense_id == id);

            if (mainData == null) return NotFound();

            return View(mainData);
        }

        // หน้าสร้างใบเบิกยาใหม่ (GET)
        public async Task<IActionResult> Create()
        {
            // 🟢 ดึงรายการยา/เวชภัณฑ์ทั้งหมดที่ยังมีสต็อก พร้อมข้อมูลหมวดหมู่และหน่วยนับ
            // 🟢 จัดเรียงตามหลัก FEFO (First Expired, First Out): รายการที่ใกล้หมดอายุที่สุดจะถูกจัดไว้บนสุด
            //     รายการที่ไม่มีวันหมดอายุระบุไว้จะถูกจัดไว้ท้ายสุดของลำดับ
            var availableItems = await _context.Medicines
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Stock > 0 && m.Status != "Inactive")
                .OrderBy(m => m.Expired_at ?? DateTime.MaxValue)
                .ThenBy(m => m.Medicine_name)
                .ToListAsync();

            // 🟢 แยกกลุ่ม "ยา" กับ "เวชภัณฑ์" ใส่ ViewBag.Medicines / ViewBag.Supplies ให้ View นำไปแสดงแยกแท็บ
            //     (ลำดับ FEFO จากด้านบนยังคงอยู่ เพราะ Where() ไม่เปลี่ยนลำดับเดิม)
            PopulateCatalogViewBag(availableItems);

            ViewBag.DepartmentList = await _context.Departments
                .Where(d => d.Status == "Active")
                .OrderBy(d => d.Department_name)
                .ToListAsync();

            var lastItem = await _context.Dispense
                .OrderByDescending(x => x.Dispense_id)
                .FirstOrDefaultAsync();

            string previewId = "D001";
            if (lastItem != null && !string.IsNullOrEmpty(lastItem.Dispense_id))
            {
                if (lastItem.Dispense_id.StartsWith("D") && int.TryParse(lastItem.Dispense_id.Substring(1), out int lastNumber))
                {
                    previewId = "D" + (lastNumber + 1).ToString("D3");
                }
            }

            ViewBag.NewId = previewId;
            return View();
        }

        // API endpoint ดึงรหัสถัดไปตามหมวดหมู่
        private async Task<string> GetNextMedicineIdAsync()
        {
            var lastMed = await _context.Medicines
                .OrderByDescending(m => m.Medicine_id)
                .FirstOrDefaultAsync();

            if (lastMed == null || string.IsNullOrEmpty(lastMed.Medicine_id))
            {
                return "MED001";
            }

            // ดึงเฉพาะตัวเลขมาบวกเพิ่ม 1
            string numericPart = lastMed.Medicine_id.Replace("MED", "");
            if (int.TryParse(numericPart, out int number))
            {
                return $"MED{(number + 1):D3}";
            }

            return $"MED{DateTime.Now:ticks}".Substring(0, 8);
        }
        // CATALOG
        [HttpGet]
        public async Task<IActionResult> Catalog()
        {
            var allItems = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive")
                .ToListAsync();

            PopulateCatalogViewBag(allItems);
            ViewData["Title"] = "รายการยาและเวชภัณฑ์ทั้งหมด";

            return View();
        }

        // บันทึกสร้างใบเบิกยาใหม่ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Department_id, List<string> MedicineIds, List<int> DispenseQuantities) // 🟢 เพิ่มรับค่า Department_id
        {
            if (string.IsNullOrEmpty(Department_id))
            {
                TempData["Error"] = "กรุณาเลือกแผนกที่ต้องการเบิกยา";
                return RedirectToAction("Create");
            }

            if (MedicineIds == null || MedicineIds.Count == 0)
            {
                TempData["Error"] = "กรุณาเลือกรายการยาอย่างน้อย 1 รายการ";
                return RedirectToAction("Create");
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // ===== สร้าง ID =====
                    var lastItem = await _context.Dispense
                        .OrderByDescending(x => x.Dispense_id)
                        .FirstOrDefaultAsync();

                    string actualId = "D001";

                    if (lastItem != null && !string.IsNullOrEmpty(lastItem.Dispense_id))
                    {
                        if (lastItem.Dispense_id.StartsWith("D") &&
                            int.TryParse(lastItem.Dispense_id.Substring(1), out int lastNumber))
                        {
                            actualId = "D" + (lastNumber + 1).ToString("D3");
                        }
                    }

                    // ===== header =====
                    var header = new Dispense
                    {
                        Dispense_id = actualId,
                        Dispense_date = DateTime.Now,
                        Department_id = Department_id, // 🟢 บันทึก Department_id ลงในตาราง Dispense
                        Status = "Pending"
                    };

                    _context.Dispense.Add(header);

                    // ===== details =====
                    for (int i = 0; i < MedicineIds.Count; i++)
                    {
                        if (string.IsNullOrEmpty(MedicineIds[i]) || DispenseQuantities[i] <= 0)
                            continue;

                        var detail = new DispenseDetail
                        {
                            Dispense_id = actualId,
                            Medicine_id = MedicineIds[i],
                            Quantity_dispensed = DispenseQuantities[i]
                        };

                        _context.DispenseDetails.Add(detail);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = $"ส่งคำขอเบิกยาเลขที่ {actualId} เรียบร้อยแล้ว!";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "เกิดข้อผิดพลาด: " + ex.Message;
                }
            });

            return RedirectToAction("Create");
        }

        // หน้าจอหลักของฝั่งอนุมัติ
        public async Task<IActionResult> Approve()
        {
            var role = HttpContext.Session.GetString("Role");

            if (role != "Pharmacist")
            {
                TempData["Error"] = "คุณไม่มีสิทธิ์เข้าถึงหน้านี้";
                return RedirectToAction("Index");
            }

            var pendingList = await _context.Dispense
                .Include(d => d.Departments) // 🟢 ดึงข้อมูลแผนกมาแสดง
                .Include(d => d.DispenseDetails)
                    .ThenInclude(dd => dd.Medicine)
                .Where(d => d.Status == "Pending")
                .OrderBy(d => d.Dispense_date)
                .ToListAsync();

            return View(pendingList);
        }

        // GET: Dispenses/History
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var today = DateTime.Today;

            var historyData = await _context.Dispense
                .Include(d => d.Departments)
                .AsNoTracking()
                .Where(d => d.Dispense_date.HasValue &&
                            d.Dispense_date.Value.Month == today.Month &&
                            d.Dispense_date.Value.Year == today.Year)
                .OrderByDescending(d => d.Dispense_date)
                .ToListAsync();

            return View(historyData);
        }

        // กดยืนยันอนุมัติและหักสต็อก
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmApprove(string dispenseId, string[] keepDetailIds)
        {
            var role = HttpContext.Session.GetString("Role");

            if (role != "Pharmacist")
            {
                TempData["Error"] = "คุณไม่มีสิทธิ์อนุมัติใบเบิกยา";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(dispenseId))
                return NotFound();

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var dispense = await _context.Dispense
                        .Include(d => d.DispenseDetails)
                        .FirstOrDefaultAsync(d => d.Dispense_id == dispenseId);

                    if (dispense == null)
                        throw new Exception("ไม่พบข้อมูลใบเบิก");

                    foreach (var detail in dispense.DispenseDetails.ToList())
                    {
                        var med = await _context.Medicines.FindAsync(detail.Medicine_id);

                        if (med != null)
                        {
                            int qty = detail.Quantity_dispensed ?? 0;

                            if (med.Stock < qty)
                                throw new Exception($"ยา {med.Medicine_name} ในคลังไม่พอ");

                            med.Stock -= qty;
                        }
                    }

                    dispense.Status = "Approved";

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                TempData["Success"] = "อนุมัติใบเบิกเรียบร้อยแล้ว";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Approve));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id)
        {
            var role = HttpContext.Session.GetString("Role");

            if (role != "Pharmacist")
            {
                TempData["Error"] = "คุณไม่มีสิทธิ์ปฏิเสธใบเบิกยา";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                var dispense = await _context.Dispense.FindAsync(id);

                if (dispense == null)
                {
                    TempData["Error"] = "ไม่พบข้อมูลใบเบิก";
                    return RedirectToAction(nameof(Approve));
                }

                dispense.Status = "Rejected";

                await _context.SaveChangesAsync();

                TempData["Success"] = $"ปฏิเสธคำขอใบเบิกเลขที่ {id} เรียบร้อยแล้ว";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Approve));
        }
    }
}