using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
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
    public DbSet<ReviewImage> ReviewImages => Set<ReviewImage>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherUsage> VoucherUsages => Set<VoucherUsage>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSearchTerm> ProductSearchTerms => Set<ProductSearchTerm>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();
    public DbSet<CustomerSegment> CustomerSegments => Set<CustomerSegment>();
    public DbSet<CustomerSegmentMember> CustomerSegmentMembers => Set<CustomerSegmentMember>();
    public DbSet<AbandonedCartReminder> AbandonedCartReminders => Set<AbandonedCartReminder>();
    public DbSet<CrossSellOffer> CrossSellOffers => Set<CrossSellOffer>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<ReturnWarrantyRequest> ReturnWarrantyRequests => Set<ReturnWarrantyRequest>();
    public DbSet<ReturnWarrantyRequestItem> ReturnWarrantyRequestItems => Set<ReturnWarrantyRequestItem>();
    public DbSet<ReturnWarrantyRequestImage> ReturnWarrantyRequestImages => Set<ReturnWarrantyRequestImage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Ignore<IdentityUserClaim<string>>();
        builder.Ignore<IdentityRoleClaim<string>>();
        builder.Ignore<IdentityUserLogin<string>>();
        builder.Ignore<IdentityUserToken<string>>();

        builder.Entity<Product>()
            .HasQueryFilter(product => !product.IsDeleted);

        builder.Entity<Product>()
            .Property(product => product.Price)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Product>()
            .Ignore(product => product.SalePrice);

        builder.Entity<Product>()
            .Ignore(product => product.PrimaryImageUrl);

        builder.Entity<ProductImage>()
            .HasOne(image => image.Product)
            .WithMany(product => product.Images)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductImage>()
            .HasIndex(image => new { image.ProductId, image.ImageUrl })
            .IsUnique();

        builder.Entity<ProductImage>()
            .HasIndex(image => new { image.ProductId, image.SortOrder })
            .IsUnique();

        builder.Entity<ProductSearchTerm>()
            .HasIndex(term => term.Term);

        builder.Entity<ProductSearchTerm>()
            .HasIndex(term => new { term.ProductId, term.Term })
            .IsUnique();

        builder.Entity<ProductSearchTerm>()
            .HasOne(term => term.Product)
            .WithMany(product => product.SearchTerms)
            .HasForeignKey(term => term.ProductId)
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

        builder.Entity<Order>()
            .Property(order => order.ShippingFee)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(order => order.DiscountAmount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Order>()
            .Property(order => order.RefundStatus)
            .HasMaxLength(40)
            .HasDefaultValue(RefundStatuses.NotRequired);

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

        builder.Entity<Cart>()
            .Property(cart => cart.UpdatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Entity<Cart>()
            .HasIndex(cart => cart.UpdatedAt);

        builder.Entity<CartItem>()
            .HasIndex(item => new { item.CartId, item.ProductId })
            .IsUnique();

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

        builder.Entity<ReviewImage>()
            .HasOne(image => image.Review)
            .WithMany(review => review.Images)
            .HasForeignKey(image => image.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Voucher>()
            .HasIndex(voucher => voucher.Code)
            .IsUnique();

        builder.Entity<Voucher>()
            .HasOne(voucher => voucher.CustomerSegment)
            .WithMany(segment => segment.Vouchers)
            .HasForeignKey(voucher => voucher.CustomerSegmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Voucher>()
            .HasOne(voucher => voucher.TargetUser)
            .WithMany()
            .HasForeignKey(voucher => voucher.TargetUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Voucher>()
            .HasIndex(voucher => voucher.TargetUserId);

        builder.Entity<VoucherUsage>()
            .HasIndex(usage => new { usage.VoucherId, usage.UserId })
            .IsUnique();

        builder.Entity<VoucherUsage>()
            .HasIndex(usage => usage.OrderId)
            .IsUnique();

        builder.Entity<VoucherUsage>()
            .HasOne(usage => usage.Voucher)
            .WithMany(voucher => voucher.Usages)
            .HasForeignKey(usage => usage.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VoucherUsage>()
            .HasOne(usage => usage.User)
            .WithMany()
            .HasForeignKey(usage => usage.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VoucherUsage>()
            .HasOne(usage => usage.Order)
            .WithOne(order => order.VoucherUsage)
            .HasForeignKey<VoucherUsage>(usage => usage.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CustomerSegment>()
            .HasIndex(segment => segment.Code)
            .IsUnique();

        builder.Entity<CustomerSegmentMember>()
            .Property(member => member.TotalSpent)
            .HasColumnType("decimal(18,2)");

        builder.Entity<CustomerSegmentMember>()
            .HasIndex(member => new { member.CustomerSegmentId, member.UserId })
            .IsUnique();

        builder.Entity<CustomerSegmentMember>()
            .HasOne(member => member.CustomerSegment)
            .WithMany(segment => segment.Members)
            .HasForeignKey(member => member.CustomerSegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CustomerSegmentMember>()
            .HasOne(member => member.User)
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AbandonedCartReminder>()
            .Property(reminder => reminder.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Entity<AbandonedCartReminder>()
            .HasIndex(reminder => new { reminder.CartId, reminder.CartUpdatedAt })
            .IsUnique();

        builder.Entity<AbandonedCartReminder>()
            .HasIndex(reminder => reminder.UserId);

        builder.Entity<AbandonedCartReminder>()
            .HasOne(reminder => reminder.Cart)
            .WithMany()
            .HasForeignKey(reminder => reminder.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AbandonedCartReminder>()
            .HasOne(reminder => reminder.User)
            .WithMany()
            .HasForeignKey(reminder => reminder.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AbandonedCartReminder>()
            .HasOne(reminder => reminder.Voucher)
            .WithMany()
            .HasForeignKey(reminder => reminder.VoucherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CrossSellOffer>()
            .HasIndex(offer => new { offer.AnchorProductId, offer.AddOnProductId })
            .IsUnique();

        builder.Entity<CrossSellOffer>()
            .HasOne(offer => offer.AnchorProduct)
            .WithMany()
            .HasForeignKey(offer => offer.AnchorProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CrossSellOffer>()
            .HasOne(offer => offer.AddOnProduct)
            .WithMany()
            .HasForeignKey(offer => offer.AddOnProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserNotification>()
            .Property(notification => notification.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Entity<UserNotification>()
            .HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt });

        builder.Entity<UserNotification>()
            .HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReturnWarrantyRequest>()
            .Property(request => request.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Entity<ReturnWarrantyRequest>()
            .HasIndex(request => new { request.UserId, request.CreatedAt });

        builder.Entity<ReturnWarrantyRequest>()
            .HasIndex(request => new { request.Status, request.Type });

        builder.Entity<ReturnWarrantyRequest>()
            .HasOne(request => request.User)
            .WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReturnWarrantyRequest>()
            .HasOne(request => request.Order)
            .WithMany(order => order.ReturnWarrantyRequests)
            .HasForeignKey(request => request.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReturnWarrantyRequestItem>()
            .HasOne(item => item.ReturnWarrantyRequest)
            .WithMany(request => request.Items)
            .HasForeignKey(item => item.ReturnWarrantyRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReturnWarrantyRequestItem>()
            .HasOne(item => item.OrderItem)
            .WithMany()
            .HasForeignKey(item => item.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReturnWarrantyRequestImage>()
            .HasOne(image => image.ReturnWarrantyRequest)
            .WithMany(request => request.Images)
            .HasForeignKey(image => image.ReturnWarrantyRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
