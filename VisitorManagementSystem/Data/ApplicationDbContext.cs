using Microsoft.EntityFrameworkCore;
using VisitorManagementSystem.Models;

namespace VisitorManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {


        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Visitor>()
                .HasIndex(v => v.NationalId)
                .IsUnique();
        }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<Visit> Visits { get; set; }
        public DbSet<Receptionist> Receptionists { get; set; }
        public DbSet<VisitType> VisitTypes { get; set; }
        public DbSet<VisitorItem> VisitorItems { get; set; }
    }
}
