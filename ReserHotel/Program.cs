using AutoMapper;
using HotelSystem.Domain.Interfaces;
using HotelSystem.Infrastructure.Data;
using HotelSystem.Infrastructure.Identity;
using HotelSystem.Infrastructure.Persistence;
using HotelSystem.Infrastructure.Repositories;
using HotelSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace ReserHotel;

internal class Program
{
 public static async Task Main(string[] args)
 {
 var builder = WebApplication.CreateBuilder(args);

 // Serilog: consola + archivo
 builder.Host.UseSerilog((ctx, cfg) => cfg
 .ReadFrom.Configuration(ctx.Configuration)
 .WriteTo.Console()
 .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day));

 // Connection string preferida desde appsettings; si falla, probar variantes locales
 var primary = builder.Configuration.GetConnectionString("Default")
 ?? "Server=LAPTOP-GR5R0DAP;Database=HotelSystemDb;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15";

 static bool CanOpen(string cs)
 {
 try { using var c = new SqlConnection(cs); c.Open(); return true; } catch { return false; }
 }

 var candidates = new[]
 {
 primary,
 "Server=(local);Database=HotelSystemDb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=15",
 "Server=localhost;Database=HotelSystemDb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=15",
 "Server=.;Database=HotelSystemDb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=15",
 "Server=tcp:LAPTOP-GR5R0DAP,1433;Database=HotelSystemDb;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15"
 };

 string chosen = candidates.FirstOrDefault(CanOpen) ?? primary;
 Log.Information("[DB] Usando cadena de conexión: {DataSource}", new SqlConnectionStringBuilder(chosen).DataSource);

 // DbContext
 builder.Services.AddDbContext<HotelDbContext>(options =>
 options.UseSqlServer(chosen, sql => sql.EnableRetryOnFailure()));

 // Identity
 builder.Services
 .AddDefaultIdentity<ApplicationUser>(options =>
 {
 options.SignIn.RequireConfirmedAccount = false;
 })
 .AddRoles<IdentityRole>()
 .AddEntityFrameworkStores<HotelDbContext>();

 // MVC
 builder.Services.AddControllersWithViews();

 // UnitOfWork & repositories
 builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

 // Infra services
 builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();

 // AutoMapper
 builder.Services.AddAutoMapper(typeof(Program));

 // Quartz job to send reminders48h/24h (programación simple cada hora)
 builder.Services.AddQuartz(q =>
 {
 q.UseMicrosoftDependencyInjectionJobFactory();
 var jobKey = JobKey.Create("ReminderJob");
 q.AddJob<ReminderJob>(opts => opts.WithIdentity(jobKey));
 q.AddTrigger(opts => opts
 .ForJob(jobKey)
 .WithIdentity("ReminderJob-trigger")
 .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever()));
 });

 builder.Services.AddQuartzHostedService(options =>
 {
 options.WaitForJobsToComplete = true;
 });

 var app = builder.Build();

 if (!app.Environment.IsDevelopment())
 {
 app.UseExceptionHandler("/Home/Error");
 app.UseHsts();
 app.UseHttpsRedirection();
 }

 app.UseStaticFiles();
 app.UseRouting();
 app.UseAuthentication();
 app.UseAuthorization();

 app.MapControllerRoute(
 name: "reservas_root",
 pattern: "Reservas",
 defaults: new { controller = "Reservas", action = "Index" });

 app.MapControllerRoute(
 name: "default",
 pattern: "{controller=Home}/{action=Index}/{id?}");
 app.MapRazorPages();

 // Migraciones + seed y roles
 using (var scope = app.Services.CreateScope())
 {
 try
 {
 var db = scope.ServiceProvider.GetRequiredService<HotelDbContext>();
 await DataSeeder.SeedAsync(db);
 var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
 foreach (var role in new[] { "Admin", "Gerente", "Recepcionista", "Huesped" })
 if (!await roleMgr.RoleExistsAsync(role)) await roleMgr.CreateAsync(new IdentityRole(role));
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[Startup] No se pudo aplicar migraciones/seed");
 }
 }

 await app.RunAsync();
 }
}

public class ReminderJob : IJob
{
 private readonly IUnitOfWork _uow;
 private readonly IEmailSender _email;
 public ReminderJob(IUnitOfWork uow, IEmailSender email) { _uow = uow; _email = email; }
 public async Task Execute(IJobExecutionContext context)
 {
 try
 {
 var reservas = await _uow.Reservas.GetAll(context.CancellationToken);
 var t48 = DateTime.UtcNow.AddHours(48);
 var t24 = DateTime.UtcNow.AddHours(24);
 foreach (var r in reservas)
 {
 if (r.Cliente?.Email is null) continue;
 if (r.Estado == HotelSystem.Domain.Entities.EstadoReserva.Pendiente && r.FechaEntrada <= t48 && r.FechaEntrada > DateTime.UtcNow.AddHours(47))
 await _email.SendAsync(r.Cliente.Email, "Recordatorio de reserva (48h)", $"Su reserva {r.NumeroReserva} es en48 horas", context.CancellationToken);
 if (r.Estado == HotelSystem.Domain.Entities.EstadoReserva.Pendiente && r.FechaEntrada <= t24 && r.FechaEntrada > DateTime.UtcNow.AddHours(23))
 await _email.SendAsync(r.Cliente.Email, "Recordatorio de reserva (24h)", $"Su reserva {r.NumeroReserva} es en24 horas", context.CancellationToken);
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[ReminderJob] Error");
 }
 }
}
