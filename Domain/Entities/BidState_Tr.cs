using Domain.Enums;

namespace Domain.Entities;

public class BidState_Tr
{
    public BidState StateId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
