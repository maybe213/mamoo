using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DrugInventoryPro.Models;

namespace DrugInventoryPro.Services
{
    /// <summary>
    /// บริการสำหรับ Parse ไฟล์ Excel 3 sheets (เวชภัณฑ์ไม่ใช่ยา, ยา, ยาเพิ่มเติม)
    /// </summary>
    public class ExcelParserService
    {
        /// <summary>
        /// Parse ไฟล์ Excel และคืนค่ารายการทั้งหมด
        /// </summary>
        public List<ReceiveDetail> ParseExcelFile(string filePath, string importType)
        {
            var result = new List<ReceiveDetail>();

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    string sheetName = worksheet.Name;
                    var usedRange = worksheet.RangeUsed();
                    if (usedRange == null) continue;

                    var rows = usedRange.RowsUsed();

                    bool isFirstRow = true;
                    foreach (var row in rows)
                    {
                        if (isFirstRow)
                        {
                            isFirstRow = false;
                            continue;
                        }

                        // Col 1: ลำดับ (ข้ามไป)
                        // Col 2: ชื่อยา
                        string medName = SafeGetCellValue(row.Cell(2));
                        if (string.IsNullOrEmpty(medName)) continue;

                        // Col 3: จำนวน (ใช้ ParseQuantity แบบ ปลอดภัย)
                        int qty = ParseQuantity(SafeGetCellValue(row.Cell(3)));

                        // Col 4: ราคา (แปลงราคาสดแบบปลอดภัย)
                        decimal price = ParseDecimalSafe(SafeGetCellValue(row.Cell(4)));

                        // Col 5: Lot
                        string lot = SafeGetCellValue(row.Cell(5));

                        // Col 6: Expiry Date
                        DateTime expiry = DateTime.Now.AddYears(2);
                        var expiryCell = row.Cell(6);
                        if (!expiryCell.IsEmpty())
                        {
                            if (expiryCell.DataType == XLDataType.DateTime)
                            {
                                expiry = expiryCell.GetDateTime();
                            }
                            else if (DateTime.TryParse(SafeGetCellValue(expiryCell), out DateTime parsedDate))
                            {
                                expiry = parsedDate;
                            }
                        }

                        // Col 7: Packing Size, Col 8: Account Type
                        string packingSize = SafeGetCellValue(row.Cell(7));
                        string accountType = SafeGetCellValue(row.Cell(8));

                        result.Add(new ReceiveDetail
                        {
                            Medicine_name = medName,
                            Quantity_received = qty,
                            Price = price,
                            Lot_number = lot,
                            Expiry_date = expiry,
                            Packing_Size = packingSize,
                            Account_Type = accountType,
                            SheetName = sheetName
                        });
                    }
                }
            }

