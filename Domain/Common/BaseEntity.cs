using Domain.Enums;

namespace Domain.Common;

public abstract class BaseEntity
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Availability AvailabilityId { get; set; } = Availability.Active;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
