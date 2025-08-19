using StackExchange.Redis;
using PaymentGateway.Payments.Models;
using System.Text.Json;

namespace PaymentGateway.Payments.Services;

public interface IRedisService
{
    Task SetTransactionStatusAsync(string transactionId, TransactionStatus status);
    Task<TransactionStatus?> GetTransactionStatusAsync(string transactionId);
    Task PublishTransactionUpdateAsync(Transaction transaction);
    Task SubscribeToTransactionUpdatesAsync(Action<Transaction> onTransactionUpdated);
    Task<List<object>?> GetUserTransactionsAsync(string email);
    Task<decimal?> GetUserBalanceAsync(string email);
}

public class RedisService : IRedisService
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisService> _logger;
    private const string TransactionStatusPrefix = "transaction_status:";
    private const string TransactionUpdateChannel = "transaction_updates";

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _database = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _logger = logger;
    }

    public async Task SetTransactionStatusAsync(string transactionId, TransactionStatus status)
    {
        try
        {
            var key = $"{TransactionStatusPrefix}{transactionId}";
            await _database.StringSetAsync(key, status.ToString(), TimeSpan.FromHours(24));
            _logger.LogInformation("Transaction {TransactionId} status set to {Status}", transactionId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting transaction status for {TransactionId}", transactionId);
            throw;
        }
    }

    public async Task<TransactionStatus?> GetTransactionStatusAsync(string transactionId)
    {
        try
        {
            var key = $"{TransactionStatusPrefix}{transactionId}";
            var status = await _database.StringGetAsync(key);
            
            if (status.HasValue && Enum.TryParse<TransactionStatus>(status, out var parsedStatus))
            {
                return parsedStatus;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction status for {TransactionId}", transactionId);
            return null;
        }
    }

    public async Task PublishTransactionUpdateAsync(Transaction transaction)
    {
        try
        {
            var message = JsonSerializer.Serialize(transaction);
            await _subscriber.PublishAsync(TransactionUpdateChannel, message);
            _logger.LogInformation("Transaction update published for {TransactionId}", transaction.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing transaction update for {TransactionId}", transaction.Id);
            throw;
        }
    }

    public async Task SubscribeToTransactionUpdatesAsync(Action<Transaction> onTransactionUpdated)
    {
        try
        {
            await _subscriber.SubscribeAsync(TransactionUpdateChannel, (channel, message) =>
            {
                try
                {
                    var transaction = JsonSerializer.Deserialize<Transaction>(message!);
                    if (transaction != null)
                    {
                        onTransactionUpdated(transaction);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing transaction update message");
                }
            });
            
            _logger.LogInformation("Subscribed to transaction updates channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to transaction updates");
            throw;
        }
    }

    public async Task<List<object>?> GetUserTransactionsAsync(string email)
    {
        try
        {
            var key = $"user_transactions:{email}";
            var transactions = await _database.ListRangeAsync(key, 0, -1);
            
            if (transactions?.Length > 0)
            {
                var result = new List<object>();
                foreach (var transaction in transactions)
                {
                    if (transaction.HasValue)
                    {
                        var transactionObj = JsonSerializer.Deserialize<object>(transaction!);
                        if (transactionObj != null)
                        {
                            result.Add(transactionObj);
                        }
                    }
                }
                return result;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user transactions for {Email}", email);
            return null;
        }
    }

    public async Task<decimal?> GetUserBalanceAsync(string email)
    {
        try
        {
            var key = $"user_balance:{email}";
            var userData = await _database.StringGetAsync(key);
            
            if (userData.HasValue)
            {
                var userObj = JsonSerializer.Deserialize<JsonElement>(userData!);
                if (userObj.TryGetProperty("Balance", out var balanceProperty))
                {
                    return balanceProperty.GetDecimal();
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user balance for {Email}", email);
            return null;
        }
    }
}
