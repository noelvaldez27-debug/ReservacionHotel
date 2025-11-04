using System.ComponentModel.DataAnnotations;
using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.DTOs;

public class TarifaHabitacionDto
{
 public int Id { get; set; }
 [Required]
 public TipoHabitacion TipoHabitacion { get; set; }
 [Required]
 [Range(0,9999999)]
 public decimal PrecioBase { get; set; }
 [Required]
 public Temporada Temporada { get; set; }
 [Range(0,100)]
 public decimal VariacionPorcentaje { get; set; }
 [Required]
 public int HotelId { get; set; }
}