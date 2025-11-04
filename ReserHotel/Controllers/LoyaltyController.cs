using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ReserHotel.Controllers;

public class LoyaltyController : Controller
{
 private readonly IUnitOfWork _uow;
 public LoyaltyController(IUnitOfWork uow) { _uow = uow; }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> ApplyPoints(int huespedId, int puntosUsar, CancellationToken ct)
 {
 var huesped = await _uow.Huespedes.GetByIdAsync(huespedId, ct);
 if (huesped == null) return NotFound();
 if (puntosUsar <=0 || puntosUsar > huesped.PuntosAcumulados)
 return BadRequest("Puntos inválidos");
 huesped.PuntosAcumulados -= puntosUsar;
 _uow.Huespedes.Update(huesped);
 await _uow.Huespedes.SaveChangesAsync(ct);
 TempData["Success"] = $"Se aplicaron {puntosUsar} puntos";
 return RedirectToAction("Search", "Reservas");
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> AddPointsByReserva(int reservaId, CancellationToken ct)
 {
 var reserva = await _uow.Reservas.GetByIdAsync(reservaId, ct);
 if (reserva == null) return NotFound();
 var factura = (await _uow.Facturas.GetAll(ct)).FirstOrDefault(f => f.ReservaId == reserva.Id);
 if (factura == null) return BadRequest("Factura no encontrada");
 var huesped = await _uow.Huespedes.GetByIdAsync(reserva.ClienteId, ct);
 if (huesped == null) return NotFound();
 huesped.PuntosAcumulados += (int)Math.Round(factura.MontoTotal);
 _uow.Huespedes.Update(huesped);
 await _uow.Huespedes.SaveChangesAsync(ct);
 TempData["Success"] = "Puntos acumulados";
 return RedirectToAction("Search", "Reservas");
 }
}
