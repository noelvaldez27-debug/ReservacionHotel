using HotelSystem.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelSystem.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
 private readonly DbContext _context;
 private readonly DbSet<T> _dbSet;

 public GenericRepository(DbContext context)
 {
 _context = context;
 _dbSet = _context.Set<T>();
 }

 public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
 => await _dbSet.FindAsync([id], cancellationToken);

 public async Task<IEnumerable<T>> GetAll(CancellationToken cancellationToken = default)
 => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

 public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
 => await _dbSet.AddAsync(entity, cancellationToken);

 public void Update(T entity) => _dbSet.Update(entity);

 public void Remove(T entity) => _dbSet.Remove(entity);

 public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
 => _context.SaveChangesAsync(cancellationToken);
}