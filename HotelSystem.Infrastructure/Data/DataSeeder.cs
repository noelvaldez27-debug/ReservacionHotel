using HotelSystem.Domain.Entities;
using HotelSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace HotelSystem.Infrastructure.Data;

public static class DataSeeder
{
 public static async Task SeedAsync(HotelDbContext db, CancellationToken ct = default)
 {
 // Asegurar creación de BD en SQL Server (creando en master si no existe)
 try
 {
 var cs = db.Database.GetDbConnection().ConnectionString;
 var csb = new SqlConnectionStringBuilder(cs);
 if (!string.IsNullOrWhiteSpace(csb.InitialCatalog))
 {
 var master = new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" };
 await using var cnn = new SqlConnection(master.ConnectionString);
 await cnn.OpenAsync(ct);
 await using var cmd = cnn.CreateCommand();
 cmd.CommandText = "IF DB_ID(@n) IS NULL CREATE DATABASE [" + csb.InitialCatalog + "]";
 cmd.Parameters.AddWithValue("@n", csb.InitialCatalog);
 await cmd.ExecuteNonQueryAsync(ct);
 }
 }
 catch { /* ignore */ }

 // Migraciones
 try { await db.Database.MigrateAsync(ct); } catch { await db.Database.EnsureCreatedAsync(ct); }

 // Parche: columnas faltantes
 await EnsureColumnAsync(db, "Reservas", "FechaReserva", "DATETIME2 NOT NULL CONSTRAINT DF_Reservas_FechaReserva DEFAULT SYSUTCDATETIME()", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RegistradoNombreCompleto", "NVARCHAR(150) NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RegistradoDni", "NVARCHAR(20) NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefEntrada", "DATETIME2 NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefSalida", "DATETIME2 NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefNoches", "INT NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefSubtotalHabitacion", "DECIMAL(18,2) NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefSubtotalServicios", "DECIMAL(18,2) NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefTotal", "DECIMAL(18,2) NULL", ct);
 await EnsureColumnAsync(db, "Habitaciones", "RefServiciosJson", "NVARCHAR(2000) NULL", ct);

 if (await db.Hoteles.AnyAsync(ct)) return;

 var hotel = new Hotel
 {
 Nombre = "Hotel Central",
 Ubicacion = "Ciudad Central",
 Estrellas =4,
 Descripcion = "Hotel principal con excelente ubicación"
 };
 db.Hoteles.Add(hotel);
 await db.SaveChangesAsync(ct);

 // Habitaciones:20 cuartos
 var habitaciones = new List<Habitacion>();
 int[] simples = Enumerable.Range(101,8).ToArray();
 int[] dobles = Enumerable.Range(201,7).ToArray();
 int[] suites = Enumerable.Range(301,5).ToArray();

 var jacuzziRooms = new HashSet<int>(suites.Take(3).Concat(dobles.Take(2))); //5 con jacuzzi (y wifi)
 var wifiOnlyRooms = new HashSet<int>(simples.Take(3).Concat(dobles.Skip(2).Take(2))); //5 adicionales solo wifi

 string BuildAmenidades(int numero, string extra)
 {
 var list = new List<string> { "netflix", "tv", "aire" };
 if (jacuzziRooms.Contains(numero)) { list.Add("jacuzzi"); list.Add("wifi"); }
 else if (wifiOnlyRooms.Contains(numero)) { list.Add("wifi"); }
 if (!string.IsNullOrWhiteSpace(extra)) list.Add(extra);
 return string.Join(", ", list);
 }

 foreach (var n in simples)
 habitaciones.Add(new Habitacion { Numero = n, Piso = n /100, Tipo = TipoHabitacion.Simple, Capacidad =1, Amenidades = BuildAmenidades(n, string.Empty), HotelId = hotel.Id });
 foreach (var n in dobles)
 habitaciones.Add(new Habitacion { Numero = n, Piso = n /100, Tipo = TipoHabitacion.Doble, Capacidad =2, Amenidades = BuildAmenidades(n, "cafetera"), HotelId = hotel.Id });
 foreach (var n in suites)
 habitaciones.Add(new Habitacion { Numero = n, Piso = n /100, Tipo = TipoHabitacion.Suite, Capacidad =4, Amenidades = BuildAmenidades(n, "cafetera"), HotelId = hotel.Id });
 db.Habitaciones.AddRange(habitaciones);
 await db.SaveChangesAsync(ct);

 var bases = new Dictionary<TipoHabitacion, decimal>
 {
 [TipoHabitacion.Simple] =50m,
 [TipoHabitacion.Doble] =80m,
 [TipoHabitacion.Suite] =120m,
 };
 var tarifas = new List<TarifaHabitacion>();
 foreach (var kv in bases)
 {
 tarifas.Add(new TarifaHabitacion { HotelId = hotel.Id, TipoHabitacion = kv.Key, Temporada = Temporada.Baja, PrecioBase = kv.Value, VariacionPorcentaje = -30m });
 tarifas.Add(new TarifaHabitacion { HotelId = hotel.Id, TipoHabitacion = kv.Key, Temporada = Temporada.Alta, PrecioBase = kv.Value, VariacionPorcentaje =20m });
 }
 db.TarifasHabitacion.AddRange(tarifas);
 await db.SaveChangesAsync(ct);

 var servicios = new[]
 {
 new ServicioAdicional { HotelId = hotel.Id, Nombre = NombreServicio.Desayuno, Precio =15m, Descripcion = "Desayuno buffet" },
 new ServicioAdicional { HotelId = hotel.Id, Nombre = NombreServicio.Spa, Precio =50m, Descripcion = "Acceso a spa" },
 new ServicioAdicional { HotelId = hotel.Id, Nombre = NombreServicio.Estacionamiento, Precio =10m, Descripcion = "Estacionamiento cubierto" },
 new ServicioAdicional { HotelId = hotel.Id, Nombre = NombreServicio.LateCheckout, Precio =25m, Descripcion = "Salida tardía" },
 };
 db.ServiciosAdicionales.AddRange(servicios);
 await db.SaveChangesAsync(ct);

 var huespedes = new List<Huesped>
 {
 new() { Documento = "DNI0001", NombreCompleto = "Juan Perez", Email = "juan@example.com", Pais = "AR", FechaRegistro = DateTime.UtcNow.AddMonths(-6) },
 new() { Documento = "DNI0002", NombreCompleto = "Maria Lopez", Email = "maria@example.com", Pais = "AR", FechaRegistro = DateTime.UtcNow.AddMonths(-5) },
 new() { Documento = "DNI0003", NombreCompleto = "Carlos Diaz", Email = "carlos@example.com", Pais = "AR", FechaRegistro = DateTime.UtcNow.AddMonths(-4) },
 };
 db.Huespedes.AddRange(huespedes);
 await db.SaveChangesAsync(ct);

 var rand = new Random(42);
 DateTime start = DateTime.Today.AddMonths(-6);
 var serviciosList = await db.ServiciosAdicionales.Where(s => s.HotelId == hotel.Id).ToListAsync(ct);
 for (int i =0; i <25; i++)
 {
 var he = start.AddDays(rand.Next(0,150));
 var nights = rand.Next(1,5);
 var hs = habitaciones[rand.Next(habitaciones.Count)];
 var guest = huespedes[rand.Next(huespedes.Count)];
 var reserva = new Reserva
 {
 NumeroReserva = $"HX-{he:yyyyMMdd}-{i:000}",
 FechaReserva = he.AddDays(-7),
 FechaEntrada = he,
 FechaSalida = he.AddDays(nights),
 Estado = EstadoReserva.Completada,
 ClienteId = guest.Id,
 CheckInAt = he.AddHours(14),
 CheckOutAt = he.AddDays(nights).AddHours(10)
 };
 db.Reservas.Add(reserva);
 await db.SaveChangesAsync(ct);

 decimal subtotalHab =0m;
 for (var d = reserva.FechaEntrada; d < reserva.FechaSalida; d = d.AddDays(1))
 {
 var temp = (d.Month is 1 or 7 or 8 or 12) ? Temporada.Alta : Temporada.Baja;
 var tarifa = tarifas.First(t => t.HotelId == hotel.Id && t.TipoHabitacion == hs.Tipo && t.Temporada == temp);
 subtotalHab += tarifa.PrecioBase * (1 + tarifa.VariacionPorcentaje /100m);
 }
 var detalle = new DetalleReserva
 {
 ReservaId = reserva.Id,
 HabitacionId = hs.Id,
 CantidadNoches = nights,
 PrecioTotal = subtotalHab,
 DescuentoAplicado =0
 };
 db.DetallesReserva.Add(detalle);

 decimal subtotalServ =0m;
 foreach (var s in serviciosList.OrderBy(_ => rand.Next()).Take(rand.Next(0,3)))
 {
 db.ReservasServicios.Add(new ReservaServicio
 {
 ReservaId = reserva.Id,
 ServicioId = s.Id,
 Cantidad =1,
 PrecioUnitario = s.Precio
 });
 subtotalServ += s.Precio;
 }

 db.Facturas.Add(new Factura
 {
 ReservaId = reserva.Id,
 MontoTotal = subtotalHab + subtotalServ,
 EstadoPago = EstadoPago.Pagado,
 FechaPago = reserva.FechaEntrada.AddDays(-1),
 MetodoPago = "Tarjeta"
 });
 await db.SaveChangesAsync(ct);
 }
 }

 private static async Task EnsureColumnAsync(HotelDbContext db, string table, string column, string definition, CancellationToken ct)
 {
 var sql = $@"IF COL_LENGTH('{table}', '{column}') IS NULL
BEGIN
 ALTER TABLE [{table}] ADD [{column}] {definition};
END";
 try
 {
 await db.Database.ExecuteSqlRawAsync(sql, ct);
 }
 catch
 {
 // ignorar si no se puede aplicar (permisos) o ya existe con otra restricción por defecto
 }
 }
}
