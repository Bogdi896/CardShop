using CardShop.Domain.Models;


namespace CardShop.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task <Product> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task AddAsync (Product product, CancellationToken cancellationToken = default);
    }
}
