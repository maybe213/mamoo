using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Dispense")]
    public class Dispense
    {
        [Key]
        public string Dispense_id { get; set; } = null!;
        public DateTime? Dispense_date { get; set; }
        public string? Department_id { get; set; }
        public string? Status { get; set; }
        [ForeignKey("Department_id")]
        public virtual Departments? Departments { get; set; }
        public virtual ICollection<DispenseDetail> DispenseDetails { get; set; } = new List<DispenseDetail>();
    }
}