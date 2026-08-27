using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Medicines")]
    public class Medicines
    {
        [Key]
        [Column("medicine_id")]
        public string Medicine_id { get; set; } = string.Empty;

        [Column("medicine_name")]
        public string? Medicine_name { get; set; }

        [Column("category_id")]
        public string? Category_id { get; set; }

        public virtual Category? Category { get; set; }

        [Column("Unit_id")]
        public int? Unit_id { get; set; }

        [Column("quantity")]
        public int? Quantity { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("expired_at")]
        public DateTime? Expired_at { get; set; }

        [Column("Price")]
        public decimal Price { get; set; }

        [Column("Stock")]
        public int? Stock { get; set; }

        [Column("Packing_Size")]
        public string? Packing_Size { get; set; }

        [Column("Account_Type")]
        public string? Account_Type { get; set; }

        [Column("lot")]
        public string? Lot { get; set; }
        [Column("SafetyStock")]
        public int? SafetyStock { get; set; }

        //  แมปความสัมพันธ์เข้าหาฟิลด์ Unit_id โดยตรง ถูกต้องสมบูรณ์แล้วครับ
        [ForeignKey(nameof(Unit_id))]
        public virtual MedicineUnit? MedicineUnit { get; set; }
    }
}