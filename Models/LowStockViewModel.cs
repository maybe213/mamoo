using System;
using DrugInventoryPro.Models;

namespace DrugInventoryPro.Models
{
    public class LowStockViewModel
    {
        public Medicines Medicine { get; set; } = null!;
        public int CurrentStock { get; set; }           // สต็อกคงเหลือปัจจุบัน (m.Stock)
        public int SafetyStock { get; set; }            // เกณฑ์ขั้นต่ำจากแพทย์/เภสัชกร (m.SafetyStock)
        public decimal AvgDailyUsage { get; set; }      // อัตราการใช้ยาลดลงต่อวัน (30 วันย้อนหลัง)
        public int LeadTimeDays { get; set; }           // ระยะเวลาจัดส่งสินค้า (วัน)
        public int ReorderPoint { get; set; }           // จุดสั่งซื้อใหม่ (ROP = Lead Time Demand + Safety Stock)
        public int AutoReorderPoint { get; set; }       // ROP ที่ระบบคำนวณอัตโนมัติ
        public bool IsManualRop { get; set; }           // true = ใช้ค่าที่เภสัชกรกำหนดเอง (ReorderPoint คือค่านั้น)
        public int SuggestedROQ { get; set; }           // ปริมาณแนะนำสั่งซื้อ (ROQ)
        public bool IsCritical { get; set; }            // สถานะวิกฤต (Stock <= Safety Stock)
        public bool IsReorder { get; set; }             // สถานะต้องสั่งซื้อเพิ่ม (Stock <= ROP)
        public bool IsExpired { get; set; }             // หมดอายุแล้ว
        public bool IsExpiringSoon { get; set; }        // ใกล้หมดอายุ (รวม 3 ระดับ ≤ 6 เดือน) — คงไว้เพื่อความเข้ากันได้ย้อนหลัง

        // เกณฑ์เตือนวันหมดอายุ 3 ระดับ (สอดคล้องกับหน้า Stock Alert / เบิกยา / Catalog / ปฏิทิน)
        public bool IsExpiringWithin1Month { get; set; }  // 🔴 แดง: เหลืออายุ ≤ 1 เดือน (ควรงดจ่าย)
        public bool IsExpiringWithin3Months { get; set; } // 🟡 เหลือง: เหลืออายุ ≤ 3 เดือน
        public bool IsExpiringWithin6Months { get; set; } // 🟢 เขียว: เหลืออายุ ≤ 6 เดือน
    }
}