using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    public class DispenseDetail
    {
        [Key]
        public string Dispense_detail_id { get; set; } = Guid.NewGuid().ToString();
        public string Dispense_id { get; set; } = string.Empty;

        [Column("Medicine_id")]
        public string Medicine_id { get; set; } = string.Empty;

        public int? Quantity_dispensed { get; set; }
        public int? Quantity_requested { get; set; } = 0;

        [ForeignKey("Dispense_id")]
        public virtual Dispense? Dispense { get; set; }

        [ForeignKey("Medicine_id")]
        public virtual Medicines? Medicine { get; set; }
    }
}