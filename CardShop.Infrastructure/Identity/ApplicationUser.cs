using Microsoft.AspNetCore.Identity;

namespace CardShop.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public Guid? RegistrationId { get; set; }
    }
}