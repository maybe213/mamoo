using Microsoft.AspNetCore.Mvc;
using DrugInventoryPro.Data;
using DrugInventoryPro.Services;
using System.Linq;

namespace DrugInventoryPro.Controllers
{
    public class AccountController : Controller
    {
        private readonly DrugInventoryContext _context;

        public AccountController(DrugInventoryContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var user = _context.Users
                .FirstOrDefault(u => u.Username == username && u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Login Failed";
                return View();
            }

            // 🛡️ บัญชีที่ปิดใช้งานแล้วเข้าสู่ระบบไม่ได้
            if (user.Status == "Inactive")
            {
                ViewBag.Error = "บัญชีนี้ถูกปิดใช้งาน กรุณาติดต่อผู้ดูแลระบบ";
                return View();
            }

            HttpContext.Session.SetString("Username", user.Username ?? "");
            // บันทึก Role ในรูปแบบมาตรฐาน (ตัดช่องว่าง/แก้ตัวพิมพ์) เพื่อให้เมนูและสิทธิ์ทำงานถูกต้อง
            var role = RoleHelper.Normalize(user.Role) ?? (user.Role ?? "").Trim();
            HttpContext.Session.SetString("Role", role);

            HttpContext.Session.SetString(
                "FullName",
                $"{user.Title} {user.Firstname} {user.Lastname}"
            );

            // Admin มี Dashboard ของตัวเอง (ภาพรวมผู้ใช้และแผนก)
            if (role == "Admin")
                return RedirectToAction("Index", "AdminDashboard");

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); //  ลบ session ทั้งหมด
            return RedirectToAction("Login");
        }
    }
}