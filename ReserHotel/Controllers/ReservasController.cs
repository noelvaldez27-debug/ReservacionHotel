using HotelSystem.Domain.Entities;
using HotelSystem.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ReserHotel.Models.ViewModels;

namespace ReserHotel.Controllers;

public class ReservasController : Controller
{
 private readonly IUnitOfWork _uow;
 private const int HoraLimiteCheckout =11; //11:00
 private const decimal RecargoJacuzziPorcentaje =0.15m; // +15% por Jacuzzi

 public ReservasController(IUnitOfWork uow)
 {
 _uow = uow;
 }

 // Helpers para amenities base
 private static IReadOnlyList<string> GetBaseAmenitiesFor(Habitacion h)
 {
 // Todas incluyen Netflix, algunas tienen Jacuzzi y/o WiFi
 var list = new List<string> { "Netflix" };
 var a = (h.Amenidades ?? string.Empty).ToLowerInvariant();
 if (a.Contains("wifi")) list.Add("WiFi");
 if (a.Contains("jacuzzi") || a.Contains("jacuzi")) list.Add("Jacuzzi");
 return list;
 }

 private static bool TieneJacuzzi(Habitacion h)
 {
 var a = (h.Amenidades ?? string.Empty).ToLowerInvariant();
 return a.Contains("jacuzzi") || a.Contains("jacuzi");
 }
 private static bool TieneWifi(Habitacion h)
 {
 var a = (h.Amenidades ?? string.Empty).ToLowerInvariant();
 return a.Contains("wifi");
 }

 [HttpGet]
 public async Task<IActionResult> Create(int? habitacionId, DateTime? fechaEntrada, DateTime? fechaSalida, CancellationToken ct)
 {
 var habitaciones = (await _uow.Habitaciones.GetAll(ct)).ToList();
 var hoteles = (await _uow.Hoteles.GetAll(ct)).ToList();
 var vm = new ReservaCreateViewModel
 {
 Habitaciones = habitaciones,
 Hoteles = hoteles,
 HabitacionId = habitacionId ??0,
 FechaEntrada = fechaEntrada?.Date ?? DateTime.Today,
 FechaSalida = fechaSalida?.Date ?? DateTime.Today.AddDays(1)
 };
 return View(vm);
 }

