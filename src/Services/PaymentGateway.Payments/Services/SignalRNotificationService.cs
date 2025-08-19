using Microsoft.AspNetCore.SignalR;
using PaymentGateway.Payments.Hubs;
using PaymentGateway.Payments.Models;
using PaymentGateway.Payments.Services;

namespace PaymentGateway.Payments.Services;

public interface ISignalRNotificationService
{
    Task SendTransactionUpdateAsync(Transaction transaction);
}

public class SignalRNotificationService : BackgroundService, ISignalRNotificationService
{
    private readonly ILogger<SignalRNotificationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SignalRNotificationService(ILogger<SignalRNotificationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task SendTransactionUpdateAsync(Transaction transaction)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TransactionHub>>();

        try
        {
            
            var userGroup = $"user_{transaction.FromEmail}";
            
            var updateMessage = new
            {
                TransactionId = transaction.Id,
                Status = transaction.Status.ToString(),
                Amount = transaction.Amount,
                ToAgencia = transaction.ToAgencia,
                ToConta = transaction.ToConta,
                ToUserName = transaction.ToUserName,
                ProcessedAt = transaction.ProcessedAt,
                ErrorMessage = transaction.ErrorMessage
            };

            await hubContext.Clients.Group(userGroup).SendAsync("TransactionUpdate", updateMessage);
            
            _logger.LogInformation("Sent transaction update via SignalR to user {Email} for transaction {TransactionId} with status {Status}", 
                transaction.FromEmail, transaction.Id, transaction.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification for transaction {TransactionId}", transaction.Id);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TransactionHub>>();

        _logger.LogInformation("SignalR notification service started");

        await redisService.SubscribeToTransactionUpdatesAsync(async (transaction) =>
        {
            await SendTransactionUpdateAsync(transaction);
        });

        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
