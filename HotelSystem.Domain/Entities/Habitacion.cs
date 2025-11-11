namespace HotelSystem.Domain.Entities;

public enum TipoHabitacion
{
 Simple =1,
 Doble =2,
 Suite =3
}

public class Habitacion
{
 public int Id { get; set; }
 public int Numero { get; set; }
 public int Piso { get; set; }
 public TipoHabitacion Tipo { get; set; }
 public int Capacidad { get; set; }
 public string? Amenidades { get; set; }

 // Datos de registro de la habitación (quién la registró)
 public string? RegistradoNombreCompleto { get; set; }
 public string? RegistradoDni { get; set; }

 // Cotización de referencia guardada al crear/editar
 public DateTime? RefEntrada { get; set; }
 public DateTime? RefSalida { get; set; }
 public int? RefNoches { get; set; }
 public decimal? RefSubtotalHabitacion { get; set; }
 public decimal? RefSubtotalServicios { get; set; }
 public decimal? RefTotal { get; set; }
 public string? RefServiciosJson { get; set; }

 public int HotelId { get; set; }
 public Hotel Hotel { get; set; } = null!;

 public ICollection<DetalleReserva> DetallesReserva { get; set; } = new HashSet<DetalleReserva>();
}