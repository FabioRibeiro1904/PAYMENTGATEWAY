using PaymentGateway.Payments.Models;

namespace PaymentGateway.Payments.Services;
public class MockKafkaProducerService : IKafkaProducerService
{
    private readonly ILogger<MockKafkaProducerService> _logger;

    public MockKafkaProducerService(ILogger<MockKafkaProducerService> logger)
    {
        _logger = logger;
    }

    public async Task SendTransactionAsync(Transaction transaction)
    {
        _logger.LogInformation("MOCK: Transaction {TransactionId} sent to Kafka", transaction.Id);
        await Task.Delay(100); 
    }
}

public class MockRedisService : IRedisService
{
    private readonly ILogger<MockRedisService> _logger;
    private readonly Dictionary<string, TransactionStatus> _statusCache = new();

    public MockRedisService(ILogger<MockRedisService> logger)
    {
        _logger = logger;
    }

    public async Task SetTransactionStatusAsync(string transactionId, TransactionStatus status)
    {
        _statusCache[transactionId] = status;
        _logger.LogInformation("MOCK: Transaction {TransactionId} status set to {Status}", transactionId, status);
        await Task.Delay(10);
    }

    public async Task<TransactionStatus?> GetTransactionStatusAsync(string transactionId)
    {
        await Task.Delay(10);
        return _statusCache.GetValueOrDefault(transactionId);
    }

    public async Task PublishTransactionUpdateAsync(Transaction transaction)
    {
        _logger.LogInformation("MOCK: Transaction update published for {TransactionId}", transaction.Id);
        await Task.Delay(10);
    }

    public async Task SubscribeToTransactionUpdatesAsync(Action<Transaction> onTransactionUpdated)
    {
        _logger.LogInformation("MOCK: Subscribed to transaction updates");
        await Task.Delay(10);
    }

    public async Task<List<object>?> GetUserTransactionsAsync(string email)
    {
        _logger.LogInformation("MOCK: Getting transactions for {Email}", email);
        await Task.Delay(10);
        
        
        return new List<object>
        {
            new {
                id = Guid.NewGuid().ToString(),
                date = DateTime.UtcNow.AddDays(-1),
                description = "Transferência enviada para Maria",
                amount = -50.0m,
                currency = "BRL",
                type = "sent",
                status = "completed"
            },
            new {
                id = Guid.NewGuid().ToString(),
                date = DateTime.UtcNow.AddDays(-2),
                description = "Transferência recebida de João",
                amount = 100.0m,
                currency = "BRL",
                type = "received",
                status = "completed"
            }
        };
    }

    public async Task<decimal?> GetUserBalanceAsync(string email)
    {
        _logger.LogInformation("MOCK: Getting balance for {Email}", email);
        await Task.Delay(10);
        
        
        return 1000.0m;
    }
}
