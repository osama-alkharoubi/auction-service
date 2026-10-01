namespace Application.Dto.Auctions;

public sealed record ChangeStateApiRequest(int NewState, string? Reason);
