using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class User : BaseEntity, IMustHaveTenant
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }

    public Organization Organization { get; set; } = null!;

    public ICollection<Auction> Auctions { get; set; } = new HashSet<Auction>();
}
