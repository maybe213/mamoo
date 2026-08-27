using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugInventoryPro.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        [Column("category_id")]
        [StringLength(10)]
        public string? Category_id { get; set; }

        [Column("category_name")]
        [StringLength(100)]
        public string? Category_name { get; set; }

        [Column("description")]
        [StringLength(255)]
        public string? Description { get; set; }

        [Column("status")]
        [StringLength(20)]
        public string? Status { get; set; }

        [Column("related_disease")]
        public string? Related_disease { get; set; }

        // Navigation: ยาที่อยู่ในหมวดหมู่นี้
        public virtual ICollection<Medicines> Medicines { get; set; } = new List<Medicines>();
    }
}
