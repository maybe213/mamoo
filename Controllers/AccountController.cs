using Microsoft.AspNetCore.Mvc;
using DrugInventoryPro.Data;
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

            HttpContext.Session.SetString("Username", user.Username ?? "");
            HttpContext.Session.SetString("Role", user.Role ?? "");

            HttpContext.Session.SetString(
                "FullName",
                $"{user.Title} {user.Firstname} {user.Lastname}"
            );

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); //  ลบ session ทั้งหมด
            return RedirectToAction("Login");
        }
    }
}