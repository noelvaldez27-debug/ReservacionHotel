using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ReserHotel.Models.ViewModels;

namespace ReserHotel.Controllers;

public class ReservasController : Controller
{
 private readonly IUnitOfWork _uow;
 private const int HoraLimiteCheckout =11; //11:00

 public ReservasController(IUnitOfWork uow)
 {
 _uow = uow;
 }

 [HttpGet]
 public async Task<IActionResult> Search([FromQuery] ReservaSearchFilters filtros, CancellationToken ct)
 {
 var vm = new ReservaSearchViewModel { Filtros = filtros };

 // Validación fechas
 if (filtros.FechaEntrada.HasValue && filtros.FechaSalida.HasValue)
 {
 if (filtros.FechaSalida <= filtros.FechaEntrada)
 ModelState.AddModelError("Filtros.FechaSalida", "La fecha de salida debe ser mayor que la de entrada.");
 }

 // Datos base (AsNoTracking en repositorio)
 var hoteles = (await _uow.Hoteles.GetAll(ct)).ToList();
 var habitaciones = (await _uow.Habitaciones.GetAll(ct)).ToList();
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 var detalles = (await _uow.DetallesReserva.GetAll(ct)).ToList();
 var tarifas = (await _uow.TarifasHabitacion.GetAll(ct)).ToList();

 // Filtros por hotel o ubicación
 if (filtros.HotelId.HasValue)
 {
 habitaciones = habitaciones.Where(h => h.HotelId == filtros.HotelId.Value).ToList();
 hoteles = hoteles.Where(h => h.Id == filtros.HotelId.Value).ToList();
 }
 else if (!string.IsNullOrWhiteSpace(filtros.Ubicacion))
 {
 var ubic = filtros.Ubicacion.Trim().ToLowerInvariant();
 var hotelIds = hoteles.Where(h => (h.Ubicacion ?? string.Empty).ToLowerInvariant().Contains(ubic))
 .Select(h => h.Id)
 .ToHashSet();
 habitaciones = habitaciones.Where(h => hotelIds.Contains(h.HotelId)).ToList();
 hoteles = hoteles.Where(h => hotelIds.Contains(h.Id)).ToList();
 }

 // Filtro por capacidad (cantidad de huéspedes)
 if (filtros.CantidadHuespedes.HasValue && filtros.CantidadHuespedes.Value >0)
 habitaciones = habitaciones.Where(h => h.Capacidad >= filtros.CantidadHuespedes.Value).ToList();

 // Filtro por comodidades
 if (!string.IsNullOrWhiteSpace(filtros.Comodidades))
 {
 var tokens = filtros.Comodidades.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
 .Select(t => t.ToLowerInvariant()).ToList();
 if (tokens.Count >0)
 {
 habitaciones = habitaciones.Where(h =>
 {
 var a = (h.Amenidades ?? string.Empty).ToLowerInvariant();
 return tokens.All(t => a.Contains(t));
 }).ToList();
 }
 }

 // Disponibilidad y precios
 if (filtros.FechaEntrada.HasValue && filtros.FechaSalida.HasValue && ModelState.IsValid)
 {
 var fe = filtros.FechaEntrada.Value.Date;
 var fs = filtros.FechaSalida.Value.Date;
 var noches = (int)Math.Ceiling((fs - fe).TotalDays);
 if (noches <1)
 {
 ModelState.AddModelError("Filtros.FechaSalida", "El rango debe ser de al menos una noche.");
 }
 else
 {
 // Índices auxiliares
 var hotelIndex = hoteles.ToDictionary(h => h.Id, h => h);
 var reservasIndex = reservas.ToDictionary(r => r.Id, r => r);
 var detallesPorHabitacion = detalles.GroupBy(d => d.HabitacionId).ToDictionary(g => g.Key, g => g.Select(x => x).ToList());

 foreach (var hab in habitaciones)
 {
 // Stock:1 si libre,0 si ocupada por alguna reserva que se solape
 var reservadas = detallesPorHabitacion.TryGetValue(hab.Id, out var dets)
 ? dets.Any(d =>
 {
 var r = reservasIndex[d.ReservaId];
 return Overlaps(fe, fs, r.FechaEntrada.Date, r.FechaSalida.Date);
 })
 : false;
 if (reservadas) continue; // no disponible

 // Precio por noche según temporada del día
 var nightly = new List<decimal>(noches);
 for (var day = fe; day < fs; day = day.AddDays(1))
 {
 var temporada = GetTemporada(day);
 var t = tarifas.FirstOrDefault(t => t.HotelId == hab.HotelId && t.TipoHabitacion == hab.Tipo && t.Temporada == temporada);
 if (t == null)
 {
 // Sin tarifa definida: ignorar habitación
 nightly.Clear();
 break;
 }
 var precioNoche = t.PrecioBase * (1 + (t.VariacionPorcentaje /100m));
 nightly.Add(precioNoche);
 }
 if (nightly.Count != noches) continue;
 var precioTotal = nightly.Sum();
 var precioProm = nightly.Average();

 // filtro por precio máximo
 if (filtros.PrecioMax.HasValue && precioProm > filtros.PrecioMax.Value)
 continue;

 vm.Resultados.Add(new ReservaSearchResultItem
 {
 Habitacion = hab,
 Hotel = hotelIndex[hab.HotelId],
 Noches = noches,
 PrecioPorNochePromedio = decimal.Round(precioProm,2),
 PrecioTotal = decimal.Round(precioTotal,2),
 Gallery = GetGalleryForType(hab.Tipo)
 });
 }
 }
 }

 return View(vm);
 }

