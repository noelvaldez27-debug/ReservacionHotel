using System.ComponentModel.DataAnnotations;
using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.DTOs;

public class HabitacionDto
{
 public int Id { get; set; }

 [Required]
 [Range(1, int.MaxValue, ErrorMessage = "El número debe ser mayor a0")] 
 public int Numero { get; set; }

 [Required]
 [Range(0,200, ErrorMessage = "Piso inválido")] 
 public int Piso { get; set; }

 [Required]
 public TipoHabitacion Tipo { get; set; }

 [Required]
 [Range(1,20, ErrorMessage = "Capacidad inválida")] 
 public int Capacidad { get; set; }

 [StringLength(500)]
 public string? Amenidades { get; set; }

 [Required]
 [Display(Name = "Hotel")]
 public int HotelId { get; set; }
}