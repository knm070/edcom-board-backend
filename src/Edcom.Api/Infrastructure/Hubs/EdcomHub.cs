using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Edcom.Api.Infrastructure.Hubs;

[Authorize]
public class EdcomHub : Hub
{
    /// <summary>Client calls this after connecting to subscribe to an org channel.</summary>
    public async Task JoinOrg(string orgId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"org:{orgId}");
    }

    /// <summary>Client calls this to subscribe to a specific space channel.</summary>
    public async Task JoinSpace(string spaceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"space:{spaceId}");
    }

    public async Task LeaveOrg(string orgId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"org:{orgId}");
    }

    public async Task LeaveSpace(string spaceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"space:{spaceId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
    }
}
