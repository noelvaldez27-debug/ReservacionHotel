namespace HotelSystem.Domain.Entities;

public class ReservaServicio
{
 public int Id { get; set; }
 public int ReservaId { get; set; }
 public Reserva Reserva { get; set; } = null!;
 public int ServicioId { get; set; }
 public ServicioAdicional Servicio { get; set; } = null!;
 public int Cantidad { get; set; }
 public decimal PrecioUnitario { get; set; }
}