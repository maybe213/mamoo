using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly CsvParserService _csvParser;

        // แก้ไข: ใช้ Dependency Injection สำหรับ Services ทั้งหมด
        public ReceiveController(DrugInventoryContext context, ExcelParserService excelParser, CsvParserService csvParser)
        {
            _context = context;
            _excelParser = excelParser;
            _csvParser = csvParser;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var receives = await _context.Receives
                .OrderByDescending(r => r.Receive_date)
                .ToListAsync();

            ViewBag.UserList = await _context.Users.ToListAsync();
            return View(receives);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(IFormFile excelFile, string invoiceNo, string receivedBy, string importType)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "กรุณาเลือกไฟล์ CSV ที่ต้องการอัปโหลด";
                return RedirectToAction("Index");
            }

            if (!excelFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "ระบบรองรับเฉพาะไฟล์นามสกุล .csv เท่านั้น";
                return RedirectToAction("Index");
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{excelFile.FileName}");

            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await excelFile.CopyToAsync(stream);
                }

                string detectedDocumentTitle;
                // แก้ไข: เรียกใช้ parser ผ่าน _csvParser ที่ถูก Inject เข้ามา
                var previewList = _csvParser.ParseCsvFile(tempFilePath, importType, out detectedDocumentTitle);

                if (previewList == null || !previewList.Any())
                {
                    TempData["ErrorMessage"] = "ไฟล์ CSV ว่างเปล่า หรือไม่มีข้อมูลรายการยาที่ถูกต้อง";
                    return RedirectToAction("Index");
                }

                var existingMedicines = await _context.Medicines.AsNoTracking().ToListAsync();
                _excelParser.DetectDuplicates(previewList, existingMedicines);

                string prefix = (importType?.ToUpper() == "SUP") ? "SUP" : "MED";
                int currentMaxId = await GetCurrentMaxNumberAsync(prefix);

                foreach (var item in previewList)
                {
                    if (string.IsNullOrEmpty(item.MatchedMedicineId) || item.MatchedMedicineId.StartsWith($"{prefix}-TEMP") || item.MatchedMedicineId.StartsWith("MED-TEMP"))
                    {
                        currentMaxId++;
                        item.MatchedMedicineId = $"{prefix}{currentMaxId:D3}";
                    }
                }

                ViewBag.InvoiceNo = invoiceNo;
                ViewBag.ReceivedBy = receivedBy;
                ViewBag.ImportType = importType;
                ViewBag.DocumentTitle = detectedDocumentTitle;
                // นำ ViewBag.TempFilePath ออก เพราะไฟล์จะถูกลบทันทีด้านล่าง

                return View("Preview", previewList);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"การตรวจสอบไฟล์ล้มเหลว: {ex.Message}";
                return RedirectToAction("Index");
            }
            finally
            {
                // แก้ไข: ลบไฟล์ชั่วคราวทิ้งทันทีที่อ่านเสร็จ เพื่อป้องกันปัญหาด้าน Security และประหยัดพื้นที่ดิสก์
                if (System.IO.File.Exists(tempFilePath))
                {
                    try { System.IO.File.Delete(tempFilePath); } catch { }
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = 5000)]
        // แก้ไข: นำตัวแปร string tempFilePath ออกจากพารามิเตอร์ เนื่องจากไม่จำเป็นต้องใช้อีกต่อไป
        public async Task<IActionResult> ConfirmReceive(
            string invoiceNo,
            string receivedBy,
            string importType,
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
                        // ✅ เพิ่ม .AsNoTracking() - ป้องกันไม่ให้ EF track ยาทุกตัวในระบบไว้ตั้งแต่ต้น
                        //    (สาเหตุหลักของ error "already being tracked")
                        var dbMedicines = await _context.Medicines.AsNoTracking().ToListAsync();
                        var dbCategories = await _context.Categories.AsNoTracking().ToListAsync();
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
                        int currentMaxSeq = await GetCurrentMaxNumberAsync(prefix);
                        int detailSeq = 1;

                        foreach (var item in items)
                        {
                            string finalMedicineId = "";
                            string medName = item.Medicine_name ?? "";

                            bool isUpdateExisting = (item.ActionType == "UpdateExisting" || item.ActionType == "UPDATE");

                            // ตรวจสอบรายการที่ Preview จับคู่ไว้ (ถ้ามี)
                            Medicines? matchedCandidate = null;
                            if (!string.IsNullOrEmpty(item.MatchedMedicineId))
                            {
                                matchedCandidate = dbMedicines.FirstOrDefault(m => m.Medicine_id == item.MatchedMedicineId);
                            }

                            // ✅ อัปเดตแถวเดิมได้ก็ต่อเมื่อ: ผู้ใช้เลือกให้อัปเดต + Lot ตรงกัน + วันหมดอายุตรงกัน (เป๊ะ)
                            //    ถ้า Lot หรือวันหมดอายุไม่ตรง ถือว่าเป็น "ล็อตใหม่" ต้องสร้างรายการแยกเสมอ
                            bool sameBatch = matchedCandidate != null &&
                                             (matchedCandidate.Lot?.Trim() ?? "") == (item.Lot_number?.Trim() ?? "") &&
                                             matchedCandidate.Expired_at == item.Expiry_date;

                            Medicines? targetMed = (isUpdateExisting && sameBatch) ? matchedCandidate : null;

                            if (targetMed != null)
                            {
                                // ล็อต + วันหมดอายุ ตรงกันเป๊ะ -> บวกจำนวนเพิ่มเข้าแถวเดิม
                                targetMed.Stock = (targetMed.Stock ?? 0) + item.Quantity_received;
                                targetMed.Price = item.Price ?? targetMed.Price;
                                targetMed.Medicine_name = medName;
                                targetMed.Packing_Size = item.Packing_Size;
                                targetMed.Account_Type = item.Account_Type;
                                targetMed.Status = "Active";
                                // หมายเหตุ: ไม่แก้ Lot/Expired_at ซ้ำ เพราะ sameBatch ยืนยันแล้วว่าตรงกับของเดิมอยู่แล้ว

                                // ✅ ใช้ Attach + Entry.State แทน Update() เพราะ targetMed มาจาก AsNoTracking
                                //    ถ้าเคย Attach ไปแล้วในลูปก่อนหน้า (แถวซ้ำ) ให้ตรวจสอบสถานะก่อน
                                var entry = _context.Entry(targetMed);
                                if (entry.State == EntityState.Detached)
                                {
                                    _context.Medicines.Attach(targetMed);
                                    entry.State = EntityState.Modified;
                                }
                                finalMedicineId = targetMed.Medicine_id;
                            }
                            else
                            {
                                // ไม่มีล็อตเดิมให้ merge ได้ (ยาใหม่ทั้งหมด หรือ ชื่อ/รหัสตรงแต่คนละล็อต) -> สร้างรายการใหม่เสมอ
                                if (matchedCandidate == null &&
                                    !string.IsNullOrEmpty(item.MatchedMedicineId) &&
                                    item.MatchedMedicineId.StartsWith(prefix))
                                {
                                    // กรณีนี้คือรหัสที่ Preview จองไว้ล่วงหน้าสำหรับ "ของใหม่จริงๆ" (ยังไม่มีในระบบ) -> ใช้รหัสนี้ได้เลย
                                    finalMedicineId = item.MatchedMedicineId;
                                    if (finalMedicineId.Length > prefix.Length)
                                    {
                                        string numPart = finalMedicineId.Substring(prefix.Length).TrimStart('-', '_');
                                        if (int.TryParse(numPart, out int num) && num > currentMaxSeq)
                                        {
                                            currentMaxSeq = num;
                                        }
                                    }
                                }
                                else
                                {
                                    // ✅ สำคัญ: กรณีชื่อ/รหัสตรงกับของเดิม (matchedCandidate != null) แต่คนละล็อต
                                    //    ห้ามใช้ item.MatchedMedicineId ซ้ำ (จะชนกับของเดิม) ต้องสร้างรหัสใหม่เสมอ
                                    currentMaxSeq++;
                                    finalMedicineId = $"{prefix}{currentMaxSeq:D3}";
                                }

                                var newMed = CreateNewMedicineObject(
                                    finalMedicineId, medName, item.Quantity_received,
                                    item.Price ?? 0, item.Lot_number, item.Expiry_date,
                                    item.Packing_Size, item.Account_Type, validCategoryId,
                                    minQuantity: 10
                                );

                                await _context.Medicines.AddAsync(newMed);
                                // ✅ สำคัญ: เพิ่มเข้า dbMedicines ทันที เพื่อให้แถวถัดไปในลูปเดียวกัน
                                //    ที่อาจมีรหัสซ้ำ (เช่น ชื่อยาเดียวกัน 2 แถวในไฟล์ CSV) ตรวจเจอและอัปเดตแทนที่จะ Add ซ้ำ
                                dbMedicines.Add(newMed);
                            }

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
            // แก้ไข: ลบบล็อก finally ที่คอยลบไฟล์ออกจากเมธอด ConfirmReceive แล้ว
        }

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

        private Medicines CreateNewMedicineObject(
            string medId, string medName, int stockQuantity, decimal price,
            string lotNumber, DateTime expiryDate, string packingSize,
            string accountType, string categoryId, int minQuantity = 10)
        {
            return new Medicines
            {
                Medicine_id = medId,
                Medicine_name = medName,
                Stock = stockQuantity,
                Quantity = minQuantity,
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
}