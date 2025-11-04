namespace HotelSystem.Domain.Entities;

public enum EstadoPago
{
 Pendiente =1,
 Pagado =2,
 Reembolsado =3
}

public class Factura
{
 public int Id { get; set; }
 public int ReservaId { get; set; }
 public Reserva Reserva { get; set; } = null!;
 public decimal MontoTotal { get; set; }
 public EstadoPago EstadoPago { get; set; }
 public DateTime? FechaPago { get; set; }
 public string? MetodoPago { get; set; }

 public ICollection<TransaccionPago> Transacciones { get; set; } = new HashSet<TransaccionPago>();
}