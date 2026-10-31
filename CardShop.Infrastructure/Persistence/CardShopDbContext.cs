
using CardShop.Domain.Models;
using CardShop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure.Persistence;

public class CardShopDbContext : IdentityDbContext<ApplicationUser>
{
    public CardShopDbContext(
        DbContextOptions<CardShopDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CardShopDbContext).Assembly);

        // Each registration attempt has a unique identifier.
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(user => user.RegistrationId)
            .IsUnique()
            .HasFilter("[RegistrationId] IS NOT NULL");
    }
}
