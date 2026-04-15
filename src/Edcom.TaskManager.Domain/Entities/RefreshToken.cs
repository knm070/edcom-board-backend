using System.ComponentModel.DataAnnotations.Schema;

namespace Edcom.TaskManager.Domain.Entities;

public class RefreshToken : ModelBase<long>
{
    public long UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
