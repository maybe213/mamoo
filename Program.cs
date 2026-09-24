using DrugInventoryPro.Data;
using DrugInventoryPro.Services; // 👈 เพิ่ม Namespace สำหรับ Services
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();

// 🛠️ ลงทะเบียน Services สำหรับการอ่านไฟล์ CSV และ Excel (แก้ปัญหา DI Error)
builder.Services.AddScoped<ExcelParserService>();
builder.Services.AddScoped<CsvParserService>();

// 🛠️ เพิ่มส่วนขยายขีดจำกัดการรับค่า Form (แก้ไขปัญหา HTTP ERROR 400)
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 10000;           // ขยายจำนวน Keys/Inputs สูงสุดเป็น 10,000 ตัว
    options.ValueLengthLimit = int.MaxValue;   // ขยายความยาวของข้อมูลในแต่ละช่อง
    options.MultipartBodyLengthLimit = int.MaxValue; // รองรับขนาด Body ของ Form
});

builder.Services.AddDbContext<DrugInventoryContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("DrugInventoryPro");

            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            );
        }
    )
);

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// สำคัญ
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();