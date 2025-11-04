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
 return View();
 }
}
