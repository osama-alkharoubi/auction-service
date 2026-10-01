namespace Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime? ProcessedUtc { get; set; }
    public int Attempts { get; set; }
}
