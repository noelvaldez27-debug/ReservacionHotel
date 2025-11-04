using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ReserHotel.Controllers;

[Authorize(Roles = "Huesped,Admin")]
public class HuespedController : Controller
{
 private readonly IUnitOfWork _uow;
 public HuespedController(IUnitOfWork uow) { _uow = uow; }

 public async Task<IActionResult> Dashboard(int huespedId, CancellationToken ct)
 {
 var huesped = await _uow.Huespedes.GetByIdAsync(huespedId, ct);
 if (huesped == null) return NotFound();
 var reservas = (await _uow.Reservas.GetAll(ct)).Where(r => r.ClienteId == huespedId).ToList();
 var activas = reservas.Where(r => r.Estado is EstadoReserva.Pendiente or EstadoReserva.Confirmada).ToList();
 var historial = reservas.Where(r => r.Estado is EstadoReserva.Completada or EstadoReserva.Cancelada).OrderByDescending(r => r.FechaEntrada).ToList();
 ViewBag.Huesped = huesped;
 ViewBag.Activas = activas;
 ViewBag.Historial = historial;
 ViewBag.Puntos = huesped.PuntosAcumulados;
 ViewBag.Proximas = reservas.Where(r => r.FechaEntrada >= DateTime.Today).OrderBy(r => r.FechaEntrada).Take(5).ToList();
 return View();
 }
}
