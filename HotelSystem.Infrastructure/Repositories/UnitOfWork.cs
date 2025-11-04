using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using HotelSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelSystem.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
 private readonly HotelDbContext _context;
 private IDbContextTransaction? _currentTransaction;

 public UnitOfWork(HotelDbContext context)
 {
 _context = context;
 Hoteles = new GenericRepository<Hotel>(_context);
 Habitaciones = new GenericRepository<Habitacion>(_context);
 TarifasHabitacion = new GenericRepository<TarifaHabitacion>(_context);
 Huespedes = new GenericRepository<Huesped>(_context);
 Reservas = new GenericRepository<Reserva>(_context);
 DetallesReserva = new GenericRepository<DetalleReserva>(_context);
 ServiciosAdicionales = new GenericRepository<ServicioAdicional>(_context);
 ReservaServicios = new GenericRepository<ReservaServicio>(_context);
 Facturas = new GenericRepository<Factura>(_context);
 Transacciones = new GenericRepository<TransaccionPago>(_context);
 Resenas = new GenericRepository<Resena>(_context);
 }

 public IGenericRepository<Hotel> Hoteles { get; }
 public IGenericRepository<Habitacion> Habitaciones { get; }
 public IGenericRepository<TarifaHabitacion> TarifasHabitacion { get; }
 public IGenericRepository<Huesped> Huespedes { get; }
 public IGenericRepository<Reserva> Reservas { get; }
 public IGenericRepository<DetalleReserva> DetallesReserva { get; }
 public IGenericRepository<ServicioAdicional> ServiciosAdicionales { get; }
 public IGenericRepository<ReservaServicio> ReservaServicios { get; }
 public IGenericRepository<Factura> Facturas { get; }
 public IGenericRepository<TransaccionPago> Transacciones { get; }
 public IGenericRepository<Resena> Resenas { get; }

 public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
 {
 if (_currentTransaction != null)
 return;
 _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
 }

 public async Task CommitAsync(CancellationToken cancellationToken = default)
 {
 if (_currentTransaction == null)
 {
 // No transaction: just save
 await _context.SaveChangesAsync(cancellationToken);
 return;
 }
 try
 {
 await _context.SaveChangesAsync(cancellationToken);
 await _currentTransaction.CommitAsync(cancellationToken);
 }
 catch
 {
 await RollbackAsync(cancellationToken);
 throw;
 }
 finally
 {
 await _currentTransaction.DisposeAsync();
 _currentTransaction = null;
 }
 }

 public async Task RollbackAsync(CancellationToken cancellationToken = default)
 {
 if (_currentTransaction != null)
 {
 await _currentTransaction.RollbackAsync(cancellationToken);
 await _currentTransaction.DisposeAsync();
 _currentTransaction = null;
 }
 }

 public async ValueTask DisposeAsync()
 {
 if (_currentTransaction != null)
 {
 await _currentTransaction.DisposeAsync();
 _currentTransaction = null;
 }
 await _context.DisposeAsync();
 }
}