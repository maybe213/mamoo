using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            bool isGeneralDrugSheet = matchedTitle.Contains("ใบเบิกเวชภัณฑ์ยา (ยาทั่วไป)");
            var resultList = new List<ReceiveDetail>();
            int headerRowIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("รายการ") || lines[i].Contains("ชื่อยา"))
                {
                    headerRowIndex = i;
                    break;
                }
            }

            if (headerRowIndex == -1)
                throw new Exception("ไม่พบตารางข้อมูลในไฟล์ CSV (ไม่พบหัวคอลัมน์รายการ)");

            var headers = ParseCsvLine(lines[headerRowIndex]);

            int nameIdx = GetColumnIndex(headers, "รายการ", "ชื่อยา", "ชื่อเวชภัณฑ์");
            int strengthIdx = isGeneralDrugSheet ? GetColumnIndex(headers, "ความแรง", "ปริมาตรบรรจุ") : -1;
            int packIdx = GetColumnIndex(headers, "ขนาดบรรจุ", "หน่วยบรรจุ");
            int qtyIdx = GetColumnIndex(headers, "จำนวนเบิก", "จำนวนรับ", "จำนวน");
            int priceIdx = GetColumnIndex(headers, "ราคา", "ราคา/หน่วย");
            int lotIdx = GetColumnIndex(headers, "Lot", "เลขล็อต");
            int expIdx = GetColumnIndex(headers, "วันหมดอายุ", "Exp");

            for (int i = headerRowIndex + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cols = ParseCsvLine(lines[i]);
                if (cols.Count == 0 || nameIdx >= cols.Count || string.IsNullOrWhiteSpace(cols[nameIdx]))
                    continue;

                string medicineName = cols[nameIdx].Trim();
                string packingSize = "";

                if (isGeneralDrugSheet && strengthIdx != -1 && strengthIdx < cols.Count)
                {
                    string strength = cols[strengthIdx].Trim();
                    if (!string.IsNullOrEmpty(strength)) packingSize = strength;
                }

                if (packIdx != -1 && packIdx < cols.Count && !string.IsNullOrEmpty(cols[packIdx]))
                {
                    packingSize += string.IsNullOrEmpty(packingSize) ? cols[packIdx].Trim() : $" ({cols[packIdx].Trim()})";
                }

                int.TryParse(GetColValue(cols, qtyIdx), out int qty);
                decimal.TryParse(GetColValue(cols, priceIdx), out decimal price);

                string lot = GetColValue(cols, lotIdx);
                if (string.IsNullOrEmpty(lot)) lot = "-";

                DateTime expDate = DateTime.Now.AddYears(1);
                if (expIdx != -1 && expIdx < cols.Count)
                {
                    DateTime.TryParse(cols[expIdx], out expDate);
                }

                resultList.Add(new ReceiveDetail
                {
                    Medicine_name = medicineName,
                    Quantity_received = qty > 0 ? qty : 1,
                    Price = price,
                    Lot_number = lot,
                    Expiry_date = expDate > DateTime.MinValue ? expDate : DateTime.Now.AddYears(1),
                    Packing_Size = packingSize,
                    Account_Type = isMed ? "ED" : "เวชภัณฑ์",
                    SheetName = matchedTitle
                });
            }

            return resultList;
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

        private int GetColumnIndex(List<string> headers, params string[] keywords)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                foreach (var kw in keywords)
                {
                    if (headers[i].Contains(kw, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        private string GetColValue(List<string> cols, int index)
        {
            return (index != -1 && index < cols.Count) ? cols[index].Trim() : "";
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