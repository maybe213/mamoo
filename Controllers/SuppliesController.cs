using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using DrugInventoryPro.Data;
using DrugInventoryPro.Models;

namespace DrugInventoryPro.Controllers
{
    public class SuppliesController : Controller
    {
        private readonly DrugInventoryContext _context;

        
        public SuppliesController(DrugInventoryContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UploadExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "ไม่พบไฟล์ หรือไฟล์ไม่มีข้อมูล กรุณาเลือกไฟล์ Excel ใหม่";
                return RedirectToAction("Index", "Receive");
            }

            try
            {
                var extension = Path.GetExtension(excelFile.FileName).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    TempData["Error"] = "รองรับเฉพาะไฟล์นามสกุล .xlsx หรือ .xls เท่านั้น";
                    return RedirectToAction("Index", "Receive");
                }

                // 💡 จุดนี้สำหรับนำโค้ดอ่าน Excel (EPPlus / ExcelDataReader) มาใส่ในอนาคต

                TempData["Success"] = "นำเข้าข้อมูลจาก Excel เรียบร้อยแล้ว";
                return RedirectToAction("Index", "Receive");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "เกิดข้อผิดพลาดระหว่างนำเข้าไฟล์: " + ex.Message;
                return RedirectToAction("Index", "Receive");
            }
        }
    }
}