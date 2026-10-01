using Domain.Enums;

namespace Domain.Entities;

public class AuctionState_Tr
{
    public AuctionState StateId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
