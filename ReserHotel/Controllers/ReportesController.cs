using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ReserHotel.Controllers;

public class ReportesController : Controller
{
 private readonly IUnitOfWork _uow;
 public ReportesController(IUnitOfWork uow) { _uow = uow; }

 [HttpGet]
 public async Task<IActionResult> OcupacionPorMes(CancellationToken ct)
 {
 var reservas = await _uow.Reservas.GetAll(ct);
 var grouped = reservas
 .GroupBy(r => new { r.FechaEntrada.Year, r.FechaEntrada.Month })
 .Select(g => new { Mes = new DateTime(g.Key.Year, g.Key.Month,1), Ocupacion = g.Count() })
 .OrderBy(x => x.Mes)
 .ToList();
 return Json(grouped);
 }

 [HttpGet]
 public async Task<IActionResult> HabitacionesMasReservadas(CancellationToken ct)
 {
 var detalles = await _uow.DetallesReserva.GetAll(ct);
 var grouped = detalles
 .GroupBy(d => d.HabitacionId)
 .Select(g => new { HabitacionId = g.Key, Cantidad = g.Count() })
 .OrderByDescending(x => x.Cantidad)
 .Take(10)
 .ToList();
 return Json(grouped);
 }

 [HttpGet]
 public async Task<IActionResult> ServiciosPopulares(CancellationToken ct)
 {
 var rs = await _uow.ReservaServicios.GetAll(ct);
 var grouped = rs
 .GroupBy(x => x.ServicioId)
 .Select(g => new { ServicioId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
 .OrderByDescending(x => x.Cantidad)
 .ToList();
 return Json(grouped);
 }

 [HttpGet]
 public async Task<IActionResult> IngresoPorTemporada(CancellationToken ct)
 {
 var reservas = await _uow.Reservas.GetAll(ct);
 var facturas = await _uow.Facturas.GetAll(ct);
 var data = reservas.Join(facturas, r => r.Id, f => f.ReservaId, (r, f) => new { r, f })
 .GroupBy(x => GetTemporada(x.r.FechaEntrada))
 .Select(g => new { Temporada = g.Key.ToString(), Ingreso = g.Sum(x => x.f.MontoTotal) })
 .ToList();
 return Json(data);
 }

 private static Temporada GetTemporada(DateTime date)
 => (date.Month is 1 or 7 or 8 or 12) ? Temporada.Alta : Temporada.Baja;
}
