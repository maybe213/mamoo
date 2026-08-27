using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Receives")]
    public class Receive
    {
        [Key]
        public string? Receive_id { get; set; } = string.Empty;
        public DateTime? Receive_date { get; set; }
        public string? Invoice_number { get; set; }
        public string? Received_by { get; set; }
        public string? Status { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? Total_amount { get; set; }
        public int? Po_id { get; set; }
        public virtual ICollection<ReceiveDetail> ReceiveDetails { get; set; } = new List<ReceiveDetail>();
    }
}
