using Azure.Core;
using DrugInventoryPro.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugInventoryPro.Data
{
    public class DrugInventoryContext : DbContext
    {
        public DrugInventoryContext(DbContextOptions<DrugInventoryContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Departments> Departments { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Medicines> Medicines { get; set; }
        public DbSet<Stock> Stock { get; set; }
        public DbSet<Receive> Receives { get; set; }
        public DbSet<Dispense> Dispense { get; set; }
        public DbSet<DispenseDetail> DispenseDetails { get; set; }
        public DbSet<ReceiveDetail> ReceiveDetails { get; set; }
        public DbSet<MedicineUnit> MedicineUnits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. คอนฟิกตาราง Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.User_id);
            });

            // 2. คอนฟิกตาราง Categories
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");
                entity.HasKey(c => c.Category_id);
            });

            // 3. คอนฟิกตาราง Departments
            modelBuilder.Entity<Departments>(entity =>
            {
                entity.ToTable("Departments", tb => tb.HasTrigger("trg_Departments_Update"));
                entity.HasKey(d => d.Department_id);
            });

            // 4. คอนฟิกตาราง Medicines
            modelBuilder.Entity<Medicines>(entity =>
            {
                entity.ToTable("Medicines", tb => tb.HasTrigger("trg_Medicines_Update"));
                entity.HasKey(m => m.Medicine_id);

                entity.HasOne(m => m.Category)
                    .WithMany(c => c.Medicines)
                    .HasForeignKey(m => m.Category_id)
                    .HasPrincipalKey(c => c.Category_id)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(m => m.MedicineUnit)
                    .WithMany(mu => mu.Medicines)
                    .HasForeignKey(m => m.Unit_id)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // 5. คอนฟิกตาราง Stock
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.ToTable("Stock");
                entity.HasKey(s => s.StockId);

                entity.HasOne(s => s.Medicines)
                    .WithMany()
                    .HasForeignKey(s => s.Medicine_id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 7. คอนฟิกตารางฝั่งการจ่ายยา (Dispense & DispenseDetail)
            modelBuilder.Entity<Dispense>(entity =>
            {
                entity.ToTable("Dispense", tb => tb.HasTrigger("trg_Dispense_Insert"));
                entity.HasKey(d => d.Dispense_id);

                // 🟢 เพิ่มการผูกความสัมพันธ์กับตาราง Department ให้ชัดเจน
                entity.HasOne(d => d.Departments)
                    .WithMany()
                    .HasForeignKey(d => d.Department_id)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<DispenseDetail>(entity =>
            {
                entity.ToTable("DispenseDetails");
                entity.HasKey(dd => dd.Dispense_detail_id);

                entity.HasOne(dd => dd.Dispense)
                    .WithMany(d => d.DispenseDetails)
                    .HasForeignKey(dd => dd.Dispense_id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dd => dd.Medicine)
                    .WithMany()
                    .HasForeignKey(dd => dd.Medicine_id)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 8. คอนฟิกตารางการรับเข้า (Receive & ReceiveDetail)
            modelBuilder.Entity<Receive>(entity =>
            {
                entity.ToTable("Receives", tb => tb.HasTrigger("trg_Receive_Insert"));
                entity.HasKey(r => r.Receive_id);
            });

            modelBuilder.Entity<ReceiveDetail>(entity =>
            {
                entity.ToTable("ReceiveDetails");
                entity.HasKey(rd => rd.Receive_detail_id);

                entity.HasOne(rd => rd.Receive)
                    .WithMany(r => r.ReceiveDetails)
                    .HasForeignKey(rd => rd.Receive_id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rd => rd.Medicines)
                    .WithMany()
                    .HasForeignKey(rd => rd.Medicine_id)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 9. คอนฟิกตารางหน่วยนับ
            modelBuilder.Entity<MedicineUnit>(entity =>
            {
                entity.ToTable("MedicineUnits");
                entity.HasKey(mu => mu.Unit_id);
            });
        }
    }
}