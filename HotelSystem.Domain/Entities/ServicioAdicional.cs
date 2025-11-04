namespace HotelSystem.Domain.Entities;

public enum NombreServicio
{
 Desayuno =1,
 Spa =2,
 Estacionamiento =3,
 LateCheckout =4
}

public class ServicioAdicional
{
 public int Id { get; set; }
 public NombreServicio Nombre { get; set; }
 public decimal Precio { get; set; }
 public string? Descripcion { get; set; }

 public int HotelId { get; set; }
 public Hotel Hotel { get; set; } = null!;

 public ICollection<ReservaServicio> Reservas { get; set; } = new HashSet<ReservaServicio>();
}