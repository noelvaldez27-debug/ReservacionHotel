using AutoMapper;
using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReserHotel.Models.DTOs;
using X.PagedList;

namespace ReserHotel.Controllers;

public class HabitacionesController : Controller
{
 private readonly IUnitOfWork _uow;
 private readonly IMapper _mapper;

 public HabitacionesController(IUnitOfWork uow, IMapper mapper)
 {
 _uow = uow;
 _mapper = mapper;
 }

 public async Task<IActionResult> Index(TipoHabitacion? tipo, int? piso, int page =1, int pageSize =10, CancellationToken ct = default)
 {
 var all = await _uow
 .Habitaciones
 .GetAll(ct); // Generic repo usa AsNoTracking internamente

 var q = all.AsQueryable();
 if (tipo.HasValue)
 q = q.Where(h => h.Tipo == tipo.Value);
 if (piso.HasValue)
 q = q.Where(h => h.Piso == piso.Value);

 var ordered = q.OrderBy(h => h.HotelId).ThenBy(h => h.Numero).ToList();

 // Construir mapa HabitacionId -> Nombre del huésped (última reserva no cancelada)
 var detalles = await _uow.DetallesReserva.GetAll(ct);
 var reservas = await _uow.Reservas.GetAll(ct);
 var huespedes = await _uow.Huespedes.GetAll(ct);
 var nombres = new Dictionary<int,string>();
 foreach (var hab in ordered)
 {
 var ultimaReserva = detalles
 .Where(d => d.HabitacionId == hab.Id)
 .Join(reservas, d => d.ReservaId, r => r.Id, (d, r) => r)
 .Where(r => r.Estado != EstadoReserva.Cancelada)
 .OrderByDescending(r => r.FechaEntrada)
 .FirstOrDefault();
 if (ultimaReserva != null)
 {
 var guest = huespedes.FirstOrDefault(h => h.Id == ultimaReserva.ClienteId);
 nombres[hab.Id] = guest?.NombreCompleto ?? "";
 }
 else
 {
 nombres[hab.Id] = "";
 }
 }
 ViewBag.NombresPorHab = nombres;

 var paged = ordered.ToPagedList(page, pageSize);
 ViewBag.Tipo = tipo;
 ViewBag.Piso = piso;
 return View(paged);
 }

