using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Controllers
{
    public class MedicinesController : Controller
    {
        private readonly DrugInventoryContext _context;

        public MedicinesController(DrugInventoryContext context)
        {
            _context = context;
        }

        private bool MedicineExists(string id)
            => _context.Medicines.Any(e => e.Medicine_id == id);

        #region Helpers & Data Preparation

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

        // ดึงเลขลำดับสูงสุดปัจจุบันใน DB (รองรับรูปแบบ MED001, MED-001)
        private async Task<int> GetCurrentMaxNumberAsync(string prefix)
        {
            var matchingIds = await _context.Medicines
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.Medicine_id != null && m.Medicine_id.StartsWith(prefix))
                .Select(m => m.Medicine_id!)
                .ToListAsync();

            int maxNum = 0;
            int prefixLength = prefix.Length;

            foreach (var id in matchingIds)
            {
                if (id.Length > prefixLength)
                {
                    string numPart = id.Substring(prefixLength).TrimStart('-', '_');
                    if (int.TryParse(numPart, out int num) && num > maxNum)
                    {
                        maxNum = num;
                    }
                }
            }

            return maxNum;
        }

        // Helper ในการสร้าง ID รันอัตโนมัติรายการเดียว
        private async Task<string> GetNextMedicineIdAsync(string prefix = "MED")
        {
            int maxNum = await GetCurrentMaxNumberAsync(prefix);
            return $"{prefix}{(maxNum + 1):D3}";
        }

        private async Task PrepareViewDataAsync(string prefix = "MED")
        {
            ViewBag.NextId = await GetNextMedicineIdAsync(prefix);

            ViewBag.Categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Category_id != null && c.Status == "Active")
                .OrderBy(c => c.Category_name)
                .ToListAsync();

            var activeUnits = await _context.MedicineUnits
                .AsNoTracking()
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.Group_name)
                .ThenBy(u => u.Unit_name)
                .ToListAsync();

            ViewBag.Units = activeUnits;

            ViewBag.Unit_idGroups = activeUnits
                .GroupBy(u => u.Group_name ?? "ทั่วไป")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(u => u.Unit_id.ToString()).Distinct().ToList()
                );
        }

        /// <summary>
        /// ฟังก์ชันกลางสำหรับคำนวณและดึงรายการยาที่สต็อกต่ำกว่าเกณฑ์ ROP (ใช้มาตรฐานเดียวกันทั้ง Controller)
        /// </summary>
        public async Task<List<LowStockViewModel>> GetLowStockListAsync()
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

                // 💡 ป้องกัน ROP เป็น 0: หาก SafetyStock ใน DB เป็น NULL/0 ให้ใช้ค่าจาก Quantity หรือใช้ Default (10)
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

                // 🎯 กรองเฉพาะยาที่สต็อกต่ำกว่า ROP, Safety Stock หรือสต็อกหมด (0)
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

        // API endpoint ดึงจำนวนรายการยาที่ต่ำกว่าจุดสั่งซื้อ (ไว้ใช้กับ Dashboard Stat Card)
        [HttpGet]
        public async Task<IActionResult> GetLowStockCount()
        {
            var list = await GetLowStockListAsync();
            return Json(new { count = list.Count });
        }

        // API endpoint ดึงรหัสถัดไปตามหมวดหมู่
        [HttpGet]
        public async Task<IActionResult> GetNextIdByCategory(int categoryId)
        {
            string prefix = await ResolvePrefixAsync(categoryId.ToString());
            string nextId = await GetNextMedicineIdAsync(prefix);
            return Json(new { nextId });
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

        // 1. เวชภัณฑ์หมดอายุ (/Medicines/Expired)
        [HttpGet]
        public async Task<IActionResult> Expired()
        {
            var today = DateTime.Today;
            var expiredItems = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive" && m.Expired_at.HasValue && m.Expired_at.Value.Date <= today)
                .ToListAsync();

            PopulateCatalogViewBag(expiredItems);
            ViewData["Title"] = "รายการยาและเวชภัณฑ์ที่หมดอายุแล้ว";
            return View("Catalog");
        }

        // 2. จำนวนคงเหลือสุทธิ (/Medicines/StockSummary)
        [HttpGet]
        public async Task<IActionResult> StockSummary()
        {
            var allItems = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive")
                .ToListAsync();

            PopulateCatalogViewBag(allItems);
            ViewData["Title"] = "รายการจำนวนคงเหลือสุทธิทั้งหมด";
            return View("Catalog");
        }

        // 3. ใกล้หมดอายุ 30 วัน (/Medicines/ExpiringSoon)
        [HttpGet]
        public async Task<IActionResult> ExpiringSoon()
        {
            var today = DateTime.Today;
            var next30Days = today.AddDays(30);

            var expiringItems = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive" &&
                            m.Expired_at.HasValue &&
                            m.Expired_at.Value.Date > today &&
                            m.Expired_at.Value.Date <= next30Days)
                .OrderBy(m => m.Expired_at)
                .ToListAsync();

            PopulateCatalogViewBag(expiringItems);
            ViewData["Title"] = "รายการยาและเวชภัณฑ์ใกล้หมดอายุ (ภายใน 30 วัน)";
            return View("Catalog");
        }

        /// <summary>
        /// คำนวณสต็อกต่ำสุด (Minimum Stock Formula - M) ในรูปแบบ Months of Supply
        /// </summary>
        private static int CalculateMinimumStock(decimal monthlyRate, decimal leadTimeMonths, decimal safetyStockMonths)
        {
            if (monthlyRate <= 0) return 0;

            decimal leadTimeDemand = monthlyRate * leadTimeMonths;
            decimal safetyStockDemand = monthlyRate * safetyStockMonths;

            decimal totalMinStock = leadTimeDemand + safetyStockDemand;

            return (int)Math.Ceiling(totalMinStock);
        }

        // 🌟 สต็อกต่ำกว่าเกณฑ์อัตโนมัติ (Automated ROP Alert) (/Medicines/LowStock)
        [HttpGet]
        public async Task<IActionResult> LowStock()
        {
            var alertList = await GetLowStockListAsync();
            return View(alertList);
        }

       
        // INDEX
        [HttpGet]
        public async Task<IActionResult> Index(string search, string sortBy)
        {
            // 🔹 1. ตรวจสอบและบันทึกค่า Sort ล่าสุดลง Session
            if (!string.IsNullOrEmpty(sortBy))
            {
                // หากผู้ใช้เลือกค่าใหม่ ให้บันทึกลง Session
                HttpContext.Session.SetString("Medicines_SortBy", sortBy);
            }
            else
            {
                // หากไม่มีการส่งค่ามา (กดเข้าหน้าเว็บใหม่) ให้ดึงค่าเดิมจาก Session (ถ้าไม่มีให้ใช้ค่าเริ่มต้น "id_asc")
                sortBy = HttpContext.Session.GetString("Medicines_SortBy") ?? "id_asc";
            }

            // ส่งค่า Sort ปัจจุบันไปให้ View
            ViewBag.CurrentSort = sortBy;

            var q = _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .Where(m => m.Status != "Inactive")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                q = q.Where(m => (m.Medicine_id != null && m.Medicine_id.Contains(search)) ||
                                 (m.Medicine_name != null && m.Medicine_name.Contains(search)));
            }
            return View(await q.ToListAsync());
        }
        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PrepareViewDataAsync();
            return View();
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Medicine_id,Medicine_name,Category_id,Unit_id,Price,Quantity,Stock,Lot,Expired_at,Packing_Size,Account_Type,Status,SafetyStock")] Medicines medicine, string? duplicateAction)
        {
            medicine.Medicine_name = medicine.Medicine_name?.Trim();
            medicine.Lot = medicine.Lot?.Trim();
            medicine.Account_Type = medicine.Account_Type?.Trim();

            if (!string.IsNullOrEmpty(medicine.Category_id))
            {
                bool ok = await _context.Categories.AnyAsync(c => c.Category_id == medicine.Category_id);
                if (!ok) ModelState.AddModelError("Category_id", "หมวดหมู่ที่เลือกไม่มีในระบบ");
            }

            if (medicine.Unit_id.HasValue)
            {
                bool unitIdOk = await _context.MedicineUnits.AnyAsync(u => u.Unit_id == medicine.Unit_id.Value);
                if (!unitIdOk) ModelState.AddModelError("Unit_id", "หน่วยนับที่เลือกไม่มีในระบบหลัก");
            }

            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var existingMedicine = await _context.Medicines
                            .FirstOrDefaultAsync(m => m.Medicine_name == medicine.Medicine_name &&
                                                      m.Category_id == medicine.Category_id &&
                                                      m.Account_Type == medicine.Account_Type &&
                                                      m.Lot == medicine.Lot &&
                                                      m.Status == "Active");

                        if (existingMedicine != null)
                        {
                            if (duplicateAction == "UpdateStock")
                            {
                                existingMedicine.Stock += medicine.Quantity;
                                _context.Medicines.Update(existingMedicine);
                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                TempData["SuccessMessage"] = $"บวกเพิ่มสต็อกเดิมให้เรียบร้อยแล้ว (สต็อกใหม่: {existingMedicine.Stock})";
                                return RedirectToAction(nameof(Index));
                            }
                            else if (duplicateAction == "ReplaceOld")
                            {
                                existingMedicine.Status = "Inactive";
                                _context.Medicines.Update(existingMedicine);

                                string prefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
                                medicine.Medicine_id = await GetNextMedicineIdAsync(prefix);
                                medicine.Status = "Active";

                                _context.Medicines.Add(medicine);
                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                TempData["SuccessMessage"] = "ปิดใช้งานรายการเก่า และสร้างล็อตใหม่ทดแทนเรียบร้อย";
                                return RedirectToAction(nameof(Index));
                            }
                            else
                            {
                                await transaction.RollbackAsync();

                                ViewBag.DuplicateFound = true;
                                ViewBag.DuplicateMessage = $"พบข้อมูลยา '{medicine.Medicine_name}' ล็อตหมายเลข '{medicine.Lot}' นี้ในระบบแล้ว";

                                string prefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
                                await PrepareViewDataAsync(prefix);

                                return View(medicine);
                            }
                        }

                        string newPrefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
                        medicine.Medicine_id = await GetNextMedicineIdAsync(newPrefix);
                        medicine.Status = "Active";

                        _context.Medicines.Add(medicine);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "เพิ่มรายการยาใหม่เรียบร้อยแล้ว";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }

            string defaultPrefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
            await PrepareViewDataAsync(defaultPrefix);
            return View(medicine);
        }

        // EDIT GET
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return NotFound();

            string prefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
            await PrepareViewDataAsync(prefix);
            return View(medicine);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Medicine_id,Medicine_name,Category_id,Unit_id,Quantity,Status,Price,Stock,Packing_Size,Account_Type,Expired_at,Lot,SafetyStock")] Medicines medicine)
        {
            if (id != medicine.Medicine_id) return NotFound();

            if (!string.IsNullOrEmpty(medicine.Category_id))
            {
                bool ok = await _context.Categories.AnyAsync(c => c.Category_id == medicine.Category_id);
                if (!ok) ModelState.AddModelError("Category_id", "หมวดหมู่ที่เลือกไม่มีในระบบ");
            }

            if (medicine.Unit_id.HasValue)
            {
                bool unitIdOk = await _context.MedicineUnits.AnyAsync(u => u.Unit_id == medicine.Unit_id.Value);
                if (!unitIdOk) ModelState.AddModelError("Unit_id", "หน่วยนับที่เลือกไม่มีในระบบหลัก");
            }

            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var existing = await _context.Medicines.FindAsync(id);
                        if (existing == null)
                        {
                            await transaction.RollbackAsync();
                            return NotFound();
                        }

                        existing.Medicine_name = medicine.Medicine_name?.Trim();
                        existing.Category_id = medicine.Category_id;
                        existing.Unit_id = medicine.Unit_id;
                        existing.Stock = medicine.Stock;
                        existing.Price = medicine.Price;
                        existing.Expired_at = medicine.Expired_at;
                        existing.Lot = medicine.Lot?.Trim();
                        existing.Packing_Size = medicine.Packing_Size?.Trim();
                        existing.Account_Type = medicine.Account_Type;
                        existing.Status = medicine.Status;
                        existing.Quantity = medicine.Quantity;
                        existing.SafetyStock = medicine.SafetyStock;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "แก้ไขข้อมูลยาเรียบร้อยแล้ว";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        await transaction.RollbackAsync();
                        if (!MedicineExists(medicine.Medicine_id)) return NotFound();
                        throw;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }

            string prefix = await ResolvePrefixAsync(medicine.Category_id, medicine.Account_Type);
            await PrepareViewDataAsync(prefix);
            return View(medicine);
        }

        // DETAILS
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var med = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.MedicineUnit)
                .FirstOrDefaultAsync(m => m.Medicine_id == id);

            return med == null ? NotFound() : View(med);
        }

        // DELETE GET
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var medicine = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Medicine_id == id);

            if (medicine == null) return NotFound();

            ViewBag.HasDispenseHistory = await _context.DispenseDetails.AnyAsync(d => d.Medicine_id == id);

            return View(medicine);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var med = await _context.Medicines.FindAsync(id);
                    if (med == null)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "ไม่พบข้อมูลยาที่ต้องการจัดการในระบบ";
                        return RedirectToAction(nameof(Index));
                    }

                    bool hasDispenseHistory = await _context.DispenseDetails.AnyAsync(d => d.Medicine_id == id);

                    if (med.Stock > 0 || hasDispenseHistory)
                    {
                        med.Stock = 0;
                        med.Status = "Inactive";

                        if (!string.IsNullOrEmpty(med.Medicine_name) && !med.Medicine_name.StartsWith("[ปิดใช้งาน]"))
                        {
                            med.Medicine_name = "[ปิดใช้งาน] " + med.Medicine_name;
                        }

                        _context.Medicines.Update(med);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = $"ปิดใช้งานและเซ็ตสต็อกของรหัส {id} เป็น 0 เรียบร้อยแล้ว";
                    }
                    else
                    {
                        _context.Medicines.Remove(med);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "ลบรายการยาที่เพิ่มผิดออกจากระบบถาวรเรียบร้อยแล้ว";
                    }

                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "เกิดข้อผิดพลาดในการจัดการข้อมูล หรือระบบไม่สามารถลบข้อมูลเนื่องจากข้อจำกัดทางคลัง";
                    return RedirectToAction(nameof(Index));
                }
            });
        }

        #endregion
    }
}