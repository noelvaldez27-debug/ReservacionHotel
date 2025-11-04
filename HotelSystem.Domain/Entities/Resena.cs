namespace HotelSystem.Domain.Entities;

public class Resena
{
 public int Id { get; set; }
 public int ReservaId { get; set; }
 public Reserva Reserva { get; set; } = null!;
 public int HuespedId { get; set; }
 public Huesped Huesped { get; set; } = null!;
 public int HabitacionId { get; set; }
 public Habitacion Habitacion { get; set; } = null!;
 public int Calificacion { get; set; } //1-5
 public string? Comentario { get; set; }
 public string? FotosJson { get; set; } // URLs o base64
 public string? EstadoLimpieza { get; set; }
 public string? EstadoConfort { get; set; }
 public DateTime Fecha { get; set; }
}