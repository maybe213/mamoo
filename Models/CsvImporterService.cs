using System.IO;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;

public class CsvImporterService
{
    private readonly DrugInventoryContext _context;
    public CsvImporterService(DrugInventoryContext context) => _context = context;

    public void ImportFromCsv(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var lines = File.ReadAllLines(filePath);

        // กำหนดจำนวนแถวที่จะข้ามตามชื่อไฟล์
        int skipRows = fileName.Contains("ยาสมุนไพร") || fileName.Contains("ยาทั่วไป") ? 5 : 6;

        foreach (var line in lines.Skip(skipRows))
        {
            var cols = line.Split(',');
            if (string.IsNullOrWhiteSpace(cols[1])) continue; // ข้ามบรรทัดว่าง

            var med = new Medicines
            {
                Medicine_id = Guid.NewGuid().ToString().Substring(0, 8),
                Medicine_name = cols[1],
                Packing_Size = cols[2],
                Account_Type = cols[3],
                Stock = int.TryParse(cols[6], out int s) ? s : 0 // คอลัมน์ที่ 6 คือคงเหลือ
            };
            _context.Medicines.Add(med);
        }
        _context.SaveChanges();
    }
}