using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DrugInventoryPro.Models;

namespace DrugInventoryPro.Services
{
    public class CsvParserService
    {
        // รายการหัวเอกสารที่อนุญาตสำหรับ "รับยาเข้าคลัง (MED)"
        private readonly List<string> _allowedMedTitles = new List<string>
        {
            "ใบเบิกเวชภัณฑ์ยา (ยาทั่วไป) รพ.สต.",
            "ใบเบิกเวชภัณฑ์ยา (ยาสมุนไพร)",
            "ใบเบิกเวชภัณฑ์ยา (คลินิกโรคเรื้อรัง)",
            "ใบเบิกเวชภัณฑ์ยาเพิ่มเติม เฉพาะหน่วยบริการที่ขึ้นทะเบียน PCC"
        };

        // รายการหัวเอกสารที่อนุญาตสำหรับ "รับเวชภัณฑ์เข้าคลัง (SUP)"
        private readonly List<string> _allowedSupTitles = new List<string>
        {
            "ใบเบิกเวชภัณฑ์มิใช่ยา สำหรับใช้ในหน่วยบริการ รพ.สต.ท่าทองใหม่",
            "ใบเบิกเวชภัณฑ์มิใช่ยา"
        };

        public List<ReceiveDetail> ParseCsvFile(string filePath, string importType, out string detectedTitle)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding encoding = DetectEncoding(filePath);
            var lines = File.ReadAllLines(filePath, encoding);

            if (lines.Length == 0)
                throw new Exception("ไฟล์ CSV ว่างเปล่า");

            // อ่านหัวเอกสารบรรทัดแรก และตัดเครื่องหมาย " และ , ออก
            string rawTitle = lines[0].Replace("\"", "").Trim().TrimEnd(',', ' ');
            detectedTitle = rawTitle;

            // เลือกชุดหัวเอกสารตามประเภทการรับเข้า (MED / SUP)
            bool isMed = string.Equals(importType, "MED", StringComparison.OrdinalIgnoreCase);
            List<string> allowedTitles = isMed ? _allowedMedTitles : _allowedSupTitles;

            string matchedTitle = allowedTitles
                .FirstOrDefault(t => rawTitle.Contains(t, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(matchedTitle))
            {
                string targetCategory = isMed ? "รับยาเข้าคลัง (MED)" : "รับเวชภัณฑ์เข้าคลัง (SUP)";
                throw new Exception($"ประเภทการรับเข้าที่คุณเลือกคือ '{targetCategory}' แต่หัวเอกสารในไฟล์คือ '{rawTitle}' ซึ่งไม่ตรงกับเงื่อนไขที่กำหนด");
            }

            var resultList = new List<ReceiveDetail>();
            int headerRowIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("รายการ") || lines[i].Contains("ชื่อยา") || lines[i].Contains("ชื่อเวชภัณฑ์"))
                {
                    headerRowIndex = i;
                    break;
                }
            }

            if (headerRowIndex == -1)
                throw new Exception("ไม่พบตารางข้อมูลในไฟล์ CSV (ไม่พบหัวคอลัมน์รายการ)");

