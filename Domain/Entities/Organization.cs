using Domain.Common;

namespace Domain.Entities;

public class Organization: BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new HashSet<User>();
    public ICollection<Auction> Auctions { get; set; } = new HashSet<Auction>();
}
