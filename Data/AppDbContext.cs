using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<ShippingInfo> ShippingInfos => Set<ShippingInfo>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>()
            .Property(product => product.Price)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Product>()
            .Ignore(product => product.SalePrice);

        builder.Entity<ProductImage>()
            .HasOne(image => image.Product)
            .WithMany(product => product.Images)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StockLog>()
            .HasOne(log => log.Product)
            .WithMany()
            .HasForeignKey(log => log.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StockLog>()
            .HasOne(log => log.ChangedByUser)
            .WithMany()
            .HasForeignKey(log => log.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Order>()
            .Property(order => order.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<OrderItem>()
            .Property(item => item.UnitPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Voucher>()
            .Property(voucher => voucher.Value)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Voucher>()
            .Property(voucher => voucher.MinOrderAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Voucher>()
            .Property(voucher => voucher.MaxDiscount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Category>()
            .HasIndex(category => category.Slug)
            .IsUnique();

        builder.Entity<Order>()
            .HasOne(order => order.ShippingInfo)
            .WithOne(info => info.Order)
            .HasForeignKey<ShippingInfo>(info => info.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CartItem>()
            .HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WishlistItem>()
            .HasIndex(item => new { item.UserId, item.ProductId })
            .IsUnique();

        builder.Entity<WishlistItem>()
            .HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WishlistItem>()
            .HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Review>()
            .HasIndex(review => new { review.UserId, review.ProductId })
            .IsUnique();

        builder.Entity<Review>()
            .HasOne(review => review.User)
            .WithMany()
            .HasForeignKey(review => review.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Review>()
            .HasOne(review => review.Product)
            .WithMany()
            .HasForeignKey(review => review.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Voucher>()
            .HasIndex(voucher => voucher.Code)
            .IsUnique();
    }
}
