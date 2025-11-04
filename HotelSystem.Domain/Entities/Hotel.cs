namespace HotelSystem.Domain.Entities;

public class Hotel
{
 public int Id { get; set; }
 public string Nombre { get; set; } = string.Empty;
 public string Ubicacion { get; set; } = string.Empty;
 public int Estrellas { get; set; }
 public string? Descripcion { get; set; }

 public ICollection<Habitacion> Habitaciones { get; set; } = new HashSet<Habitacion>();
 public ICollection<TarifaHabitacion> TarifasHabitacion { get; set; } = new HashSet<TarifaHabitacion>();
}