namespace HotelSystem.Domain.Entities;

public enum EstadoReserva
{
 Pendiente =1,
 Confirmada =2,
 Cancelada =3,
 Completada =4
}

public class Reserva
{
 public int Id { get; set; }
 public string NumeroReserva { get; set; } = string.Empty;
 public DateTime FechaReserva { get; set; } // fecha/hora en que se registró la reserva
 public DateTime FechaEntrada { get; set; }
 public DateTime FechaSalida { get; set; }
 public EstadoReserva Estado { get; set; }
 public DateTime? CheckInAt { get; set; }
 public DateTime? CheckOutAt { get; set; }
 public string? CodigoAcceso { get; set; }

 public int ClienteId { get; set; } // Huesped
 public Huesped Cliente { get; set; } = null!;

 public ICollection<DetalleReserva> Detalles { get; set; } = new HashSet<DetalleReserva>();
 public ICollection<ReservaServicio> Servicios { get; set; } = new HashSet<ReservaServicio>();
 public Factura? Factura { get; set; }
}