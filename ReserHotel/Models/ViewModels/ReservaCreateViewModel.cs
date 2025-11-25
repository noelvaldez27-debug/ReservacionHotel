using System.ComponentModel.DataAnnotations;
using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.ViewModels;

public class ReservaCreateViewModel
{
 // Datos del huésped
 [Required]
 [RegularExpression(@"^\d{8}$", ErrorMessage = "Documento debe tener exactamente 8 dígitos.")]
 public string Documento { get; set; } = string.Empty;
 [Required]
 public string NombreCompleto { get; set; } = string.Empty;
 [RegularExpression(@"^\d{9}$", ErrorMessage = "Teléfono debe tener exactamente 9 dígitos.")]
 public string? Telefono { get; set; }

 // Datos de la reserva
 [Required]
 [DataType(DataType.Date)]
 public DateTime FechaEntrada { get; set; }
 [Required]
 [DataType(DataType.Date)]
 [DateGreaterThan(nameof(FechaEntrada), ErrorMessage = "La salida debe ser posterior a la entrada.")]
 public DateTime FechaSalida { get; set; }
 [Required]
 public int HabitacionId { get; set; }
 public int? HotelId { get; set; }

 // Servicios adicionales (se validarán contra los disponibles del hotel)
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

// Atributo de validación simple para comparar fechas en el ViewModel
public sealed class DateGreaterThanAttribute : ValidationAttribute
{
 public string OtherProperty { get; }
 public DateGreaterThanAttribute(string otherProperty) => OtherProperty = otherProperty;
 protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
 {
 var otherProp = validationContext.ObjectType.GetProperty(OtherProperty);
 if (otherProp == null) return ValidationResult.Success;
 var otherVal = otherProp.GetValue(validationContext.ObjectInstance) as DateTime?;
 var current = value as DateTime?;
 if (current.HasValue && otherVal.HasValue && current.Value <= otherVal.Value)
 return new ValidationResult(ErrorMessage ?? $"{validationContext.MemberName} debe ser mayor que {OtherProperty}");
 return ValidationResult.Success;
 }
}