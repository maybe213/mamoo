using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    public class ReceiveDetail
    {
        [Key]
        public string Receive_detail_id { get; set; } = Guid.NewGuid().ToString("N");

        public string? Receive_id { get; set; }

        [ForeignKey("Receive_id")]
        public virtual Receive? Receive { get; set; }

        public string? Medicine_id { get; set; }

        [ForeignKey("Medicine_id")]
        public virtual Medicines? Medicines { get; set; }

        public int Quantity_received { get; set; }
        public string? Lot_number { get; set; }
        public DateTime Expiry_date { get; set; } = DateTime.Now.AddYears(2);
        public string? SheetName { get; set; }

        // =============================================================
        // พร็อพเพอร์ตี้ชั่วคราวสำหรับอ่านจาก Excel และแสดงผล (NotMapped)
        // =============================================================
        [NotMapped]
        public string? Medicine_name { get; set; }

        [NotMapped]
        public decimal? Price { get; set; }

        [NotMapped]
        public string? Packing_Size { get; set; }

        [NotMapped]
        public string? Account_Type { get; set; }

        [NotMapped]
        public string? Unit_id { get; set; }

        [NotMapped]
        public int Stock { get; set; }

        [NotMapped]
        public string? Remark { get; set; }

        [NotMapped]
        public string ActionType { get; set; } = "AddNew";

        [NotMapped]
        public string? MatchedMedicineId { get; set; }

        [NotMapped]
        public string? MatchedMedicineName { get; set; }

        [NotMapped]
        public bool IsDuplicateOrSimilar { get; set; } = false;
    }
}