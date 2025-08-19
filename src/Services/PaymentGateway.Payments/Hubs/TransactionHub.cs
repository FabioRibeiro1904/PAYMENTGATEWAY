using Microsoft.AspNetCore.SignalR;

namespace PaymentGateway.Payments.Hubs;

public class TransactionHub : Hub
{
    private readonly ILogger<TransactionHub> _logger;

    public TransactionHub(ILogger<TransactionHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinUserGroup(string userEmail)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userEmail}");
        _logger.LogInformation("User {Email} joined group user_{Email}", userEmail, userEmail);
    }

    public async Task LeaveUserGroup(string userEmail)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userEmail}");
        _logger.LogInformation("User {Email} left group user_{Email}", userEmail, userEmail);
    }

    public async Task SendToGroup(string groupName, string method, object arg1, object arg2, object arg3)
    {
        _logger.LogInformation("📤 Sending message to group {GroupName}: {Method}", groupName, method);
        await Clients.Group(groupName).SendAsync(method, arg1, arg2, arg3);
        _logger.LogInformation("Message sent to group {GroupName}", groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
