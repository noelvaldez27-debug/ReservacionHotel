namespace HotelSystem.Domain.Entities;

public class DetalleReserva
{
 public int Id { get; set; }
 public int HabitacionId { get; set; }
 public Habitacion Habitacion { get; set; } = null!;
 public int ReservaId { get; set; }
 public Reserva Reserva { get; set; } = null!;
 public int CantidadNoches { get; set; }
 public decimal PrecioTotal { get; set; }
 public decimal? DescuentoAplicado { get; set; }
}