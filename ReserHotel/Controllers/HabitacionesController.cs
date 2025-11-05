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
 _uow.Habitaciones.Remove(entity);
 await _uow.CommitAsync(ct);
 TempData["Success"] = "Habitación eliminada";
 return RedirectToAction(nameof(Index));
 }

 public async Task<IActionResult> Details(int id, CancellationToken ct)
 {
 var entity = await _uow.Habitaciones.GetByIdAsync(id, ct);
 if (entity == null) return NotFound();
 // Galería simple basada en tipo
 var gallery = GetGalleryForType(entity.Tipo);
 ViewBag.Gallery = gallery;
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
