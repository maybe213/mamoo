using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DrugInventoryPro.Models;

namespace DrugInventoryPro.Services
{
    /// <summary>
    /// บริการสำหรับ Parse ไฟล์ Excel (เวชภัณฑ์ไม่ใช่ยา SUP และ ยา MED)
    /// </summary>
    public class ExcelParserService
    {
        /// <summary>
        /// Parse ไฟล์ Excel และคืนค่ารายการทั้งหมดตามประเภทการนำเข้า (importType: "SUP" หรือ "MED")
        /// </summary>
        public List<ReceiveDetail> ParseExcelFile(string filePath, string importType)
        {
            var resultList = new List<ReceiveDetail>();

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    if (worksheet.RangeUsed() == null) continue;

                    List<ReceiveDetail> parsedItems;

                    if (string.Equals(importType, "SUP", StringComparison.OrdinalIgnoreCase))
                    {
                        parsedItems = ParseSuppliesSheet(worksheet);
                    }
                    else
                    {
                        parsedItems = ParseMedicinesSheet(worksheet);
                    }

                    resultList.AddRange(parsedItems);
                }
            }

            return resultList;
        }

        /// <summary>
        /// ตรวจสอบรายการซ้ำหรือใกล้เคียงในระบบ
        /// </summary>
        public void DetectDuplicates(List<ReceiveDetail> rawList, List<Medicines> existingMedicines)
        {
            foreach (var item in rawList)
            {
                if (string.IsNullOrWhiteSpace(item.Medicine_name)) continue;

                var trimmedName = item.Medicine_name.Trim();

                // 1. ตรวจสอบแบบตรงกันเป๊ะ (Exact Match)
                var exactMatch = existingMedicines.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.Medicine_name) &&
                    m.Medicine_name.Trim().Equals(trimmedName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    item.IsDuplicateOrSimilar = true;
                    item.MatchedMedicineId = exactMatch.Medicine_id;
                    item.MatchedMedicineName = exactMatch.Medicine_name;
                    item.ActionType = "UpdateExisting";
                    continue;
                }

                // 2. ตรวจสอบความคล้ายคลึง (Fuzzy Match >= 85%)
                var similarMatch = existingMedicines
                    .Select(m => new { Medicine = m, Similarity = CalculateSimilarity(trimmedName, m.Medicine_name ?? "") })
                    .Where(x => x.Similarity >= 0.85)
                    .OrderByDescending(x => x.Similarity)
                    .FirstOrDefault();

                if (similarMatch != null)
                {
                    item.IsDuplicateOrSimilar = true;
                    item.MatchedMedicineId = similarMatch.Medicine.Medicine_id;
                    item.MatchedMedicineName = similarMatch.Medicine.Medicine_name;
                    item.ActionType = "UpdateExisting";
                }
            }
        }

        /// <summary>
        /// Parse ข้อมูลเวชภัณฑ์มิใช่ยา (SUP)
        /// </summary>
        private List<ReceiveDetail> ParseSuppliesSheet(IXLWorksheet worksheet)
        {
            var items = new List<ReceiveDetail>();
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return items;

            int lastRow = usedRange.LastRow().RowNumber();
            string sheetName = worksheet.Name;

            // ค้นหาบรรทัด Header โดยอัตโนมัติ
            int dataStartRow = FindHeaderRow(worksheet, lastRow) + 1;
            if (dataStartRow <= 1) dataStartRow = 7; // ค่าสำรองกรณีหาไม่พบ

            for (int row = dataStartRow; row <= lastRow; row++)
            {
                try
                {
                    string itemName = SafeGetCellValue(worksheet.Cell(row, 2));

                    if (string.IsNullOrWhiteSpace(itemName) || itemName.Contains("เวชภัณฑ์อื่นๆ") || itemName.Contains("รายการ"))
                        continue;

                    string packSize = SafeGetCellValue(worksheet.Cell(row, 3)); // Col C: ขนาด / คุณลักษณะ
                    string qtyStr = SafeGetCellValue(worksheet.Cell(row, 4));   // Col D: จำนวนขอเบิก
                    int qty = ParseQuantity(qtyStr);

                    string remark = SafeGetCellValue(worksheet.Cell(row, 6));   // Col F: หมายเหตุ

                    var detail = CreateReceiveDetail(
                        itemName: itemName,
                        quantity: qty,
                        packSize: packSize,
                        account_Type: "เวชภัณฑ์",
                        importType: "SUP",
                        sheetName: sheetName,
                        remark: remark
                    );

                    items.Add(detail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing SUP Row {row}: {ex.Message}");
                }
            }

            return items;
        }

        /// <summary>
        /// Parse ข้อมูลยา (MED)
        /// </summary>
        private List<ReceiveDetail> ParseMedicinesSheet(IXLWorksheet worksheet)
        {
            var items = new List<ReceiveDetail>();
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return items;

            int lastRow = usedRange.LastRow().RowNumber();
            string sheetName = worksheet.Name;

            // ค้นหาบรรทัด Header โดยอัตโนมัติ
            int dataStartRow = FindHeaderRow(worksheet, lastRow) + 1;
            if (dataStartRow <= 1) dataStartRow = 8; // ค่าสำรองกรณีหาไม่พบ

            for (int row = dataStartRow; row <= lastRow; row++)
            {
                try
                {
                    // Col B (2): รายการยา
                    string itemName = SafeGetCellValue(worksheet.Cell(row, 2));
                    if (string.IsNullOrWhiteSpace(itemName) || itemName.Contains("รายการยา")) continue;

                    // Col C (3): ความแรง / ปริมาตร
                    string strength = SafeGetCellValue(worksheet.Cell(row, 3));

                    // Col D (4): ขนาดบรรจุ
                    string rawPackSize = SafeGetCellValue(worksheet.Cell(row, 4));

                    // ผสมความแรงและขนาดบรรจุเข้าด้วยกัน
                    string combinedPackSize = !string.IsNullOrEmpty(strength) && !string.IsNullOrEmpty(rawPackSize)
                        ? $"{strength} ({rawPackSize})"
                        : (!string.IsNullOrEmpty(strength) ? strength : rawPackSize);

                    // Col E (5): ประเภทบัญชียาหลักแห่งชาติ (ED / NED)
                    string accountType = SafeGetCellValue(worksheet.Cell(row, 5));
                    if (string.IsNullOrWhiteSpace(accountType)) accountType = "ED";

                    // Col H (8): จำนวนขอเบิก
                    string qtyStr = SafeGetCellValue(worksheet.Cell(row, 8));
                    int qty = ParseQuantity(qtyStr);

                    // Col I (9): หมายเหตุ
                    string remark = SafeGetCellValue(worksheet.Cell(row, 9));

                    var detail = CreateReceiveDetail(
                        itemName: itemName,
                        quantity: qty,
                        packSize: combinedPackSize,
                        account_Type: accountType,
                        importType: "MED",
                        sheetName: sheetName,
                        unitId: strength,
                        remark: remark
                    );

                    items.Add(detail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing MED Row {row}: {ex.Message}");
                }
            }

            return items;
        }

        /// <summary>
        /// ค้นหาบรรทัดที่เป็น Header ของตารางข้อมูล
        /// </summary>
        private int FindHeaderRow(IXLWorksheet worksheet, int lastRow)
        {
            for (int r = 1; r <= Math.Min(15, lastRow); r++)
            {
                string c2 = SafeGetCellValue(worksheet.Cell(r, 2));
                string c1 = SafeGetCellValue(worksheet.Cell(r, 1));
                if (c2.Contains("รายการ") || c2.Contains("ชื่อยา") || c1.Contains("รายการ") || c1.Contains("ที่"))
                {
                    return r;
                }
            }
            return 0;
        }

        /// <summary>
        /// ดึงค่าจาก Cell อย่างปลอดภัย รองรับข้อความ ตัวเลข และสูตร
        /// </summary>
        private string SafeGetCellValue(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty())
                return string.Empty;

            try
            {
                return cell.GetFormattedString().Trim();
            }
            catch
            {
                return cell.Value.ToString()?.Trim() ?? string.Empty;
            }
        }

        /// <summary>
        /// Parse จำนวนจากข้อความ เช่น "2*25", "2x25", "100", "100อัน", "-" ให้เป็นตัวเลข
        /// </summary>
        private int ParseQuantity(string qtyStr)
        {
            if (string.IsNullOrWhiteSpace(qtyStr))
                return 0;

            qtyStr = qtyStr.Trim();

            // ลบชื่อหน่วยนับออกจากข้อความ
            string[] units = { "อัน", "ชิ้น", "ขวด", "กล่อง", "โหล", "ม้วน", "ชั้น", "ซอง", "แผง", "หลอด", "กระปุก", "คู่", "cap", "tab", "amp", "vial" };
            foreach (var unit in units)
            {
                qtyStr = Regex.Replace(qtyStr, unit, "", RegexOptions.IgnoreCase);
            }

            qtyStr = qtyStr.Replace(",", "").Trim();

            // กรณีสูตรคูณ เช่น "2*25" หรือ "2x25"
            if (qtyStr.Contains('*') || qtyStr.Contains('x') || qtyStr.Contains('X'))
            {
                var parts = qtyStr.Split(new[] { '*', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var match1 = Regex.Match(parts[0].Trim(), @"\d+");
                    var match2 = Regex.Match(parts[1].Trim(), @"\d+");

                    if (match1.Success && match2.Success)
                    {
                        if (int.TryParse(match1.Value, out int p1) && int.TryParse(match2.Value, out int p2))
                        {
                            return p1 * p2;
                        }
                    }
                }
            }

            var numberMatch = Regex.Match(qtyStr, @"\d+");
            if (numberMatch.Success && int.TryParse(numberMatch.Value, out int parsedQty))
            {
                return parsedQty;
            }

            return 0;
        }

        /// <summary>
        /// Parse ราคาจากข้อความ หรือเซลล์ตัวเลขแบบปลอดภัย
        /// </summary>
        private decimal ParseDecimalSafe(string priceStr)
        {
            if (string.IsNullOrWhiteSpace(priceStr))
                return 0m;

            priceStr = priceStr.Replace(",", "").Trim();

            if (decimal.TryParse(priceStr, out decimal parsedPrice))
                return parsedPrice;

            var match = Regex.Match(priceStr, @"\d+(\.\d+)?");
            if (match.Success && decimal.TryParse(match.Value, out decimal extractedPrice))
            {
                return extractedPrice;
            }

            return 0m;
        }

        /// <summary>
        /// สร้าง ReceiveDetail object พร้อมตั้งค่าสำหรับ NotMapped Properties
        /// </summary>
        private ReceiveDetail CreateReceiveDetail(
            string itemName,
            int quantity,
            string packSize,
            string account_Type,
            string importType,
            string sheetName,
            string? unitId = null,
            string? remark = null)
        {
            string prefix = (importType == "MED") ? "MED" : "SUP";

            return new ReceiveDetail
            {
                Receive_detail_id = Guid.NewGuid().ToString("N"),
                Medicine_id = $"{prefix}-TEMP-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Medicine_name = itemName,
                Quantity_received = quantity,
                Price = 0m,
                Lot_number = $"LOT-{DateTime.Now:yyyyMMdd}",
                Expiry_date = DateTime.Now.AddYears(2),
                Packing_Size = packSize,
                Account_Type = account_Type,
                Unit_id = unitId,
                Remark = remark,
                SheetName = sheetName,
                IsDuplicateOrSimilar = false,
                MatchedMedicineId = null,
                MatchedMedicineName = null,
                ActionType = "AddNew"
            };
        }

        /// <summary>
        /// คำนวณความคล้ายระหว่าง 2 ข้อความ (0.0 - 1.0)
        /// </summary>
        private double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            source = source.Trim().ToLower().Replace(" ", "");
            target = target.Trim().ToLower().Replace(" ", "");

            if (source == target)
                return 1.0;

            int distance = LevenshteinDistance(source, target);
            return 1.0 - ((double)distance / Math.Max(source.Length, target.Length));
        }

        /// <summary>
        /// คำนวณระยะห่าง Levenshtein ระหว่าง 2 ข้อความ
        /// </summary>
        private int LevenshteinDistance(string source, string target)
        {
            int[,] distance = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; distance[i, 0] = i++) { }
            for (int j = 0; j <= target.Length; distance[0, j] = j++) { }

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost
                    );
                }
            }

            return distance[source.Length, target.Length];
        }
    }
}