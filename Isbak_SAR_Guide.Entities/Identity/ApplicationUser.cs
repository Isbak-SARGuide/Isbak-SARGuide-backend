using Microsoft.AspNetCore.Identity;

namespace Isbak_SAR_Guide.Entities.Identity;


public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
