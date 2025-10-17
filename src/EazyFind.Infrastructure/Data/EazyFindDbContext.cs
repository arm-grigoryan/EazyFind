using System.Collections.Generic;
using System.Linq;
using EazyFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EazyFind.Infrastructure.Data;

public class EazyFindDbContext(DbContextOptions<EazyFindDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<StoreCategory> StoreCategories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<ProductAlert> ProductAlerts { get; set; }
    public DbSet<ProductAlertMatch> ProductAlertMatches { get; set; }

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
                .HasMaxLength(256);

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

        modelBuilder.Entity<ProductAlert>(builder =>
        {
            builder.ToTable("product_alerts");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ChatId)
                .HasColumnName("chat_id")
                .IsRequired();

            builder.Property(a => a.SearchText)
                .HasColumnName("search_text")
                .IsRequired();

            builder.Property(a => a.StoreKeys)
                .HasColumnName("store_keys")
                .HasColumnType("text[]")
                .HasDefaultValueSql("'{}'::text[]")
                .HasConversion(
                    v => v.ToArray(),
                    v => v == null ? new List<string>() : v.ToList());

            builder.Property(a => a.MinPrice)
                .HasColumnName("min_price")
                .HasColumnType("numeric(12,2)");

            builder.Property(a => a.MaxPrice)
                .HasColumnName("max_price")
                .HasColumnType("numeric(12,2)");

            builder.Property(a => a.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            builder.Property(a => a.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()");

            builder.Property(a => a.LastCheckedUtc)
                .HasColumnName("last_checked_utc")
                .HasColumnType("timestamptz");

            builder.HasIndex(a => a.ChatId)
                .HasDatabaseName("ix_product_alerts_chat");

            builder.HasIndex(a => a.IsActive)
                .HasDatabaseName("ix_product_alerts_active");
        });

        modelBuilder.Entity<ProductAlertMatch>(builder =>
        {
            builder.ToTable("product_alert_matches");
            builder.HasKey(m => new { m.AlertId, m.ProductId });

            builder.Property(m => m.AlertId)
                .HasColumnName("alert_id")
                .IsRequired();

            builder.Property(m => m.ProductId)
                .HasColumnName("product_id")
                .IsRequired();

            builder.Property(m => m.MatchedAtUtc)
                .HasColumnName("matched_at_utc")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()");

            builder.HasOne(m => m.Alert)
                .WithMany(a => a.Matches)
                .HasForeignKey(m => m.AlertId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
