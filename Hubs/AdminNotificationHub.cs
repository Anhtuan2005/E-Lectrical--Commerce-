using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcommerceApp.Hubs;

[Authorize(Roles = "Admin")]
public class AdminNotificationHub : Hub
{
    public const string AdminGroup = "AdminNotifications";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnDisconnectedAsync(exception);
    }
}

public sealed record AdminOrderCreatedMessage(
    int Id,
    string Code,
    string CustomerName,
    decimal TotalAmount,
    DateTime CreatedAt,
    string Url);