            for (int i = headerRowIndex + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cols = ParseCsvLine(lines[i]);
                if (cols.Count <= 1) continue;

                // ข้ามบรรทัดที่เป็น sub-header หรือไม่มีชื่อรายการ
                string rawName = cols[1].Trim();
                if (string.IsNullOrWhiteSpace(rawName) ||
                    rawName == "รายการยา" ||
                    rawName == "รายการเวชภัณฑ์ที่มิใช่ยา" ||
                    rawName == "ชื่อยา" ||
                    int.TryParse(rawName, out _))
                {
                    continue;
                }

                string medicineName = rawName;
                string packingSize = "";
                string accountType = isMed ? "ED" : "เวชภัณฑ์";
                string qtyRaw = "";
                string remark = "";

                if (isMed)
                {
                    // โครงสร้างไฟล์ใบเบิกยา (MED):
                    // Col 1: ชื่อยา
                    // Col 2: ความแรง / ปริมาตร
                    // Col 3: ขนาดบรรจุ
                    // Col 4: ประเภทบัญชียา (ED / NED)
                    // Col 7: จำนวนขอเบิก
                    // Col 8: หมายเหตุ

                    string strength = cols.Count > 2 ? cols[2].Trim() : "";
                    string pack = cols.Count > 3 ? cols[3].Trim() : "";

                    if (!string.IsNullOrEmpty(strength) && !string.IsNullOrEmpty(pack))
                        packingSize = $"{strength} ({pack})";
                    else if (!string.IsNullOrEmpty(strength))
                        packingSize = strength;
                    else
                        packingSize = pack;

                    if (cols.Count > 4)
                    {
                        var accVal = cols[4].Trim().ToUpper();
                        if (accVal.Contains("NED")) accountType = "NED";
                        else if (accVal.Contains("ED")) accountType = "ED";
                    }

                    qtyRaw = cols.Count > 7 ? cols[7].Trim() : (cols.Count > 6 ? cols[6].Trim() : "");
                    remark = cols.Count > 8 ? cols[8].Trim() : "";
                }
                else
                {
                    // โครงสร้างไฟล์เวชภัณฑ์ (SUP):
                    // Col 1: ชื่อเวชภัณฑ์
                    // Col 2: ขนาด / คุณลักษณะ
                    // Col 3: จำนวนขอเบิก
                    // Col 5: หมายเหตุ

                    packingSize = cols.Count > 2 ? cols[2].Trim() : "";
                    qtyRaw = cols.Count > 3 ? cols[3].Trim() : "";
                    remark = cols.Count > 5 ? cols[5].Trim() : "";
                }

                int qty = ParseQuantity(qtyRaw);

                string prefix = isMed ? "MED" : "SUP";
                resultList.Add(new ReceiveDetail
                {
                    Receive_detail_id = Guid.NewGuid().ToString("N"),
                    Medicine_id = $"{prefix}-TEMP-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    Medicine_name = medicineName,
                    Quantity_received = qty,
                    Price = 0m,
                    Lot_number = $"LOT-{DateTime.Now:yyyyMMdd}",
                    Expiry_date = DateTime.Now.AddYears(2),
                    Packing_Size = packingSize,
                    Account_Type = accountType,
                    Remark = remark,
                    SheetName = matchedTitle,
                    ActionType = "AddNew",
                    IsDuplicateOrSimilar = false
                });
            }

            return resultList;
        }

        /// <summary>
        /// แปลงข้อความจำนวน เช่น "4*50", "1*500", "10 ขวด", "2x25" ให้เป็นตัวเลขรวม
        /// </summary>
        private int ParseQuantity(string qtyStr)
        {
            if (string.IsNullOrWhiteSpace(qtyStr)) return 0;

            qtyStr = qtyStr.Trim().Replace(",", "");

            // ลบชื่อหน่วยนับออกเพื่อป้องกันการรบกวนการ parse ตัวเลข
            string[] units = { "อัน", "ชิ้น", "ขวด", "กล่อง", "โหล", "ม้วน", "ชั้น", "ซอง", "cap", "tab", "amp" };
            foreach (var u in units)
            {
                qtyStr = qtyStr.Replace(u, "", StringComparison.OrdinalIgnoreCase);
            }

            // คำนวณสูตรคูณ เช่น "4*50" หรือ "2x25"
            if (qtyStr.Contains('*') || qtyStr.Contains('x') || qtyStr.Contains('X'))
            {
                var parts = qtyStr.Split(new[] { '*', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var match1 = Regex.Match(parts[0], @"\d+");
                    var match2 = Regex.Match(parts[1], @"\d+");
                    if (match1.Success && match2.Success)
                    {
                        if (int.TryParse(match1.Value, out int p1) && int.TryParse(match2.Value, out int p2))
                        {
                            return p1 * p2;
                        }
                    }
                }
            }

            var match = Regex.Match(qtyStr, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int result))
            {
                return result;
            }

            return 0;
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder sb = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString().Trim());
            return result;
        }

        private Encoding DetectEncoding(string filePath)
        {
            using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
            {
                reader.Peek();
                if (reader.CurrentEncoding == Encoding.UTF8)
                    return Encoding.UTF8;
            }
            return Encoding.GetEncoding(874);
        }
    }
}