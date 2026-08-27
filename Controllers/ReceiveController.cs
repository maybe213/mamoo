using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;
using DrugInventoryPro.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Controllers
{
    public class ReceiveController : Controller
    {
        private readonly DrugInventoryContext _context;
        private readonly ExcelParserService _excelParser;

        public ReceiveController(DrugInventoryContext context)
        {
            _context = context;
            _excelParser = new ExcelParserService();
        }

        // =====================================================================
        // 0. หน้าแรกแสดงประวัติการรับเข้าคลัง
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var receives = await _context.Receives
                .OrderByDescending(r => r.Receive_date)
                .ToListAsync();

            ViewBag.UserList = await _context.Users.ToListAsync();
            return View(receives);
        }

        // =====================================================================
        // 1. อัปโหลด Excel และ Preview ข้อมูล (อ่านจากทุก Sheet)
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewExcel(IFormFile excelFile, string invoiceNo, string receivedBy, string importType)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "กรุณาเลือกไฟล์ที่ต้องการอัปโหลด";
                return RedirectToAction("Index");
            }

            try
            {
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{excelFile.FileName}");
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await excelFile.CopyToAsync(stream);
                }

                // อ่านข้อมูลจากทุก Sheet
                var previewList = _excelParser.ParseExcelFile(tempFilePath, importType);

                if (previewList == null || !previewList.Any())
                {
                    TempData["ErrorMessage"] = "ไฟล์ Excel ว่างเปล่า หรือไม่มีข้อมูลที่ถูกต้อง";
                    return RedirectToAction("Index");
                }

                // ตรวจจับรายการซ้ำ
                var existingMedicines = await _context.Medicines.AsNoTracking().ToListAsync();
                _excelParser.DetectDuplicates(previewList, existingMedicines);

                ViewBag.InvoiceNo = invoiceNo;
                ViewBag.ReceivedBy = receivedBy;
                ViewBag.ImportType = importType;
                ViewBag.TempFilePath = tempFilePath;

                return View("PreviewExcel", previewList);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"เกิดข้อผิดพลาดในการอ่านไฟล์: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // =====================================================================
        // 2. บันทึกข้อมูลลง DB
        // =====================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = 5000)]
        public async Task<IActionResult> ConfirmReceive(
            string invoiceNo,
            string receivedBy,
            string importType,
            string tempFilePath,
            List<ReceiveDetail> items)
        {
            if (items == null || !items.Any())
            {
                TempData["ErrorMessage"] = "ไม่พบรายการที่ต้องการรับเข้าคลัง";
                return RedirectToAction("Index");
            }

            string generatedReceiveId = string.Empty;

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        generatedReceiveId = $"RCV-{DateTime.Now:yyyyMMddHHmmss}";
                        var dbMedicines = await _context.Medicines.ToListAsync();
                        var dbCategories = await _context.Categories.ToListAsync();
                        string validCategoryId = GetValidCategoryId(dbCategories, importType);

                        decimal totalAmount = items.Sum(i => i.Quantity_received * (i.Price ?? 0));

                        var receiveHeader = new Receive
                        {
                            Receive_id = generatedReceiveId,
                            Receive_date = DateTime.Now,
                            Invoice_number = invoiceNo,
                            Received_by = receivedBy,
                            Status = "Completed",
                            Total_amount = totalAmount
                        };

                        await _context.Receives.AddAsync(receiveHeader);

                        string prefix = (importType == "MED") ? "MED" : "SUP";
                        int currentMaxSeq = GetMaxSequenceNumber(dbMedicines, prefix);
                        int detailSeq = 1;

                        foreach (var item in items)
                        {
                            string finalMedicineId = "";
                            string medName = item.Medicine_name ?? "";

                            if (item.ActionType == "UpdateExisting" && !string.IsNullOrEmpty(item.MatchedMedicineId))
                            {
                                var targetMed = dbMedicines.FirstOrDefault(m => m.Medicine_id == item.MatchedMedicineId);
                                if (targetMed != null)
                                {
                                    // ✅ แก้ไข: เพิ่มเฉพาะจำนวนที่รับเข้าใน Stock และไม่แตะต้อง Quantity (เกณฑ์ขั้นต่ำ)
                                    targetMed.Stock = (targetMed.Stock ?? 0) + item.Quantity_received;
                                    targetMed.Price = item.Price ?? targetMed.Price;
                                    targetMed.Lot = item.Lot_number;
                                    targetMed.Expired_at = item.Expiry_date;
                                    targetMed.Medicine_name = medName;
                                    targetMed.Packing_Size = item.Packing_Size;
                                    targetMed.Account_Type = item.Account_Type;
                                    targetMed.Status = "Active";

                                    _context.Medicines.Update(targetMed);
                                    finalMedicineId = targetMed.Medicine_id;
                                }
                            }
                            else
                            {
                                currentMaxSeq++;
                                finalMedicineId = $"{prefix}{currentMaxSeq:D3}";

                                // ✅ แก้ไข: กำหนด minQuantity แยกต่างหาก ( default = 10 หรือปรับเปลี่ยนได้ตามต้องการ)
                                var newMed = CreateNewMedicineObject(
                                    finalMedicineId, medName, item.Quantity_received,
                                    item.Price ?? 0, item.Lot_number, item.Expiry_date,
                                    item.Packing_Size, item.Account_Type, validCategoryId,
                                    minQuantity: 10
                                );

                                await _context.Medicines.AddAsync(newMed);
                                dbMedicines.Add(newMed);
                            }

                            // บันทึก ReceiveDetail พร้อม SheetName
                            var detail = new ReceiveDetail
                            {
                                Receive_detail_id = $"{generatedReceiveId}-{detailSeq++:D3}",
                                Receive_id = generatedReceiveId,
                                Medicine_id = finalMedicineId,
                                Quantity_received = item.Quantity_received,
                                Lot_number = item.Lot_number,
                                Expiry_date = item.Expiry_date,
                                SheetName = item.SheetName
                            };

                            await _context.ReceiveDetails.AddAsync(detail);
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                TempData["SuccessMessage"] = $"✅ บันทึกรับเข้าคลังสำเร็จ เลขที่เอกสาร: {generatedReceiveId}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ บันทึกไม่สำเร็จ: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction("Index");
            }
            finally
            {
                // ลบไฟล์ชั่วคราว
                if (!string.IsNullOrEmpty(tempFilePath) && System.IO.File.Exists(tempFilePath))
                {
                    try { System.IO.File.Delete(tempFilePath); } catch { }
                }
            }
        }

        // =====================================================================
        // 3. แสดงรายละเอียดใบรับเข้าคลัง (Details)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var receive = await _context.Receives
                .FirstOrDefaultAsync(r => r.Receive_id == id);

            if (receive == null)
                return NotFound();

            var items = await _context.ReceiveDetails
                .Include(rd => rd.Medicines)
                .Where(rd => rd.Receive_id == id)
                .ToListAsync();

            ViewBag.Items = items;
            return View(receive);
        }

        // =====================================================================
        // Helper Methods
        // =====================================================================

        private string GetValidCategoryId(List<Category> categories, string importType)
        {
            if (categories == null || !categories.Any())
                return importType;

            var matchExact = categories.FirstOrDefault(c =>
                c.Category_id.Equals(importType, StringComparison.OrdinalIgnoreCase));
            if (matchExact != null) return matchExact.Category_id;

            var matchName = categories.FirstOrDefault(c =>
                importType == "MED"
                    ? (c.Category_name != null && c.Category_name.Contains("ยา"))
                    : (c.Category_name != null && c.Category_name.Contains("เวชภัณฑ์")));
            if (matchName != null) return matchName.Category_id;

            return categories.First().Category_id ?? importType;
        }

        private int GetMaxSequenceNumber(List<Medicines> medicines, string prefix)
        {
            return medicines
                .Where(m => !string.IsNullOrEmpty(m.Medicine_id) && m.Medicine_id.StartsWith(prefix))
                .Select(m =>
                {
                    string numPart = m.Medicine_id.Substring(prefix.Length);
                    return int.TryParse(numPart, out int n) ? n : 0;
                })
                .DefaultIfEmpty(0)
                .Max();
        }

        // ✅ แก้ไข: เพิ่มพารามิเตอร์ minQuantity เพื่อกำหนดเกณฑ์เตือนสต็อกต่ำสุด แยกกับ Stock
        private Medicines CreateNewMedicineObject(
            string medId, string medName, int stockQuantity, decimal price,
            string lotNumber, DateTime expiryDate, string packingSize,
            string accountType, string categoryId, int minQuantity = 10)
        {
            return new Medicines
            {
                Medicine_id = medId,
                Medicine_name = medName,
                Stock = stockQuantity,    // สต็อกจริง ณ ปัจจุบัน
                Quantity = minQuantity,   // เกณฑ์สต็อกต่ำสุดที่ต้องการตั้งไว้
                Price = price,
                Lot = lotNumber,
                Expired_at = expiryDate,
                Packing_Size = packingSize,
                Account_Type = accountType,
                Category_id = categoryId,
                Status = "Active"
            };
        }
    }

    // =====================================================================
    // Model / DTO สำหรับรับค่าการ Preview และ Confirm
    // =====================================================================
    public class ReceiveDetailModel
    {
        public string? Medicine_id { get; set; }
        public string? Medicine_name { get; set; }
        public int Quantity_received { get; set; }
        public decimal? Price { get; set; }
        public string? Lot_number { get; set; }
        public DateTime Expiry_date { get; set; } = DateTime.Now;
        public string? Packing_Size { get; set; }
        public string? Account_Type { get; set; }

        public string? SheetName { get; set; }

        public string ActionType { get; set; } = "AddNew"; // AddNew, UpdateExisting, DisableOldAndAddNew
        public string? MatchedMedicineId { get; set; }
        public string? MatchedMedicineName { get; set; }
        public bool IsDuplicateOrSimilar { get; set; }
    }
}