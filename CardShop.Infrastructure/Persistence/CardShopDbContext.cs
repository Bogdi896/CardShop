using CardShop.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure.Persistence
{
    public class CardShopDbContext : DbContext
    {
        public CardShopDbContext(
            DbContextOptions<CardShopDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(CardShopDbContext).Assembly);
        }
    }
}