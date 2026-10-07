using CardShop.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardShop.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(product => product.Id);

            builder.Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(product => product.Description)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(product => product.Price)
                .HasPrecision(18, 2);

            builder.Property(product => product.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
        }
    }
}