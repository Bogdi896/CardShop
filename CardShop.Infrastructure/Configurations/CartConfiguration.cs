
using CardShop.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardShop.Infrastructure.Persistence.Configurations
{
    public class CartConfiguration : IEntityTypeConfiguration<Cart>
    {
        public void Configure(EntityTypeBuilder<Cart> builder)
        {
            builder.ToTable("Carts");

            builder.HasKey(cart => cart.Id);

            builder.Property(cart => cart.UserId)
                .IsRequired()
                .HasMaxLength(450);

            builder.HasIndex(cart => cart.UserId)
                .IsUnique();

            builder.HasMany(cart => cart.Items)
                .WithOne(cartItem => cartItem.Cart)
                .HasForeignKey(cartItem => cartItem.CartId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
