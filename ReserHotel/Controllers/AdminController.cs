using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ReserHotel.Controllers;

public class AdminController : Controller
{
 private readonly IUnitOfWork _uow;
 public AdminController(IUnitOfWork uow) { _uow = uow; }

 public async Task<IActionResult> Dashboard(CancellationToken ct)
 {
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 var facturas = (await _uow.Facturas.GetAll(ct)).ToList();
 var resenas = (await _uow.Resenas.GetAll(ct)).ToList();
 var ocupadas = reservas.Count(r => r.Estado is EstadoReserva.Confirmada or EstadoReserva.Completada);
 var ocupacion = reservas.Count ==0 ?0 : (double)ocupadas / reservas.Count *100.0;
 var ingresos = facturas.Sum(f => f.MontoTotal);
 var promedioResenas = resenas.Any() ? resenas.Average(r => r.Calificacion) :0.0;
 ViewBag.Ocupacion = Math.Round(ocupacion,1);
 ViewBag.Ingresos = ingresos;
 ViewBag.ResenasPromedio = Math.Round(promedioResenas,2);
 ViewBag.ReservasCount = reservas.Count;
 return View();
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> WipeReservas(CancellationToken ct)
 {
 try
 {
 //1) Eliminar reseñas asociadas a reservas
 var resenas = (await _uow.Resenas.GetAll(ct)).ToList();
 foreach (var r in resenas) _uow.Resenas.Remove(r);
 //2) Eliminar todas las reservas (cascada borrará Detalles, Servicios, Facturas y Transacciones)
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 foreach (var r in reservas) _uow.Reservas.Remove(r);

 await _uow.CommitAsync(ct); // sin transacción explícita, usa la de SaveChanges
 TempData["Success"] = "Se eliminaron todas las reservas y datos relacionados.";
 }
 catch (Exception ex)
 {
 TempData["Error"] = "No se pudo limpiar las reservas: " + ex.Message;
 }
 return RedirectToAction(nameof(Dashboard));
 }
}
