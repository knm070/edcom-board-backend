namespace Edcom.Api.Infrastructure.Data.Entities;

/// <summary>
/// Snapshot of a sprint's story-point commitment vs. completion,
/// recorded once when the sprint is completed.
/// </summary>
public class SprintVelocityRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SprintId { get; set; }
    public Guid SpaceId { get; set; }
    /// <summary>Total story points of all issues that were in the sprint when it completed.</summary>
    public int CommittedPoints { get; set; }
    /// <summary>Story points of issues whose status was terminal at completion time.</summary>
    public int CompletedPoints { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public Sprint Sprint { get; set; } = null!;
    public Space Space { get; set; } = null!;
}
