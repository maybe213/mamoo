using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("ReceiveDetails")]
    public class ReceiveDetail
    {
        [Key]
        [Column("Receive_detail_id")]
        public string Receive_detail_id { get; set; } = null!;

        [Column("Receive_id")]
        public string? Receive_id { get; set; }

        [Column("Medicine_id")]
        public string? Medicine_id { get; set; }

        [Column("Quantity_received")]
        public int Quantity_received { get; set; }

        [Column("Lot_number")]
        public string? Lot_number { get; set; }

        [Column("Expiry_date")]
        public DateTime Expiry_date { get; set; } // ✅ ไม่ใส่ ? เพราะใน DB เป็น NOT NULL

        [Column("SheetName")]
        public string? SheetName { get; set; }

        // ✅ Navigation Property (ประกาศเพียงครั้งเดียวเท่านั้น)
        [ForeignKey(nameof(Medicine_id))]
       
        public virtual Medicines? Medicines { get; set; }
        

        [ForeignKey(nameof(Receive_id))]
        public virtual Receive? Receive { get; set; }


        // -------------------------------------------------------------
        // ฟิลด์ชั่วคราวสำหรับ UI / Binding หน้าจอ (ไม่มีใน DB)
        // -------------------------------------------------------------
        [NotMapped]
        public string DuplicateAction { get; set; } = "UPDATE";
        [NotMapped]
        public string? Medicine_name { get; set; }
        [NotMapped]
        public int? SafetyStock { get; set; }

        [NotMapped]
        public decimal? Price { get; set; }

        [NotMapped]
        public string? Unit_id { get; set; }

        [NotMapped]
        public string? Packing_Size { get; set; }

        [NotMapped]
        public string? Account_Type { get; set; }

        [NotMapped]
        public bool IsDuplicateOrSimilar { get; set; }

        [NotMapped]
        public string? MatchedMedicineId { get; set; }

        [NotMapped]
        public string? MatchedMedicineName { get; set; }
        [NotMapped]
        public string? Category { get; set; }
        [NotMapped]

        public string? Remark { get; set; }
        [NotMapped]
        public string ActionType { get; set; } = "AddNew";

    }
}