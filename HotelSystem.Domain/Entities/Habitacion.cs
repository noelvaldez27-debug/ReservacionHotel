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

 public int HotelId { get; set; }
 public Hotel Hotel { get; set; } = null!;

 public ICollection<DetalleReserva> DetallesReserva { get; set; } = new HashSet<DetalleReserva>();
}