            return result;
        }

        public void DetectDuplicates(List<ReceiveDetail> rawList, List<Medicines> existingMedicines)
        {
            foreach (var item in rawList)
            {
                var match = existingMedicines.FirstOrDefault(m =>
                    m.Medicine_name != null &&
                    m.Medicine_name.Equals(item.Medicine_name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    item.IsDuplicateOrSimilar = true;
                    item.MatchedMedicineId = match.Medicine_id;
                    item.MatchedMedicineName = match.Medicine_name;
                    item.ActionType = "UpdateExisting";
                }
            }
        }

        /// <summary>
        /// Parse Sheet 1: เวชภัณฑ์ไม่ใช่ยา (เริ่มจาก Row 7)
        /// Column: A=ลำดับ, B=ชื่อ, C=ขนาด, D=ประเภท, E=จำนวนเบิก
        /// </summary>
        private List<ReceiveDetail> ParseSuppliesSheet(IXLWorksheet worksheet)
        {
            var items = new List<ReceiveDetail>();
            int dataStartRow = 7;

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return items;

            int lastRow = usedRange.LastRow().RowNumber();

            for (int row = dataStartRow; row <= lastRow; row++)
            {
                try
                {
                    // Column A: ลำดับที่ (ต้องเป็นตัวเลข)
                    string colA = SafeGetCellValue(worksheet.Cell(row, 1));

                    if (string.IsNullOrEmpty(colA) || !int.TryParse(colA, out _))
                        continue;

                    // Column B: ชื่อรายการ
                    string itemName = SafeGetCellValue(worksheet.Cell(row, 2));

                    if (string.IsNullOrWhiteSpace(itemName) || itemName.Contains("เวชภัณฑ์อื่นๆ"))
                        continue;

                    // Column C: ขนาด/บรรจุ
                    string packSize = SafeGetCellValue(worksheet.Cell(row, 3));

                    // Column D: ประเภท (ED/NED)
                    string accountType = SafeGetCellValue(worksheet.Cell(row, 4));
                    if (string.IsNullOrWhiteSpace(accountType))
                        accountType = "SUP";

                    // Column E: จำนวนเบิก/ขอ
                    string qtyStr = SafeGetCellValue(worksheet.Cell(row, 5));
                    int qty = ParseQuantity(qtyStr);

                    var detail = CreateReceiveDetail(
                        itemName: itemName,
                        quantity: qty,
                        packSize: packSize,
                        accountType: accountType,
                        importType: "SUP"
                    );

                    items.Add(detail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing Supplies Sheet row {row}: {ex.Message}");
                    continue;
                }
            }

            return items;
        }

        /// <summary>
        /// Parse Sheet 2 & 3: ยา (เริ่มจาก Row 7)
        /// Column: A=ลำดับ, B=ชื่อ, C=ขนาด, D=ประเภท, E=จำนวนขอ
        /// </summary>
        private List<ReceiveDetail> ParseMedicinesSheet(IXLWorksheet worksheet)
        {
            var items = new List<ReceiveDetail>();
            int dataStartRow = 7;

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return items;

            int lastRow = usedRange.LastRow().RowNumber();

            for (int row = dataStartRow; row <= lastRow; row++)
            {
                try
                {
                    // Column A: ลำดับที่ (ต้องเป็นตัวเลข)
                    string colA = SafeGetCellValue(worksheet.Cell(row, 1));

                    if (string.IsNullOrEmpty(colA) || !int.TryParse(colA, out _))
                        continue;

                    // Column B: ชื่อรายการ/ยา
                    string itemName = SafeGetCellValue(worksheet.Cell(row, 2));

                    if (string.IsNullOrWhiteSpace(itemName))
                        continue;

                    // Column C: ขนาด/บรรจุ
                    string packSize = SafeGetCellValue(worksheet.Cell(row, 3));

                    // Column D: ประเภท (ED/NED)
                    string accountType = SafeGetCellValue(worksheet.Cell(row, 4));
                    if (string.IsNullOrWhiteSpace(accountType))
                        accountType = "ED";

                    // Column E: จำนวนขอเบิก (ถ้าว่าง ให้ลองดู Column F, G, H)
                    string qtyStr = SafeGetCellValue(worksheet.Cell(row, 5));
                    if (string.IsNullOrWhiteSpace(qtyStr)) qtyStr = SafeGetCellValue(worksheet.Cell(row, 6));
                    if (string.IsNullOrWhiteSpace(qtyStr)) qtyStr = SafeGetCellValue(worksheet.Cell(row, 7));
                    if (string.IsNullOrWhiteSpace(qtyStr)) qtyStr = SafeGetCellValue(worksheet.Cell(row, 8));

                    int qty = ParseQuantity(qtyStr);
                    if (qty <= 0) continue;

                    var detail = CreateReceiveDetail(
                        itemName: itemName,
                        quantity: qty,
                        packSize: packSize,
                        accountType: accountType,
                        importType: "MED"
                    );

                    items.Add(detail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing Medicines Sheet row {row}: {ex.Message}");
                    continue;
                }
            }

            return items;
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
        /// Parse จำนวนจากข้อความ เช่น "2*25", "100", "100อัน", "-" ให้เป็นตัวเลข
        /// </summary>
        private int ParseQuantity(string qtyStr)
        {
            if (string.IsNullOrWhiteSpace(qtyStr))
                return 0;

            qtyStr = qtyStr.Trim()
                .Replace(",", "")
                .Replace("อัน", "")
                .Replace("ชิ้น", "")
                .Replace("ขวด", "")
                .Replace("กล่อง", "")
                .Replace("โหล", "")
                .Replace("ม้วน", "")
                .Replace("ชั้น", "");

            // กรณีเช่น "2*25" => 50
            if (qtyStr.Contains('*'))
            {
                var parts = qtyStr.Split('*');
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

            // ค้นหาตัวเลขแรกในข้อความ
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
        /// สร้าง ReceiveDetail object พร้อมข้อมูลสำหรับแสดงใน View
        /// </summary>
        private ReceiveDetail CreateReceiveDetail(
            string itemName,
            int quantity,
            string packSize,
            string accountType,
            string importType)
        {
            string prefix = (importType == "MED") ? "MED" : "SUP";

            return new ReceiveDetail
            {
                Medicine_id = $"{prefix}-TEMP-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Quantity_received = quantity,
                Lot_number = $"LOT-{DateTime.Now:yyyyMMdd}",
                Expiry_date = DateTime.Now.AddYears(2),

                IsDuplicateOrSimilar = false,
                MatchedMedicineId = null,
                MatchedMedicineName = null,
                ActionType = "AddNew",

                Medicines = new Medicines
                {
                    Medicine_id = $"{prefix}-TEMP",
                    Medicine_name = itemName,
                    Price = 0,
                    Packing_Size = packSize,
                    Account_Type = accountType,
                    Category_id = importType
                }
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