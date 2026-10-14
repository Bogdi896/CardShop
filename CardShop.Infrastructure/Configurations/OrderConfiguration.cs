using CardShop.Domain.Models;
using CardShop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardShop.Infrastructure.Persistence.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");

            builder.HasKey(order => order.Id);

            builder.Property(order => order.UserId)
                .IsRequired();

            builder.Property(order => order.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(order => order.CreatedAt)
                .IsRequired();

            builder.Property(order => order.TotalAmount)
    .HasPrecision(18, 2)
    .IsRequired();

            builder.HasMany(order => order.Items)
                .WithOne(item => item.Order)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<ApplicationUser>()
    .WithMany()
    .HasForeignKey(order => order.UserId)
    .OnDelete(DeleteBehavior.Restrict);

            builder.Property(order => order.CheckoutId)
    .IsRequired();

            builder.HasIndex(order => new
            {
                order.UserId,
                order.CheckoutId
            }).IsUnique();
        }
    }
}