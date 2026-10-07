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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(CardShopDbContext).Assembly);
        }
    }
}