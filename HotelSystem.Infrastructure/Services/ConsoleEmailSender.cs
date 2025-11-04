using System.Text;

namespace HotelSystem.Infrastructure.Services;

public class ConsoleEmailSender : IEmailSender
{
 public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
 {
 Console.WriteLine($"EMAIL TO: {to}\nSUBJECT: {subject}\nBODY:\n{htmlBody}");
 return Task.CompletedTask;
 }
}