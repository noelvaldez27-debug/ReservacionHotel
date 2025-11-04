using System.Linq.Expressions;

namespace HotelSystem.Domain.Interfaces;

public interface IGenericRepository<T> where T : class
{
 Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
 Task<IEnumerable<T>> GetAll(CancellationToken cancellationToken = default);
 Task AddAsync(T entity, CancellationToken cancellationToken = default);
 void Update(T entity);
 void Remove(T entity);
 Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}