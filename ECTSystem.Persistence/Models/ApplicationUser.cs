using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ECTSystem.Persistence.Models;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [MaxLength(100)]
    public string FirstName { get; set; }

    [PersonalData]
    [MaxLength(100)]
    public string LastName { get; set; }
}