 [HttpPost]
 public async Task<IActionResult> CheckIn(int id, CancellationToken ct)
 {
 var reserva = await _uow.Reservas.GetByIdAsync(id, ct);
 if (reserva == null) return NotFound();
 if (reserva.Estado != EstadoReserva.Pendiente)
 return BadRequest("Reserva no está en estado pendiente.");

 reserva.Estado = EstadoReserva.Confirmada;
 reserva.CheckInAt = DateTime.UtcNow;
 reserva.CodigoAcceso = GenerateAccessCode();
 _uow.Reservas.Update(reserva);
 await _uow.Reservas.SaveChangesAsync(ct);

 _ = Task.Run(() => Console.WriteLine($"Check-in confirmado. Código de acceso: {reserva.CodigoAcceso}"));
 TempData["Success"] = "Check-in realizado";
 return RedirectToAction("Search");
 }

 [HttpPost]
 public async Task<IActionResult> CheckOut(int id, CancellationToken ct)
 {
 var reserva = await _uow.Reservas.GetByIdAsync(id, ct);
 if (reserva == null) return NotFound();
 if (reserva.Estado != EstadoReserva.Confirmada)
 return BadRequest("La reserva no está confirmada.");

 var ahora = DateTime.UtcNow;
 reserva.CheckOutAt = ahora;
 reserva.Estado = EstadoReserva.Completada;

 // Cargos extras por hora límite
 var horaLimite = new DateTime(reserva.FechaSalida.Year, reserva.FechaSalida.Month, reserva.FechaSalida.Day, HoraLimiteCheckout,0,0, DateTimeKind.Utc);
 var extras =0m;
 if (ahora > horaLimite)
 {
 // Si tardó después de la hora límite, cargo extra fijo (ej:20% de una noche)
 var detalle = (await _uow.DetallesReserva.GetAll(ct)).FirstOrDefault(d => d.ReservaId == reserva.Id);
 var tarifaNocheProm = (detalle?.PrecioTotal ??0) / Math.Max(1, detalle?.CantidadNoches ??1);
 extras = Math.Round(tarifaNocheProm *0.2m,2);
 }

 // Si tenía LateCheckout contratado, reembolso del extra
 var late = (await _uow.ReservaServicios.GetAll(ct)).Join(await _uow.ServiciosAdicionales.GetAll(ct), rs => rs.ServicioId, s => s.Id, (rs, s) => new { rs, s })
 .Any(x => x.rs.ReservaId == reserva.Id && x.s.Nombre == NombreServicio.LateCheckout);
 if (late && extras >0) extras =0m;

 // Actualizar factura y registrar transacción
 var factura = (await _uow.Facturas.GetAll(ct)).FirstOrDefault(f => f.ReservaId == reserva.Id);
 if (factura != null && extras >0)
 {
 factura.MontoTotal += extras;
 _uow.Facturas.Update(factura);
 await _uow.Facturas.SaveChangesAsync(ct);
 }
 _uow.Reservas.Update(reserva);
 await _uow.Reservas.SaveChangesAsync(ct);

 _ = Task.Run(() => Console.WriteLine($"Check-out completado. Extras: {extras:C}"));
 TempData["Success"] = "Check-out realizado";
 return RedirectToAction("Search");
 }

 [HttpPost]
 public async Task<IActionResult> Cancel(int id, CancellationToken ct)
 {
 var reserva = await _uow.Reservas.GetByIdAsync(id, ct);
 if (reserva == null) return NotFound();
 if (reserva.Estado == EstadoReserva.Cancelada)
 return BadRequest("La reserva ya está cancelada.");

 var ahora = DateTime.UtcNow;
 var horasAnticipacion = (reserva.FechaEntrada - ahora).TotalHours;
 decimal porcentaje =0m;
 if (horasAnticipacion >=48) porcentaje =1m;
 else if (horasAnticipacion >=24) porcentaje =0.5m;
 else porcentaje =0m;

 reserva.Estado = EstadoReserva.Cancelada;
 _uow.Reservas.Update(reserva);

 var factura = (await _uow.Facturas.GetAll(ct)).FirstOrDefault(f => f.ReservaId == reserva.Id);
 if (factura != null)
 {
 var reembolso = Math.Round(factura.MontoTotal * porcentaje,2);
 if (reembolso >0)
 {
 factura.EstadoPago = EstadoPago.Reembolsado;
 factura.MontoTotal -= reembolso;
 _uow.Facturas.Update(factura);
 await _uow.Facturas.SaveChangesAsync(ct);
 }
 }
 await _uow.Reservas.SaveChangesAsync(ct);

 _ = Task.Run(() => Console.WriteLine($"Reserva cancelada. Reembolso aplicado: {porcentaje:P0}"));
 TempData["Success"] = "Reserva cancelada";
 return RedirectToAction("Search");
 }

 private static string GenerateAccessCode() => Random.Shared.Next(100000,999999).ToString();

 private static string GenerateNumeroReserva()
 => $"R-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

 private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
 {
 return aStart < bEnd && bStart < aEnd;
 }

 private static Temporada GetTemporada(DateTime date)
 {
 return (date.Month is 1 or 7 or 8 or 12) ? Temporada.Alta : Temporada.Baja;
 }

 private static IReadOnlyList<string> GetGalleryForType(TipoHabitacion tipo)
 => tipo switch
 {
 TipoHabitacion.Simple => new[] { "/img/hab/simple1.jpg", "/img/hab/simple2.jpg", "/img/hab/simple3.jpg" },
 TipoHabitacion.Doble => new[] { "/img/hab/doble1.jpg", "/img/hab/doble2.jpg", "/img/hab/doble3.jpg" },
 TipoHabitacion.Suite => new[] { "/img/hab/suite1.jpg", "/img/hab/suite2.jpg", "/img/hab/suite3.jpg" },
 _ => Array.Empty<string>()
 };
}