 [HttpGet]
 public async Task<IActionResult> GetDisponibles(DateTime fechaEntrada, DateTime fechaSalida, int? hotelId, int? cantidadHuespedes, bool soloJacuzzi = false, bool soloWifi = false, CancellationToken ct = default)
 {
 if (fechaSalida <= fechaEntrada)
 return BadRequest("Rango de fechas inválido.");

 var hoteles = (await _uow.Hoteles.GetAll(ct)).ToList();
 var habitaciones = (await _uow.Habitaciones.GetAll(ct)).ToList();
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 var detalles = (await _uow.DetallesReserva.GetAll(ct)).ToList();
 var tarifas = (await _uow.TarifasHabitacion.GetAll(ct)).ToList();
 var servicios = (await _uow.ServiciosAdicionales.GetAll(ct)).ToList();

 if (hotelId.HasValue)
 {
 habitaciones = habitaciones.Where(h => h.HotelId == hotelId.Value).ToList();
 }
 if (cantidadHuespedes.HasValue && cantidadHuespedes.Value >0)
 {
 habitaciones = habitaciones.Where(h => h.Capacidad >= cantidadHuespedes.Value).ToList();
 }
 if (soloJacuzzi) habitaciones = habitaciones.Where(TieneJacuzzi).ToList();
 if (soloWifi) habitaciones = habitaciones.Where(TieneWifi).ToList();

 var reservasPorHab = detalles.GroupBy(d => d.HabitacionId)
 .ToDictionary(g => g.Key, g => g.Select(d => reservas.First(r => r.Id == d.ReservaId)).ToList());
 var serviciosPorHotel = servicios.GroupBy(s => s.HotelId).ToDictionary(g => g.Key, g => g.ToList());

 var fe = fechaEntrada.Date; var fs = fechaSalida.Date;
 var noches = (int)Math.Ceiling((fs - fe).TotalDays);
 var result = new List<object>();

 foreach (var hab in habitaciones)
 {
 var lista = reservasPorHab.TryGetValue(hab.Id, out var l) ? l : new List<Reserva>();
 var solapa = lista.Any(r => r.Estado != EstadoReserva.Cancelada && fe < r.FechaSalida.Date && r.FechaEntrada.Date < fs);
 if (solapa) continue;

 var nightly = new List<decimal>(noches);
 for (var day = fe; day < fs; day = day.AddDays(1))
 {
 var esAlta = (day.Month ==1 || day.Month ==7 || day.Month ==8 || day.Month ==12);
 var temporada = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == hab.HotelId && t.TipoHabitacion == hab.Tipo && t.Temporada == temporada);
 if (t == null) { nightly.Clear(); break; }
 var precioBase = t.PrecioBase * (1 + (t.VariacionPorcentaje /100m));
 if (TieneJacuzzi(hab)) precioBase *= (1 + RecargoJacuzziPorcentaje);
 nightly.Add(precioBase);
 }
 if (nightly.Count != noches) continue;
 var precioTotal = nightly.Sum();
 var precioProm = nightly.Average();

 var hotel = hoteles.First(h => h.Id == hab.HotelId);
 var servs = serviciosPorHotel.TryGetValue(hab.HotelId, out var listaServ) ? listaServ : new List<ServicioAdicional>();
 var serviciosConPrecio = servs.Select(s => new { nombre = s.Nombre.ToString(), precio = s.Precio }).ToList();

 // flags de amenities
 var amenRaw = (hab.Amenidades ?? string.Empty).ToLowerInvariant();
 var hasWifi = amenRaw.Contains("wifi");
 var hasJacuzzi = amenRaw.Contains("jacuzzi") || amenRaw.Contains("jacuzi");

 result.Add(new
 {
 id = hab.Id,
 numero = hab.Numero,
 tipo = hab.Tipo.ToString(),
 hotelId = hotel.Id,
 hotel = hotel.Nombre,
 precioPromedio = Math.Round(precioProm,2),
 precioTotal = Math.Round(precioTotal,2),
 hasWifi,
 hasJacuzzi,
 servicios = serviciosConPrecio
 });
 }

