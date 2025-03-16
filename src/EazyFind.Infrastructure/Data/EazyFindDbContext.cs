using EazyFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data;

public class EazyFindDbContext(DbContextOptions<EazyFindDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<StoreCategory> StoreCategories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Store> Stores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(builder =>
        {
            builder.HasKey(c => c.Key);

            builder.Property(c => c.Key)
                .HasConversion<string>();

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(c => c.Name)
                .IsUnique();

            builder.Property(c => c.CreatedAt)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Category>(builder =>
        {
            builder.HasKey(c => c.Type);

            builder.Property(c => c.Type)
                .HasConversion<string>();

            builder.Property(c => c.CreatedAt)
                .HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<StoreCategory>(builder =>
        {
            builder.Property(sc => sc.OriginalCategoryName)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(sc => sc.CreatedAt)
                .HasDefaultValueSql("NOW()");

            builder.HasOne(sc => sc.Store)
                .WithMany(s => s.StoreCategories)
                .HasForeignKey(sc => sc.StoreKey);

            builder.HasOne(sc => sc.Category)
                .WithMany(c => c.StoreCategories)
                .HasForeignKey(sc => sc.CategoryType);
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(128);

            builder.HasIndex(p => p.Name)
                .HasMethod("GIN")
                .HasOperators("gin_trgm_ops");

            builder.Property(p => p.Price)
                .HasColumnType("DECIMAL(10,2)");

            builder.Property(p => p.Url)
                .HasColumnType("TEXT");

            builder.HasIndex(p => p.Url)
                .IsUnique();

            builder.Property(p => p.ImageUrl)
                .HasColumnType("TEXT");

            builder.Property(p => p.LastSyncedAt)
                .HasDefaultValueSql("NOW()");

            builder.Property(p => p.CreatedAt)
                .HasDefaultValueSql("NOW()");

            builder.HasOne(p => p.StoreCategory)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.StoreCategoryId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
