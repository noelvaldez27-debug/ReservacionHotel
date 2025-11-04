namespace HotelSystem.Domain.Entities;

public class TransaccionPago
{
 public int Id { get; set; }
 public int FacturaId { get; set; }
 public Factura Factura { get; set; } = null!;
 public DateTime Fecha { get; set; }
 public decimal Monto { get; set; }
 public string Tipo { get; set; } = string.Empty; // Cargo, Reembolso
 public string? Referencia { get; set; }
}