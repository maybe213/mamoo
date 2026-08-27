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
        public int SuggestedROQ { get; set; }           // ปริมาณแนะนำสั่งซื้อ (ROQ)
        public bool IsCritical { get; set; }            // สถานะวิกฤต (Stock <= Safety Stock)
        public bool IsReorder { get; set; }             // สถานะต้องสั่งซื้อเพิ่ม (Stock <= ROP)
        public bool IsExpired { get; set; }             // หมดอายุแล้ว
        public bool IsExpiringSoon { get; set; }        // ใกล้หมดอายุ (30 วัน)
    }
}