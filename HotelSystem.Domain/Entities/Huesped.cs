namespace HotelSystem.Domain.Entities;

public class Huesped
{
 public int Id { get; set; }
 public string Documento { get; set; } = string.Empty;
 public string NombreCompleto { get; set; } = string.Empty;
 public string? Email { get; set; }
 public string? Telefono { get; set; }
 public string Pais { get; set; } = string.Empty;
 public DateTime FechaRegistro { get; set; }
 public int PuntosAcumulados { get; set; }

 public ICollection<Reserva> Reservas { get; set; } = new HashSet<Reserva>();
}