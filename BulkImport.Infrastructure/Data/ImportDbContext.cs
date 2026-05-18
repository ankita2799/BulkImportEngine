namespace BulkImport.Infrastructure.Data;

using BulkImport.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

public class ImportDbContext : DbContext
{
    public ImportDbContext(DbContextOptions<ImportDbContext> options) : base(options) { }

    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportJobRow> ImportJobRows => Set<ImportJobRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BusinessPartner config
        modelBuilder.Entity<BusinessPartner>(e =>
        {
            e.ToTable("BusinessPartners");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.TaxId).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.TaxId).IsUnique();
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Type).IsRequired().HasMaxLength(50);
        });

        // ImportJob config
        modelBuilder.Entity<ImportJob>(e =>
        {
            e.ToTable("ImportJobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        // ImportJobRow config
        modelBuilder.Entity<ImportJobRow>(e =>
        {
            e.ToTable("ImportJobRows");
            e.HasKey(x => x.Id);
            e.Property(x => x.RawData).HasMaxLength(1000);
            e.Property(x => x.Errors).IsRequired().HasMaxLength(2000);
            e.HasOne(x => x.Job)
             .WithMany()
             .HasForeignKey(x => x.JobId);
        });
    }
}