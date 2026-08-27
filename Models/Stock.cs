using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Stock")]
    public class Stock
    {
        [Key]
        public int StockId { get; set; }

        [StringLength(20)]
        [Column("medicine_id")]
        public string Medicine_id { get; set; } = string.Empty;
        public int Quantity { get; set; }

        [ForeignKey("Medicine_id")]
        public virtual Medicines? Medicines { get; set; }
    }
}
