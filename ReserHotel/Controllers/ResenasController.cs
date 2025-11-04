using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ReserHotel.Controllers;

public class ResenasController : Controller
{
 private readonly IUnitOfWork _uow;
 public ResenasController(IUnitOfWork uow) { _uow = uow; }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateResena(int reservaId, int calificacion, string? comentario, string? fotosJson, string? estadoLimpieza, string? estadoConfort, CancellationToken ct)
 {
 if (calificacion <1 || calificacion >5) return BadRequest("Calificación inválida");
 var reserva = await _uow.Reservas.GetByIdAsync(reservaId, ct);
 if (reserva == null || reserva.Estado != EstadoReserva.Completada) return BadRequest("Reserva inválida o no completada");
 var detalle = (await _uow.DetallesReserva.GetAll(ct)).FirstOrDefault(d => d.ReservaId == reserva.Id);
 if (detalle == null) return BadRequest("Reserva sin detalle");
 var resena = new Resena
 {
 ReservaId = reserva.Id,
 HuespedId = reserva.ClienteId,
 HabitacionId = detalle.HabitacionId,
 Calificacion = calificacion,
 Comentario = comentario,
 FotosJson = fotosJson,
 EstadoLimpieza = estadoLimpieza,
 EstadoConfort = estadoConfort,
 Fecha = DateTime.UtcNow
 };
 await _uow.BeginTransactionAsync(ct);
 try
 {
 await _uow.Resenas.AddAsync(resena, ct);
 await _uow.CommitAsync(ct);
 }
 catch
 {
 await _uow.RollbackAsync(ct);
 throw;
 }
 TempData["Success"] = "Gracias por tu reseña";
 return RedirectToAction("Search", "Reservas");
 }
}
