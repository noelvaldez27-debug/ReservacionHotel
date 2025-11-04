namespace HotelSystem.Domain.Entities;

public enum Temporada
{
 Baja =1,
 Alta =2
}

public class TarifaHabitacion
{
 public int Id { get; set; }
 public TipoHabitacion TipoHabitacion { get; set; }
 public decimal PrecioBase { get; set; }
 public Temporada Temporada { get; set; }
 public decimal VariacionPorcentaje { get; set; }

 public int HotelId { get; set; }
 public Hotel Hotel { get; set; } = null!;
}