using System.ComponentModel.DataAnnotations;
using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.ViewModels;

public class ReservaCreateViewModel
{
 // Datos del huésped
 [Required]
 public string Documento { get; set; } = string.Empty;
 [Required]
 public string NombreCompleto { get; set; } = string.Empty;
 [EmailAddress]
 public string? Email { get; set; }
 public string? Telefono { get; set; }
 [Required]
 public string Pais { get; set; } = string.Empty;

 // Datos de la reserva
 [Required]
 [DataType(DataType.Date)]
 public DateTime FechaEntrada { get; set; }
 [Required]
 [DataType(DataType.Date)]
 public DateTime FechaSalida { get; set; }
 [Required]
 public int HabitacionId { get; set; }
 public int? HotelId { get; set; }

 // Servicios adicionales
 public bool Desayuno { get; set; }
 public bool Spa { get; set; }
 public bool Estacionamiento { get; set; }
 public bool LateCheckout { get; set; }

 // Resumen
 public int Noches => (int)Math.Max(1, (FechaSalida.Date - FechaEntrada.Date).TotalDays);
 public decimal PrecioHabitacionPorNoche { get; set; }
 public decimal SubtotalHabitacion { get; set; }
 public decimal SubtotalServicios { get; set; }
 public decimal? Descuento { get; set; }
 public decimal Total { get; set; }

 public IEnumerable<Habitacion>? Habitaciones { get; set; }
 public IEnumerable<Hotel>? Hoteles { get; set; }
}