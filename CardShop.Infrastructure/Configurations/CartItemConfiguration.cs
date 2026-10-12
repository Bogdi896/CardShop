
using CardShop.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardShop.Infrastructure.Persistence.Configurations
{
    public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
    {
        public void Configure(EntityTypeBuilder<CartItem> builder)
        {
            builder.ToTable("CartItems");

            builder.HasKey(cartItem => cartItem.Id);

            builder.Property(cartItem => cartItem.Quantity)
                .IsRequired();

            builder.HasIndex(cartItem => new
            {
                cartItem.CartId,
                cartItem.ProductId
            })
            .IsUnique();

            builder.HasOne(cartItem => cartItem.Product)
                .WithMany()
                .HasForeignKey(cartItem => cartItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_CartItems_Quantity_Positive",
                    "[Quantity] > 0"));
        }
    }
}
