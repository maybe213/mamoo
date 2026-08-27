namespace DrugInventoryPro.Models
{
    public class DiseaseReportViewModel
    {
        public string DiseaseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int TotalQuantityUsed { get; set; }
        public int MedicineCount { get; set; }
    }
}