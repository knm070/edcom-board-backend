namespace Edcom.Api.Infrastructure.Data.Entities;

/// <summary>
/// Designates a user as SpaceManager for a specific space.
/// One user can be SM for multiple spaces within the same org.
/// OrgManagers do not need a SpaceAssignment — they have implicit authority over all spaces.
/// </summary>
public class SpaceAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid AssignedById { get; set; }

    public Space Space { get; set; } = null!;
    public User User { get; set; } = null!;
    public User AssignedBy { get; set; } = null!;
}
