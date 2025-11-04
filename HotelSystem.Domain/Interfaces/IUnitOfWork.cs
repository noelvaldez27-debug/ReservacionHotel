using HotelSystem.Domain.Entities;

namespace HotelSystem.Domain.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
 IGenericRepository<Hotel> Hoteles { get; }
 IGenericRepository<Habitacion> Habitaciones { get; }
 IGenericRepository<TarifaHabitacion> TarifasHabitacion { get; }
 IGenericRepository<Huesped> Huespedes { get; }
 IGenericRepository<Reserva> Reservas { get; }
 IGenericRepository<DetalleReserva> DetallesReserva { get; }
 IGenericRepository<ServicioAdicional> ServiciosAdicionales { get; }
 IGenericRepository<ReservaServicio> ReservaServicios { get; }
 IGenericRepository<Factura> Facturas { get; }
 IGenericRepository<TransaccionPago> Transacciones { get; }
 IGenericRepository<Resena> Resenas { get; }

 Task BeginTransactionAsync(CancellationToken cancellationToken = default);
 Task CommitAsync(CancellationToken cancellationToken = default);
 Task RollbackAsync(CancellationToken cancellationToken = default);
}