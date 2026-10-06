using System;
using System.Collections.Generic;
using System.Linq;

namespace DrugInventoryPro.Models
{
    /// <summary>แถวรายละเอียดรายการยา 1 รายการในรายงานประจำเดือน/ประจำปี</summary>
    public class InventoryReportRow
    {
        public string MedicineId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "-";
        public decimal Price { get; set; }

        public int OpeningQty { get; set; }     // ยอดยกมา
        public int ReceivedQty { get; set; }    // รับเข้าในงวด
        public int DispensedQty { get; set; }   // จ่ายออกในงวด (เฉพาะใบเบิกที่อนุมัติแล้ว)
        public int ClosingQty => OpeningQty + ReceivedQty - DispensedQty; // ยอดคงเหลือ

        public decimal OpeningValue => OpeningQty * Price;
        public decimal ReceivedValue => ReceivedQty * Price;
        public decimal DispensedValue => DispensedQty * Price;
        public decimal ClosingValue => ClosingQty * Price;
    }

    /// <summary>สรุปรายเดือน (ใช้ในรายงานประจำปี / ปีงบประมาณ)</summary>
    public class InventoryReportMonth
    {
        public string Label { get; set; } = string.Empty;
        public decimal OpeningValue { get; set; }
        public decimal ReceivedValue { get; set; }
        public decimal DispensedValue { get; set; }
        public decimal ClosingValue { get; set; }
    }

    public class InventoryReportViewModel
    {
        public string Type { get; set; } = "monthly";   // monthly | annual | fiscal
        public int Year { get; set; }                   // ค.ศ. (ปีงบประมาณ = ปี ค.ศ. ที่งวดสิ้นสุด 30 ก.ย.)
        public int Month { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty;
        public bool IsOpenPeriod { get; set; }          // งวดยังไม่สิ้นสุด (ข้อมูลถึงวันนี้)
        public string PreparedBy { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public List<InventoryReportRow> Rows { get; set; } = new();
        public List<InventoryReportMonth> Months { get; set; } = new();

        public int NegativeClosingCount => Rows.Count(r => r.ClosingQty < 0);

        // เดือนที่มีรายการรับ/จ่ายจริงในระบบ (ใช้เป็นตัวเลือกในหน้ารายงาน)
        public List<DateTime> AvailableMonths { get; set; } = new();
        public List<int> AvailableYears => AvailableMonths.Select(m => m.Year).Distinct().OrderByDescending(y => y).ToList();
        public List<int> AvailableFiscalYears => AvailableMonths.Select(m => m.Month >= 10 ? m.Year + 1 : m.Year).Distinct().OrderByDescending(y => y).ToList();

        public decimal TotalOpeningValue => Rows.Sum(r => r.OpeningValue);
        public decimal TotalReceivedValue => Rows.Sum(r => r.ReceivedValue);
        public decimal TotalDispensedValue => Rows.Sum(r => r.DispensedValue);
        public decimal TotalClosingValue => Rows.Sum(r => r.ClosingValue);
    }
}