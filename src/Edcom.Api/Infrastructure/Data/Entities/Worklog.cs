namespace Edcom.Api.Infrastructure.Data.Entities;

public class Worklog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Hours logged in this entry.</summary>
    public decimal Hours { get; set; }
    public string? Description { get; set; }
    /// <summary>The calendar day this work was performed (UTC date, time component ignored).</summary>
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Issue Issue { get; set; } = null!;
    public User User { get; set; } = null!;
}
