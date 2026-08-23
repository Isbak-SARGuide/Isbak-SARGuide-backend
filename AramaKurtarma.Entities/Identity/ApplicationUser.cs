
using Microsoft.AspNetCore.Identity;

namespace AramaKurtarma.Entities.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}