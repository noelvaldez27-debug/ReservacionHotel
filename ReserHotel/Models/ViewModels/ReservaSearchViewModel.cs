using HotelSystem.Domain.Entities;

namespace ReserHotel.Models.ViewModels;

public class ReservaSearchFilters
{
 public DateTime? FechaEntrada { get; set; }
 public DateTime? FechaSalida { get; set; }
 public int? CantidadHuespedes { get; set; }
 public int? HotelId { get; set; }
 public string? Ubicacion { get; set; }
 public decimal? PrecioMax { get; set; }
 public string? Comodidades { get; set; }
 public bool SoloJacuzzi { get; set; }
 public bool SoloWifi { get; set; }
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
}