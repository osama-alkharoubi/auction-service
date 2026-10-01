namespace Domain.Entities;

public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public Guid UserId { get; set; }
    public string ResponseBodyJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