 public async Task<IActionResult> Create(CancellationToken ct)
 {
 await PopulateCombosAsync(ct);
 return View(new HabitacionDto());
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> Create(HabitacionDto dto, CancellationToken ct)
 {
 if (!ModelState.IsValid)
 {
 await PopulateCombosAsync(ct);
 return View(dto);
 }
 // Validación de unicidad (HotelId, Numero)
 var existentes = await _uow.Habitaciones.GetAll(ct);
 if (existentes.Any(h => h.HotelId == dto.HotelId && h.Numero == dto.Numero))
 {
 ModelState.AddModelError(nameof(dto.Numero), "Ya existe una habitación con ese número en el hotel.");
 await PopulateCombosAsync(ct);
 return View(dto);
 }
 var entity = _mapper.Map<Habitacion>(dto);
 entity.RegistradoNombreCompleto = dto.RegistradoNombreCompleto;
 entity.RegistradoDni = dto.RegistradoDni;
 // ---- Persistir cotización referencia ----
 var feStr = Request.Form["CotzEntrada"].ToString();
 var fsStr = Request.Form["CotzSalida"].ToString();
 if (DateTime.TryParse(feStr, out var feRef) && DateTime.TryParse(fsStr, out var fsRef) && fsRef > feRef)
 {
 entity.RefEntrada = feRef.Date;
 entity.RefSalida = fsRef.Date;
 entity.RefNoches = (int)(entity.RefSalida.Value - entity.RefEntrada.Value).TotalDays;
 var tarifas = await _uow.TarifasHabitacion.GetAll(ct);
 decimal subtotalHab =0m;
 for (var d = entity.RefEntrada.Value; d < entity.RefSalida.Value; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == dto.HotelId && t.TipoHabitacion == dto.Tipo && t.Temporada == temp);
 if (t == null) continue;
 var precioBase = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 var ame = (dto.Amenidades ?? string.Empty).ToLowerInvariant();
 if (ame.Contains("jacuzzi") || ame.Contains("jacuzi")) precioBase *=1.15m;
 subtotalHab += precioBase;
 }
 entity.RefSubtotalHabitacion = subtotalHab;
 var serviciosHotel = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => s.HotelId == dto.HotelId).ToList();
 decimal subtotalServ =0m; var listaServicios = new List<string>();
 foreach (var s in serviciosHotel)
 {
 var key = "svc_" + s.Nombre.ToString();
 if (Request.Form.ContainsKey(key))
 {
 subtotalServ += s.Precio;
 listaServicios.Add(s.Nombre + ":" + s.Precio.ToString());
 }
 }
 entity.RefSubtotalServicios = subtotalServ;
 entity.RefTotal = subtotalHab + subtotalServ;
 entity.RefServiciosJson = string.Join(";", listaServicios);
 }
 // -----------------------------------------
 await _uow.Habitaciones.AddAsync(entity, ct);
 await _uow.CommitAsync(ct);
 TempData["Success"] = "Habitación creada";
 return RedirectToAction(nameof(Index));
 }

 public async Task<IActionResult> Edit(int id, CancellationToken ct)
 {
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 var dto = _mapper.Map<HabitacionDto>(entity);
 await PopulateCombosAsync(ct);
 return View(dto);
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> Edit(int id, HabitacionDto dto, CancellationToken ct)
 {
 if (id != dto.Id) return BadRequest();
 if (!ModelState.IsValid)
 {
 await PopulateCombosAsync(ct);
 return View(dto);
 }
 // Validación de unicidad (HotelId, Numero) excluyendo la propia
 var existentes = await _uow.Habitaciones.GetAll(ct);
 if (existentes.Any(h => h.HotelId == dto.HotelId && h.Numero == dto.Numero && h.Id != dto.Id))
 {
 ModelState.AddModelError(nameof(dto.Numero), "Ya existe una habitación con ese número en el hotel.");
 await PopulateCombosAsync(ct);
 return View(dto);
 }
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 _mapper.Map(dto, entity);
 entity.RegistradoNombreCompleto = dto.RegistradoNombreCompleto;
 entity.RegistradoDni = dto.RegistradoDni;
 // Recalcular referencia si se enviaron fechas
 var feStr = Request.Form["CotzEntrada"].ToString();
 var fsStr = Request.Form["CotzSalida"].ToString();
 if (DateTime.TryParse(feStr, out var feRef) && DateTime.TryParse(fsStr, out var fsRef) && fsRef > feRef)
 {
 entity.RefEntrada = feRef.Date;
 entity.RefSalida = fsRef.Date;
 entity.RefNoches = (int)(entity.RefSalida.Value - entity.RefEntrada.Value).TotalDays;
 var tarifas = await _uow.TarifasHabitacion.GetAll(ct);
 decimal subtotalHab =0m;
 for (var d = entity.RefEntrada.Value; d < entity.RefSalida.Value; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == dto.HotelId && t.TipoHabitacion == dto.Tipo && t.Temporada == temp);
 if (t == null) continue;
 var precioBase = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 var ame = (dto.Amenidades ?? string.Empty).ToLowerInvariant();
 if (ame.Contains("jacuzzi") || ame.Contains("jacuzi")) precioBase *=1.15m;
 subtotalHab += precioBase;
 }
 entity.RefSubtotalHabitacion = subtotalHab;
 var serviciosHotel = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => s.HotelId == dto.HotelId).ToList();
 decimal subtotalServ =0m; var listaServicios = new List<string>();
 foreach (var s in serviciosHotel)
 {
 var key = "svc_" + s.Nombre.ToString();
 if (Request.Form.ContainsKey(key))
 {
 subtotalServ += s.Precio;
 listaServicios.Add(s.Nombre + ":" + s.Precio.ToString());
 }
 }
 entity.RefSubtotalServicios = subtotalServ;
 entity.RefTotal = subtotalHab + subtotalServ;
 entity.RefServiciosJson = string.Join(";", listaServicios);
 }
 _uow.Habitaciones.Update(entity);
 await _uow.CommitAsync(ct);
 TempData["Success"] = "Habitación actualizada";
 return RedirectToAction(nameof(Index));
 }

 public async Task<IActionResult> Delete(int id, CancellationToken ct)
 {
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 await PopulateCombosAsync(ct);
 return View(_mapper.Map<HabitacionDto>(entity));
 }

 [HttpPost, ActionName("Delete")]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
 {
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 // Verificar si la habitación tiene detalles de reserva asociados
 var detalles = await _uow.DetallesReserva.GetAll(ct);
 if (detalles.Any(d => d.HabitacionId == id))
 {
 TempData["Error"] = "No se puede eliminar la habitación porque tiene reservas asociadas. Cancele o elimine las reservas primero.";
 return RedirectToAction(nameof(Index));
 }
 _uow.Habitaciones.Remove(entity);
 await _uow.CommitAsync(ct);
 TempData["Success"] = "Habitación eliminada";
 return RedirectToAction(nameof(Index));
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> DeleteFirstTwoPages(TipoHabitacion? tipo, int? piso, int pageSize =10, CancellationToken ct = default)
 {
 // Obtener primeras dos páginas con el mismo orden que Index
 var all = await _uow.Habitaciones.GetAll(ct);
 var q = all.AsQueryable();
 if (tipo.HasValue) q = q.Where(h => h.Tipo == tipo.Value);
 if (piso.HasValue) q = q.Where(h => h.Piso == piso.Value);
 var ordered = q.OrderBy(h => h.HotelId).ThenBy(h => h.Numero).ToList();
 var targetIds = ordered.Take(pageSize *2).Select(h => h.Id).ToList();
 if (targetIds.Count ==0)
 {
 TempData["Success"] = "No hay habitaciones para eliminar.";
 return RedirectToAction(nameof(Index), new { tipo, piso });
 }

 await _uow.BeginTransactionAsync(ct);
 try
 {
 // Eliminar reservas relacionadas (cascadará Detalles/Servicios/Factura)
 var detalles = (await _uow.DetallesReserva.GetAll(ct)).Where(d => targetIds.Contains(d.HabitacionId)).ToList();
 var reservaIds = detalles.Select(d => d.ReservaId).Distinct().ToList();
 if (reservaIds.Count >0)
 {
 var reservas = (await _uow.Reservas.GetAll(ct)).Where(r => reservaIds.Contains(r.Id)).ToList();
 foreach (var r in reservas) _uow.Reservas.Remove(r);
 }
 // Eliminar reseñas asociadas a esas habitaciones (por si no está en cascada)
 var resenas = (await _uow.Resenas.GetAll(ct)).Where(r => targetIds.Contains(r.HabitacionId)).ToList();
 foreach (var rs in resenas) _uow.Resenas.Remove(rs);

 // Eliminar habitaciones
 var habs = (await _uow.Habitaciones.GetAll(ct)).Where(h => targetIds.Contains(h.Id)).ToList();
 foreach (var h in habs) _uow.Habitaciones.Remove(h);

 await _uow.CommitAsync(ct);
 TempData["Success"] = $"Se eliminaron {habs.Count} habitaciones (primeras2 páginas).";
 }
 catch (Exception ex)
 {
 await _uow.RollbackAsync(ct);
 TempData["Error"] = "No se pudieron eliminar las habitaciones: " + ex.Message;
 }
 return RedirectToAction(nameof(Index), new { tipo, piso });
 }

 public async Task<IActionResult> Details(int id, CancellationToken ct)
 {
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 var gallery = GetGalleryForType(entity.Tipo);
 ViewBag.Gallery = gallery;

 var detalles = await _uow.DetallesReserva.GetAll(ct);
 var reservas = await _uow.Reservas.GetAll(ct);
 var serviciosReserva = await _uow.ReservaServicios.GetAll(ct);
 var servicios = await _uow.ServiciosAdicionales.GetAll(ct);
 var huespedes = await _uow.Huespedes.GetAll(ct);
 var facturas = await _uow.Facturas.GetAll(ct);
 var tarifas = await _uow.TarifasHabitacion.GetAll(ct);

 var det = detalles.Where(d => d.HabitacionId == id)
 .OrderByDescending(d => d.Id)
 .FirstOrDefault();
 if (det != null)
 {
 var reserva = reservas.FirstOrDefault(r => r.Id == det.ReservaId);
 if (reserva != null && reserva.Estado != EstadoReserva.Cancelada)
 {
 var guest = huespedes.FirstOrDefault(h => h.Id == reserva.ClienteId);
 var factura = facturas.FirstOrDefault(f => f.ReservaId == reserva.Id);
 var svcs = serviciosReserva.Where(rs => rs.ReservaId == reserva.Id)
 .Join(servicios, rs => rs.ServicioId, s => s.Id, (rs, s) => new { s.Nombre, rs.Cantidad, rs.PrecioUnitario })
 .Select(x => new { Nombre = x.Nombre.ToString(), x.Cantidad, x.PrecioUnitario, Subtotal = x.PrecioUnitario * x.Cantidad })
 .ToList();

 // Per-night breakdown according to tariffs, then adjust last day to match Detalle.PrecioTotal
 var noches = new List<(DateTime fecha, decimal precio)>();
 var fe = reserva.FechaEntrada.Date; var fs = reserva.FechaSalida.Date;
 for (var d = fe; d < fs; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == entity.HotelId && t.TipoHabitacion == entity.Tipo && t.Temporada == temp);
 if (t == null) continue;
 var precio = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 var ame = (entity.Amenidades ?? string.Empty).ToLowerInvariant();
 if (ame.Contains("jacuzzi") || ame.Contains("jacuzi")) precio *=1.15m;
 noches.Add((d, Math.Round(precio,2)));
 }
 var sumaNoches = Math.Round(noches.Sum(n => n.precio),2);
 var diff = Math.Round(det.PrecioTotal - sumaNoches,2);
 if (noches.Count >0 && diff !=0)
 {
 var last = noches[^1];
 noches[^1] = (last.fecha, last.precio + diff);
 sumaNoches = Math.Round(noches.Sum(n => n.precio),2);
 }

 ViewBag.Boleta = new
 {
 HasReserva = true,
 Numero = reserva.NumeroReserva,
 Estado = reserva.Estado.ToString(),
 Entrada = fe.ToString("yyyy-MM-dd"),
 Salida = fs.ToString("yyyy-MM-dd"),
 Noches = noches.Select(n => new { Fecha = n.fecha.ToString("yyyy-MM-dd"), Precio = n.precio }).ToList(),
 Huesped = guest?.NombreCompleto,
 Documento = guest?.Documento,
 Servicios = svcs,
 SubtotalHabitacion = sumaNoches,
 TotalServicios = svcs.Sum(x => x.Subtotal),
 Total = sumaNoches + svcs.Sum(x => x.Subtotal)
 };
 }
 }
 else
 {
 // No hay reservas: usar información de referencia guardada al crear/editar
 var fe = entity.RefEntrada?.Date;
 var fs = entity.RefSalida?.Date;
 var noches = new List<(DateTime fecha, decimal precio)>();
 if (fe.HasValue && fs.HasValue && fe < fs)
 {
 for (var d = fe.Value; d < fs.Value; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == entity.HotelId && t.TipoHabitacion == entity.Tipo && t.Temporada == temp);
 if (t == null) continue;
 var precio = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 var ame = (entity.Amenidades ?? string.Empty).ToLowerInvariant();
 if (ame.Contains("jacuzzi") || ame.Contains("jacuzi")) precio *=1.15m;
 noches.Add((d, Math.Round(precio,2)));
 }
 }
 var sumaNoches = Math.Round(noches.Sum(n => n.precio),2);
 var refHab = Math.Round(entity.RefSubtotalHabitacion ??0m,2);
 var ajuste = Math.Round(refHab - sumaNoches,2);
 if (noches.Count >0 && ajuste !=0)
 {
 var last = noches[^1];
 noches[^1] = (last.fecha, last.precio + ajuste);
 sumaNoches = Math.Round(noches.Sum(n => n.precio),2);
 }

 // Servicios guardados en RefServiciosJson: nombre:precio;nombre:precio
 var serviciosRef = new List<dynamic>();
 decimal totalServ =0m;
 var raw = entity.RefServiciosJson ?? string.Empty;
 foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
 {
 var p = item.Split(':');
 if (p.Length ==2 && decimal.TryParse(p[1], out var val))
 {
 serviciosRef.Add(new { Nombre = p[0], Cantidad =1, PrecioUnitario = val, Subtotal = val });
 totalServ += val;
 }
 }
 var total = sumaNoches + totalServ;
 ViewBag.Boleta = new
 {
 HasReserva = false,
 Numero = (string?)null,
 Estado = (string?)null,
 Entrada = fe?.ToString("yyyy-MM-dd"),
 Salida = fs?.ToString("yyyy-MM-dd"),
 Noches = noches.Select(n => new { Fecha = n.fecha.ToString("yyyy-MM-dd"), Precio = n.precio }).ToList(),
 Huesped = entity.RegistradoNombreCompleto,
 Documento = entity.RegistradoDni,
 Servicios = serviciosRef,
 SubtotalHabitacion = sumaNoches,
 TotalServicios = totalServ,
 Total = total
 };
 }
 return View(entity);
 }

 [HttpGet]
 public async Task<IActionResult> ServiciosHotel(int hotelId, CancellationToken ct)
 {
 var servicios = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => s.HotelId == hotelId)
 .Select(s => new { nombre = s.Nombre.ToString(), precio = s.Precio })
 .ToList();
 return Ok(servicios);
 }

 [HttpGet]
 public async Task<IActionResult> Quote(int hotelId, TipoHabitacion tipo, DateTime fechaEntrada, DateTime fechaSalida, bool hasJacuzzi = false, CancellationToken ct = default)
 {
 if (fechaSalida <= fechaEntrada) return BadRequest("Rango inválido");
 var tarifas = (await _uow.TarifasHabitacion.GetAll(ct)).ToList();
 var fe = fechaEntrada.Date; var fs = fechaSalida.Date;
 decimal subtotal =0m; int noches =0;
 for (var d = fe; d < fs; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == hotelId && t.TipoHabitacion == tipo && t.Temporada == temp);
 if (t == null) return BadRequest("No hay tarifa para el rango");
 var precioBase = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 if (hasJacuzzi) precioBase *=1 +0.15m;
 subtotal += precioBase;
 noches++;
 }
 return Ok(new { noches, subtotal = Math.Round(subtotal,2) });
 }

 [HttpGet]
 public async Task<IActionResult> GetNumeros(int hotelId, TipoHabitacion tipo, CancellationToken ct)
 {
 // Totales:10 simples (101-110),10 dobles (201-210),5 suites (301-305)
 var habitacionesHotel = (await _uow.Habitaciones.GetAll(ct))
 .Where(h => h.HotelId == hotelId && h.Tipo == tipo)
 .ToList();
 var ocupados = habitacionesHotel.Select(h => h.Numero).OrderBy(n => n).ToList();
 var posibles = new List<int>();
 switch (tipo)
 {
 case TipoHabitacion.Simple:
 for (int n =101; n <=110; n++) posibles.Add(n);
 break;
 case TipoHabitacion.Doble:
 for (int n =201; n <=210; n++) posibles.Add(n);
 break;
 case TipoHabitacion.Suite:
 for (int n =301; n <=305; n++) posibles.Add(n);
 break;
 default:
 break;
 }
 var disponibles = posibles.Except(ocupados).OrderBy(n => n).ToList();
 return Ok(new { disponibles, ocupadas = ocupados });
 }

 private static IReadOnlyList<string> GetGalleryForType(TipoHabitacion tipo)
 => tipo switch
 {
 TipoHabitacion.Simple => new[] { "/img/hab/simple1.jpg", "/img/hab/simple2.jpg" },
 TipoHabitacion.Doble => new[] { "/img/hab/doble1.jpg", "/img/hab/doble2.jpg" },
 TipoHabitacion.Suite => new[] { "/img/hab/suite1.jpg", "/img/hab/suite2.jpg" },
 _ => Array.Empty<string>()
 };

 private async Task PopulateCombosAsync(CancellationToken ct)
 {
 var hoteles = await _uow.Hoteles.GetAll(ct);
 ViewBag.Hoteles = new SelectList(hoteles, "Id", "Nombre");
 ViewBag.Tipos = Enum.GetValues(typeof(TipoHabitacion));
 }
}
