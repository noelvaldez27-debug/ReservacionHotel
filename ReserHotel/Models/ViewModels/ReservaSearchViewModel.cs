using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.ViewModels;

public class ReservaSearchFilters
{
 public DateTime? FechaEntrada { get; set; }
 public DateTime? FechaSalida { get; set; }
 public string? NombreCompleto { get; set; }
 public string? Documento { get; set; }
 public string? Pais { get; set; }
 public TipoHabitacion? Tipo { get; set; }
 public bool Desayuno { get; set; }
 public bool Spa { get; set; }
 public bool Estacionamiento { get; set; }
 public bool LateCheckout { get; set; }
 public int? HotelId { get; set; } // nuevo para seleccionar hotel
}

public class ServicioConPrecio
{
 public string Nombre { get; set; } = string.Empty;
 public decimal Precio { get; set; }
}

public class ReservaSearchResultItem
{
 public Habitacion Habitacion { get; set; } = null!;
 public Hotel Hotel { get; set; } = null!;
 public int Noches { get; set; }
 public decimal PrecioPorNochePromedio { get; set; }
 public decimal PrecioTotal { get; set; }
 public IReadOnlyList<string> Gallery { get; set; } = Array.Empty<string>();
 public IReadOnlyList<string> AmenitiesIncluidas { get; set; } = Array.Empty<string>();
 public IReadOnlyList<ServicioConPrecio> ServiciosAdicionales { get; set; } = Array.Empty<ServicioConPrecio>();
}

public class OccupiedRoomItem
{
 public Habitacion Habitacion { get; set; } = null!;
 public Hotel Hotel { get; set; } = null!;
 public DateTime DisponibleDesde { get; set; }
}

public class ReservaSearchViewModel
{
 public ReservaSearchFilters Filtros { get; set; } = new();
 public List<ReservaSearchResultItem> Resultados { get; set; } = new();
 public List<OccupiedRoomItem> Ocupadas { get; set; } = new();
 public List<Hotel> Hoteles { get; set; } = new();
 public List<Habitacion> HabitacionesDisponiblesSelect { get; set; } = new();
 public List<ServicioConPrecio> ServiciosHeader { get; set; } = new();
 // Precio promedio por noche para cabecera (habitacion de referencia)
 public decimal HeaderPrecioPorNoche { get; set; }
 public int HeaderNoches { get; set; }
 public decimal HeaderSubtotalHabitacion => HeaderPrecioPorNoche * HeaderNoches;
}