 return Ok(result);
 }

 [HttpPost]
 public async Task<IActionResult> Create(ReservaCreateViewModel model, CancellationToken ct)
 {
 // Validaciones básicas
 if (model.FechaSalida <= model.FechaEntrada)
 ModelState.AddModelError(nameof(model.FechaSalida), "La salida debe ser posterior a la entrada.");
 if (!ModelState.IsValid)
 {
 model.Habitaciones = await _uow.Habitaciones.GetAll(ct);
 model.Hoteles = await _uow.Hoteles.GetAll(ct);
 return View(model);
 }

 // Validar que la habitación exista
 var habitacion = await _uow.Habitaciones.GetByIdAsync(model.HabitacionId, ct);
 if (habitacion == null)
 {
 ModelState.AddModelError(nameof(model.HabitacionId), "La habitación seleccionada no existe.");
 model.Habitaciones = await _uow.Habitaciones.GetAll(ct);
 model.Hoteles = await _uow.Hoteles.GetAll(ct);
 return View(model);
 }

 // Restringir servicios a los disponibles del hotel
 var serviciosHotel = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => s.HotelId == habitacion.HotelId).ToList();
 bool Has(NombreServicio n) => serviciosHotel.Any(s => s.Nombre == n);
 if (model.Desayuno && !Has(NombreServicio.Desayuno)) ModelState.AddModelError("Servicios", "El hotel no ofrece Desayuno.");
 if (model.Spa && !Has(NombreServicio.Spa)) ModelState.AddModelError("Servicios", "El hotel no ofrece Spa.");
 if (model.Estacionamiento && !Has(NombreServicio.Estacionamiento)) ModelState.AddModelError("Servicios", "El hotel no ofrece Estacionamiento.");
 if (model.LateCheckout && !Has(NombreServicio.LateCheckout)) ModelState.AddModelError("Servicios", "El hotel no ofrece Late Checkout.");
 if (!ModelState.IsValid)
 {
 model.Habitaciones = await _uow.Habitaciones.GetAll(ct);
 model.Hoteles = await _uow.Hoteles.GetAll(ct);
 return View(model);
 }

 // Evitar solapamientos de reservas de esa habitación
 var reservas = await _uow.Reservas.GetAll(ct);
 var detalles = await _uow.DetallesReserva.GetAll(ct);
 var resPorHab = detalles.Where(d => d.HabitacionId == habitacion.Id)
 .Join(reservas, d => d.ReservaId, r => r.Id, (d, r) => r);
 bool overlap = resPorHab.Any(r => r.Estado != EstadoReserva.Cancelada &&
 r.FechaEntrada.Date < model.FechaSalida.Date && model.FechaEntrada.Date < r.FechaSalida.Date);
 if (overlap)
 {
 ModelState.AddModelError(string.Empty, "La habitación está ocupada en el rango seleccionado.");
 model.Habitaciones = await _uow.Habitaciones.GetAll(ct);
 model.Hoteles = await _uow.Hoteles.GetAll(ct);
 return View(model);
 }

 // Buscar/crear huésped por Documento
 var huesped = (await _uow.Huespedes.GetAll(ct)).FirstOrDefault(h => h.Documento == model.Documento);
 if (huesped == null)
 {
 huesped = new Huesped
 {
 Documento = model.Documento,
 NombreCompleto = model.NombreCompleto,
 Email = model.Email,
 Telefono = model.Telefono,
 Pais = model.Pais,
 FechaRegistro = DateTime.UtcNow
 };
 await _uow.Huespedes.AddAsync(huesped, ct);
 await _uow.Huespedes.SaveChangesAsync(ct);
 }

 // Calcular precio de habitación por noche según tarifas
 var tarifas = await _uow.TarifasHabitacion.GetAll(ct);
 decimal subtotalHab =0m;
 for (var d = model.FechaEntrada.Date; d < model.FechaSalida.Date; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temp = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == habitacion.HotelId && t.TipoHabitacion == habitacion.Tipo && t.Temporada == temp);
 if (t == null)
 {
 ModelState.AddModelError(string.Empty, "No hay tarifa configurada para esta habitación en las fechas seleccionadas.");
 model.Habitaciones = await _uow.Habitaciones.GetAll(ct);
 model.Hoteles = await _uow.Hoteles.GetAll(ct);
 return View(model);
 }
 var precioBase = t.PrecioBase * (1 + t.VariacionPorcentaje /100m);
 if (TieneJacuzzi(habitacion)) precioBase *= (1 + RecargoJacuzziPorcentaje);
 subtotalHab += precioBase;
 }

 // Crear reserva, detalle y factura en transacción
 await _uow.BeginTransactionAsync(ct);
 try
 {
 var reserva = new Reserva
 {
 NumeroReserva = GenerateNumeroReserva(),
 FechaReserva = DateTime.UtcNow,
 FechaEntrada = model.FechaEntrada.Date,
 FechaSalida = model.FechaSalida.Date,
 Estado = EstadoReserva.Pendiente, // ocupada provisionalmente
 ClienteId = huesped.Id
 };
 await _uow.Reservas.AddAsync(reserva, ct);
 await _uow.Reservas.SaveChangesAsync(ct);

 var detalle = new DetalleReserva
 {
 ReservaId = reserva.Id,
 HabitacionId = habitacion.Id,
 CantidadNoches = (int)(model.FechaSalida.Date - model.FechaEntrada.Date).TotalDays,
 PrecioTotal = subtotalHab
 };
 await _uow.DetallesReserva.AddAsync(detalle, ct);
 await _uow.DetallesReserva.SaveChangesAsync(ct);

 // Agregar servicios seleccionados que existan en el hotel
 var serviciosSeleccionados = new List<NombreServicio>();
 if (model.Desayuno) serviciosSeleccionados.Add(NombreServicio.Desayuno);
 if (model.Spa) serviciosSeleccionados.Add(NombreServicio.Spa);
 if (model.Estacionamiento) serviciosSeleccionados.Add(NombreServicio.Estacionamiento);
 if (model.LateCheckout) serviciosSeleccionados.Add(NombreServicio.LateCheckout);
 foreach (var s in serviciosHotel.Where(s => serviciosSeleccionados.Contains(s.Nombre)))
 {
 await _uow.ReservaServicios.AddAsync(new ReservaServicio
 {
 ReservaId = reserva.Id,
 ServicioId = s.Id,
 Cantidad =1,
 PrecioUnitario = s.Precio
 }, ct);
 }
 await _uow.ReservaServicios.SaveChangesAsync(ct);

 // Factura inicial
 var subtotalServ = serviciosHotel.Where(s => serviciosSeleccionados.Contains(s.Nombre)).Sum(s => s.Precio);
 await _uow.Facturas.AddAsync(new Factura
 {
 ReservaId = reserva.Id,
 MontoTotal = subtotalHab + subtotalServ,
 EstadoPago = EstadoPago.Pendiente
 }, ct);
 await _uow.Facturas.SaveChangesAsync(ct);

 await _uow.CommitAsync(ct);
 TempData["Success"] = "Reserva creada. Número: " + reserva.NumeroReserva;
 return RedirectToAction("Search", new { FechaEntrada = model.FechaEntrada.ToString("yyyy-MM-dd"), FechaSalida = model.FechaSalida.ToString("yyyy-MM-dd") });
 }
 catch
 {
 await _uow.RollbackAsync(ct);
 throw;
 }
 }

 [HttpGet]
 public async Task<IActionResult> Search([FromQuery] ReservaSearchFilters filtros, CancellationToken ct)
 {
 if (!filtros.FechaEntrada.HasValue) filtros.FechaEntrada = DateTime.Today;
 if (!filtros.FechaSalida.HasValue) filtros.FechaSalida = DateTime.Today.AddDays(1);
 if (filtros.FechaSalida <= filtros.FechaEntrada)
 ModelState.AddModelError("Filtros.FechaSalida", "La fecha de salida debe ser mayor que la de entrada.");

 var vm = new ReservaSearchViewModel { Filtros = filtros };
 var hoteles = (await _uow.Hoteles.GetAll(ct)).ToList();
 vm.Hoteles = hoteles;
 var habitaciones = (await _uow.Habitaciones.GetAll(ct)).ToList();
 if (filtros.HotelId.HasValue)
 habitaciones = habitaciones.Where(h => h.HotelId == filtros.HotelId.Value).ToList();
 if (filtros.Tipo.HasValue)
 habitaciones = habitaciones.Where(h => h.Tipo == filtros.Tipo.Value).ToList();
 vm.HabitacionesDisponiblesSelect = habitaciones;
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 var detalles = (await _uow.DetallesReserva.GetAll(ct)).ToList();
 var tarifas = (await _uow.TarifasHabitacion.GetAll(ct)).ToList();
 var servicios = (await _uow.ServiciosAdicionales.GetAll(ct)).ToList();
 if (filtros.HotelId.HasValue)
 vm.ServiciosHeader = servicios.Where(s => s.HotelId == filtros.HotelId.Value)
 .Select(s => new ServicioConPrecio { Nombre = s.Nombre.ToString(), Precio = s.Precio }).ToList();
 else
 {
 // Si no se seleccionó hotel, mostrar listado genérico (una vez por tipo) con los precios base
 vm.ServiciosHeader = servicios
 .GroupBy(s => s.Nombre)
 .Select(g => new ServicioConPrecio { Nombre = g.Key.ToString(), Precio = g.First().Precio })
 .OrderBy(x => x.Nombre)
 .ToList();
 }
 var reservasPorHab = detalles.GroupBy(d => d.HabitacionId).ToDictionary(g => g.Key, g => g.Select(d => reservas.First(r => r.Id == d.ReservaId)).ToList());
 var serviciosPorHotel = servicios.GroupBy(s => s.HotelId).ToDictionary(g => g.Key, g => g.ToList());
 if (!ModelState.IsValid) return View(vm);
 var fe = filtros.FechaEntrada!.Value.Date; var fs = filtros.FechaSalida!.Value.Date; var noches = (int)Math.Ceiling((fs - fe).TotalDays);
 if (noches <1)
 {
 ModelState.AddModelError("Filtros.FechaSalida", "El rango debe ser de al menos una noche.");
 return View(vm);
 }
 foreach (var hab in habitaciones)
 {
 var lista = reservasPorHab.TryGetValue(hab.Id, out var l) ? l : new List<Reserva>();
 var solapa = lista.Any(r => r.Estado != EstadoReserva.Cancelada && fe < r.FechaSalida.Date && r.FechaEntrada.Date < fs);
 if (solapa)
 {
 var proximaSalida = lista.Where(r => r.FechaSalida.Date >= fe).OrderBy(r => r.FechaSalida).FirstOrDefault()?.FechaSalida.Date ?? fe;
 vm.Ocupadas.Add(new OccupiedRoomItem { Habitacion = hab, Hotel = hoteles.First(h => h.Id == hab.HotelId), DisponibleDesde = proximaSalida });
 continue;
 }
 var nightly = new List<decimal>(noches);
 for (var day = fe; day < fs; day = day.AddDays(1))
 {
 var esAlta = (day.Month ==1 || day.Month ==7 || day.Month ==8 || day.Month ==12);
 var temporada = esAlta ? Temporada.Alta : Temporada.Baja;
 var t = tarifas.FirstOrDefault(t => t.HotelId == hab.HotelId && t.TipoHabitacion == hab.Tipo && t.Temporada == temporada);
 if (t == null) { nightly.Clear(); break; }
 var precioBase = t.PrecioBase * (1 + (t.VariacionPorcentaje /100m));
 if (TieneJacuzzi(hab)) precioBase *= (1 + RecargoJacuzziPorcentaje);
 nightly.Add(precioBase);
 }
 if (nightly.Count != noches) continue;
 var precioTotal = nightly.Sum(); var precioProm = nightly.Average();
 var servs = serviciosPorHotel.TryGetValue(hab.HotelId, out var listaServ) ? listaServ : new List<ServicioAdicional>();
 var serviciosConPrecio = servs.Select(s => new ServicioConPrecio { Nombre = s.Nombre.ToString(), Precio = s.Precio }).ToList();
 vm.Resultados.Add(new ReservaSearchResultItem
 {
 Habitacion = hab,
 Hotel = hoteles.First(h => h.Id == hab.HotelId),
 Noches = noches,
 PrecioPorNochePromedio = decimal.Round(precioProm,2),
 PrecioTotal = decimal.Round(precioTotal,2),
 Gallery = GetGalleryForType(hab.Tipo),
 AmenitiesIncluidas = GetBaseAmenitiesFor(hab),
 ServiciosAdicionales = serviciosConPrecio
 });
 }
 if (vm.Resultados.Any())
 {
 vm.HeaderPrecioPorNoche = vm.Resultados.First().PrecioPorNochePromedio;
 vm.HeaderNoches = vm.Resultados.First().Noches;
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

 // Registrar datos del huésped dentro de la habitación automáticamente
 var huesped = (await _uow.Huespedes.GetAll(ct)).FirstOrDefault(h => h.Id == reserva.ClienteId);
 var detalle = (await _uow.DetallesReserva.GetAll(ct)).FirstOrDefault(d => d.ReservaId == reserva.Id);
 if (detalle != null)
 {
 var habitacion = await _uow.Habitaciones.GetByIdAsync(detalle.HabitacionId, ct);
 if (habitacion != null && huesped != null)
 {
 habitacion.RegistradoNombreCompleto = huesped.NombreCompleto;
 habitacion.RegistradoDni = huesped.Documento;
 habitacion.RefEntrada = reserva.FechaEntrada;
 habitacion.RefSalida = reserva.FechaSalida;
 habitacion.RefNoches = detalle.CantidadNoches;
 habitacion.RefSubtotalHabitacion = detalle.PrecioTotal;
 // cálculo rápido de servicios contratados
 var serviciosReserva = (await _uow.ReservaServicios.GetAll(ct)).Where(rs => rs.ReservaId == reserva.Id).ToList();
 var serviciosAd = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => serviciosReserva.Any(rs => rs.ServicioId == s.Id)).ToList();
 habitacion.RefSubtotalServicios = serviciosAd.Sum(s => s.Precio);
 habitacion.RefTotal = (habitacion.RefSubtotalHabitacion ??0) + (habitacion.RefSubtotalServicios ??0);
 habitacion.RefServiciosJson = string.Join(",", serviciosAd.Select(s => s.Nombre.ToString()));
 _uow.Habitaciones.Update(habitacion);
 await _uow.Habitaciones.SaveChangesAsync(ct);
 }
 }

 _ = Task.Run(() => Console.WriteLine($"Check-in confirmado. Código de acceso: {reserva.CodigoAcceso}"));
 TempData["Success"] = "Check-in realizado";
 return RedirectToAction("Search", new { FechaEntrada = reserva.FechaEntrada.ToString("yyyy-MM-dd"), FechaSalida = reserva.FechaSalida.ToString("yyyy-MM-dd") });
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
 decimal porcentaje = horasAnticipacion >=48 ?1m : horasAnticipacion >=24 ?0.5m :0m;
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
 return RedirectToAction("Search", new { FechaEntrada = reserva.FechaEntrada.ToString("yyyy-MM-dd"), FechaSalida = reserva.FechaSalida.ToString("yyyy-MM-dd") });
 }

 [HttpGet]
 public IActionResult QuickReserve()
 {
 return RedirectToAction("Search");
 }

 [HttpPost]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> QuickReserve(QuickReserveDto dto, CancellationToken ct)
 {
 if (dto.FechaEntrada == default) dto.FechaEntrada = DateTime.Today;
 if (dto.FechaSalida == default) dto.FechaSalida = DateTime.Today.AddDays(1);
 if (dto.FechaSalida <= dto.FechaEntrada)
 {
 TempData["Error"] = "La fecha de salida debe ser posterior a la de entrada";
 return RedirectToAction("Search", new { FechaEntrada = dto.FechaEntrada.ToString("yyyy-MM-dd"), FechaSalida = dto.FechaSalida.ToString("yyyy-MM-dd") });
 }
 var habitacion = await _uow.Habitaciones.GetByIdAsync(dto.HabitacionId, ct);
 if (habitacion == null)
 {
 TempData["Error"] = "Habitación no encontrada";
 return RedirectToAction("Search");
 }
 // Chequear solapamiento
 var reservas = await _uow.Reservas.GetAll(ct);
 var detalles = await _uow.DetallesReserva.GetAll(ct);
 var reservasHab = detalles.Where(d => d.HabitacionId == habitacion.Id)
 .Join(reservas, d => d.ReservaId, r => r.Id, (d, r) => r)
 .Where(r => r.Estado != EstadoReserva.Cancelada);
 bool overlap = reservasHab.Any(r => dto.FechaEntrada.Date < r.FechaSalida.Date && r.FechaEntrada.Date < dto.FechaSalida.Date);
 if (overlap)
 {
 TempData["Error"] = "La habitación está ocupada en el rango seleccionado";
 return RedirectToAction("Search", new { FechaEntrada = dto.FechaEntrada.ToString("yyyy-MM-dd"), FechaSalida = dto.FechaSalida.ToString("yyyy-MM-dd") });
 }
 // Huesped
 var huesped = (await _uow.Huespedes.GetAll(ct)).FirstOrDefault(h => h.Documento == dto.Documento);
 if (huesped == null)
 {
 huesped = new Huesped
 {
 Documento = dto.Documento ?? Guid.NewGuid().ToString("N").Substring(0,8),
 NombreCompleto = dto.NombreCompleto ?? "Invitado",
 Email = dto.Email,
 Telefono = dto.Telefono,
 Pais = dto.Pais ?? "-",
 FechaRegistro = DateTime.UtcNow
 };
 await _uow.Huespedes.AddAsync(huesped, ct);
 await _uow.Huespedes.SaveChangesAsync(ct);
 }
 // Calcular subtotal habitación
 var tarifas = await _uow.TarifasHabitacion.GetAll(ct);
 decimal subtotalHab =0m;
 for (var d = dto.FechaEntrada.Date; d < dto.FechaSalida.Date; d = d.AddDays(1))
 {
 var esAlta = (d.Month ==1 || d.Month ==7 || d.Month ==8 || d.Month ==12);
 var temporada = esAlta ? Temporada.Alta : Temporada.Baja;
 var tarifa = tarifas.FirstOrDefault(t => t.HotelId == habitacion.HotelId && t.TipoHabitacion == habitacion.Tipo && t.Temporada == temporada);
 if (tarifa == null)
 {
 TempData["Error"] = "No hay tarifa disponible";
 return RedirectToAction("Search");
 }
 var precioBase = tarifa.PrecioBase * (1 + tarifa.VariacionPorcentaje /100m);
 if (TieneJacuzzi(habitacion)) precioBase *= (1 + RecargoJacuzziPorcentaje);
 subtotalHab += precioBase;
 }
 var serviciosHotel = (await _uow.ServiciosAdicionales.GetAll(ct)).Where(s => s.HotelId == habitacion.HotelId).ToList();
 var serviciosSeleccionados = new List<NombreServicio>();
 if (dto.Desayuno) serviciosSeleccionados.Add(NombreServicio.Desayuno);
 if (dto.Spa) serviciosSeleccionados.Add(NombreServicio.Spa);
 if (dto.Estacionamiento) serviciosSeleccionados.Add(NombreServicio.Estacionamiento);
 if (dto.LateCheckout) serviciosSeleccionados.Add(NombreServicio.LateCheckout);
 await _uow.BeginTransactionAsync(ct);
 try
 {
 var reserva = new Reserva
 {
 NumeroReserva = GenerateNumeroReserva(),
 FechaReserva = DateTime.UtcNow,
 FechaEntrada = dto.FechaEntrada.Date,
 FechaSalida = dto.FechaSalida.Date,
 Estado = EstadoReserva.Pendiente,
 ClienteId = huesped.Id
 };
 await _uow.Reservas.AddAsync(reserva, ct);
 await _uow.Reservas.SaveChangesAsync(ct);
 await _uow.DetallesReserva.AddAsync(new DetalleReserva
 {
 ReservaId = reserva.Id,
 HabitacionId = habitacion.Id,
 CantidadNoches = (int)(dto.FechaSalida.Date - dto.FechaEntrada.Date).TotalDays,
 PrecioTotal = subtotalHab
 }, ct);
 await _uow.DetallesReserva.SaveChangesAsync(ct);
 foreach (var s in serviciosHotel.Where(s => serviciosSeleccionados.Contains(s.Nombre)))
 {
 await _uow.ReservaServicios.AddAsync(new ReservaServicio { ReservaId = reserva.Id, ServicioId = s.Id, Cantidad =1, PrecioUnitario = s.Precio }, ct);
 }
 await _uow.ReservaServicios.SaveChangesAsync(ct);
 var subtotalServ = serviciosHotel.Where(s => serviciosSeleccionados.Contains(s.Nombre)).Sum(s => s.Precio);
 await _uow.Facturas.AddAsync(new Factura { ReservaId = reserva.Id, MontoTotal = subtotalHab + subtotalServ, EstadoPago = EstadoPago.Pendiente }, ct);
 await _uow.Facturas.SaveChangesAsync(ct);
 await _uow.CommitAsync(ct);
 TempData["Success"] = "Reserva rápida creada";
 return RedirectToAction("Index");
 }
 catch
 {
 await _uow.RollbackAsync(ct);
 TempData["Error"] = "Error al crear reserva";
 return RedirectToAction("Search");
 }
 }

 private static string GenerateAccessCode() => Random.Shared.Next(100000,999999).ToString();
 private static string GenerateNumeroReserva() => $"R-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
 private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) => aStart < bEnd && bStart < aEnd;
 private static Temporada GetTemporada(DateTime date)
 {
 var esAlta = (date.Month ==1 || date.Month ==7 || date.Month ==8 || date.Month ==12);
 return esAlta ? Temporada.Alta : Temporada.Baja;
 }
 private static IReadOnlyList<string> GetGalleryForType(TipoHabitacion tipo) => tipo switch
 {
 TipoHabitacion.Simple => new[] { "/img/hab/simple1.jpg", "/img/hab/simple2.jpg", "/img/hab/simple3.jpg" },
 TipoHabitacion.Doble => new[] { "/img/hab/doble1.jpg", "/img/hab/doble2.jpg", "/img/hab/doble3.jpg" },
 TipoHabitacion.Suite => new[] { "/img/hab/suite1.jpg", "/img/hab/suite2.jpg", "/img/hab/suite3.jpg" },
 _ => Array.Empty<string>()
 };

 [HttpGet]
 public async Task<IActionResult> Index(CancellationToken ct)
 {
 var reservas = (await _uow.Reservas.GetAll(ct)).ToList();
 var detalles = (await _uow.DetallesReserva.GetAll(ct)).ToList();
 var habitaciones = (await _uow.Habitaciones.GetAll(ct)).ToList();
 var hoteles = (await _uow.Hoteles.GetAll(ct)).ToList();
 var huespedes = (await _uow.Huespedes.GetAll(ct)).ToList();
 var facturas = (await _uow.Facturas.GetAll(ct)).ToList();
 var list = reservas.OrderByDescending(r => r.FechaReserva).Select(r =>
 {
 var det = detalles.FirstOrDefault(d => d.ReservaId == r.Id);
 var hab = det != null ? habitaciones.FirstOrDefault(h => h.Id == det.HabitacionId) : null;
 var hotel = hab != null ? hoteles.FirstOrDefault(h => h.Id == hab.HotelId) : null;
 var huésped = huespedes.FirstOrDefault(h => h.Id == r.ClienteId);
 var total = facturas.FirstOrDefault(f => f.ReservaId == r.Id)?.MontoTotal ??0m;
 var noches = det?.CantidadNoches ??0;
 return new ReservaListItem { Reserva = r, Habitacion = hab, Hotel = hotel, Huesped = huésped, Total = total, Noches = noches };
 }).ToList();
 return View(list);
 }
}

public class QuickReserveDto
{
 public int HabitacionId { get; set; }
 public DateTime FechaEntrada { get; set; }
 public DateTime FechaSalida { get; set; }
 public string? Documento { get; set; }
 public string? NombreCompleto { get; set; }
 public string? Email { get; set; }
 public string? Telefono { get; set; }
 public string? Pais { get; set; }
 public bool Desayuno { get; set; }
 public bool Spa { get; set; }
 public bool Estacionamiento { get; set; }
 public bool LateCheckout { get; set; }
}

public class ReservaListItem
{
 public Reserva Reserva { get; set; } = null!;
 public Habitacion? Habitacion { get; set; }
 public Hotel? Hotel { get; set; }
 public Huesped? Huesped { get; set; }
 public int Noches { get; set; }
 public decimal Total { get; set; }
}