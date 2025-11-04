using HotelSystem.Domain.Entities;
using HotelSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelSystem.Infrastructure.Persistence;

public class HotelDbContext : IdentityDbContext<ApplicationUser>
{
 public HotelDbContext(DbContextOptions<HotelDbContext> options) : base(options) { }

 public DbSet<Hotel> Hoteles => Set<Hotel>();
 public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
 public DbSet<TarifaHabitacion> TarifasHabitacion => Set<TarifaHabitacion>();
 public DbSet<Huesped> Huespedes => Set<Huesped>();
 public DbSet<Reserva> Reservas => Set<Reserva>();
 public DbSet<DetalleReserva> DetallesReserva => Set<DetalleReserva>();
 public DbSet<ServicioAdicional> ServiciosAdicionales => Set<ServicioAdicional>();
 public DbSet<ReservaServicio> ReservasServicios => Set<ReservaServicio>();
 public DbSet<Factura> Facturas => Set<Factura>();
 public DbSet<TransaccionPago> TransaccionesPago => Set<TransaccionPago>();
 public DbSet<Resena> Resenas => Set<Resena>();

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);
 // Hotel
 modelBuilder.Entity<Hotel>(e =>
 {
 e.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
 e.Property(p => p.Ubicacion).IsRequired().HasMaxLength(200);
 e.Property(p => p.Descripcion).HasMaxLength(1000);
 e.HasMany(h => h.Habitaciones).WithOne(r => r.Hotel).HasForeignKey(r => r.HotelId).OnDelete(DeleteBehavior.Restrict);
 e.HasMany(h => h.TarifasHabitacion).WithOne(t => t.Hotel).HasForeignKey(t => t.HotelId).OnDelete(DeleteBehavior.Cascade);
 });

 // Habitacion
 modelBuilder.Entity<Habitacion>(e =>
 {
 e.HasIndex(h => new { h.HotelId, h.Numero }).IsUnique();
 e.Property(p => p.Amenidades).HasMaxLength(500);
 });

 // TarifaHabitacion
 modelBuilder.Entity<TarifaHabitacion>(e =>
 {
 e.Property(p => p.PrecioBase).HasPrecision(18,2);
 e.Property(p => p.VariacionPorcentaje).HasPrecision(18,2);
 e.HasIndex(t => new { t.HotelId, t.TipoHabitacion, t.Temporada }).IsUnique();
 });

 // Huesped
 modelBuilder.Entity<Huesped>(e =>
 {
 e.Property(p => p.Documento).IsRequired().HasMaxLength(50);
 e.Property(p => p.NombreCompleto).IsRequired().HasMaxLength(200);
 e.Property(p => p.Email).HasMaxLength(200);
 e.Property(p => p.Telefono).HasMaxLength(50);
 e.Property(p => p.Pais).IsRequired().HasMaxLength(100);
 e.HasIndex(p => p.Documento).IsUnique();
 e.HasMany(h => h.Reservas).WithOne(r => r.Cliente).HasForeignKey(r => r.ClienteId).OnDelete(DeleteBehavior.Restrict);
 });

 // Reserva
 modelBuilder.Entity<Reserva>(e =>
 {
 e.Property(r => r.NumeroReserva).IsRequired().HasMaxLength(30);
 e.HasIndex(r => r.NumeroReserva).IsUnique();
 e.Property(r => r.CodigoAcceso).HasMaxLength(20);
 e.HasMany(r => r.Detalles).WithOne(d => d.Reserva).HasForeignKey(d => d.ReservaId).OnDelete(DeleteBehavior.Cascade);
 e.HasMany(r => r.Servicios).WithOne(rs => rs.Reserva).HasForeignKey(rs => rs.ReservaId).OnDelete(DeleteBehavior.Cascade);
 e.HasOne(r => r.Factura).WithOne(f => f.Reserva).HasForeignKey<Factura>(f => f.ReservaId).OnDelete(DeleteBehavior.Cascade);
 });

 // DetalleReserva
 modelBuilder.Entity<DetalleReserva>(e =>
 {
 e.Property(p => p.PrecioTotal).HasPrecision(18,2);
 e.Property(p => p.DescuentoAplicado).HasPrecision(18,2);
 e.HasOne(d => d.Habitacion).WithMany(h => h.DetallesReserva).HasForeignKey(d => d.HabitacionId).OnDelete(DeleteBehavior.Restrict);
 });

 // ServicioAdicional
 modelBuilder.Entity<ServicioAdicional>(e =>
 {
 e.Property(p => p.Descripcion).HasMaxLength(500);
 e.Property(p => p.Precio).HasPrecision(18,2);
 e.HasIndex(s => new { s.HotelId, s.Nombre }).IsUnique();
 e.HasMany(s => s.Reservas).WithOne(rs => rs.Servicio).HasForeignKey(rs => rs.ServicioId).OnDelete(DeleteBehavior.Restrict);
 });

 // ReservaServicio
 modelBuilder.Entity<ReservaServicio>(e =>
 {
 e.Property(p => p.PrecioUnitario).HasPrecision(18,2);
 });

 // Factura
 modelBuilder.Entity<Factura>(e =>
 {
 e.Property(f => f.MontoTotal).HasPrecision(18,2);
 e.HasMany(f => f.Transacciones).WithOne(t => t.Factura).HasForeignKey(t => t.FacturaId);
 });

 // TransaccionPago
 modelBuilder.Entity<TransaccionPago>(e =>
 {
 e.Property(t => t.Monto).HasPrecision(18,2);
 e.Property(t => t.Tipo).IsRequired().HasMaxLength(20);
 e.Property(t => t.Referencia).HasMaxLength(100);
 });

 // Resena
 modelBuilder.Entity<Resena>(e =>
 {
 e.Property(r => r.Calificacion).IsRequired();
 e.Property(r => r.Comentario).HasMaxLength(2000);
 e.Property(r => r.FotosJson).HasMaxLength(4000);
 e.Property(r => r.EstadoConfort).HasMaxLength(100);
 e.Property(r => r.EstadoLimpieza).HasMaxLength(100);
 });
 }
}