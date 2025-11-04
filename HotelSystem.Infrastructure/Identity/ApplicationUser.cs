using Microsoft.AspNetCore.Identity;

namespace HotelSystem.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
 // Campos extra si se requieren
 public string? FullName { get; set; }
}