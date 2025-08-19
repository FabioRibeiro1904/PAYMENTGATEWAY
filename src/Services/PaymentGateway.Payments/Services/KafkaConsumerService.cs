using Confluent.Kafka;
using PaymentGateway.Payments.Models;
using PaymentGateway.Payments.Services;
using PaymentGateway.Payments.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Collections.Concurrent;

namespace PaymentGateway.Payments.Services;
public class BankUser
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Conta { get; set; } = string.Empty;
    public string Agencia { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Role { get; set; } = "User";
}

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConsumer<string, string> _consumer;
    private readonly IHubContext<TransactionHub> _hubContext;
    private const string TopicName = "payment-transactions";
    
    
    private static readonly ConcurrentDictionary<string, BankUser> Users = new();
    private static readonly object LockObject = new();

    public KafkaConsumerService(
        ILogger<KafkaConsumerService> logger, 
        IServiceProvider serviceProvider,
        IHubContext<TransactionHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;

        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "payment-processor-group",
            ClientId = "payment-gateway-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        
        
        InitializeTestUsers();
    }

    private void InitializeTestUsers()
    {
        
        var testUsers = new[]
        {
            new BankUser
            {
                Name = "João Silva",
                Email = "joao@email.com",
                Password = "senha123",
                Conta = "12345678-90",
                Agencia = "12345-6",
                Balance = 1000.00m
            },
            new BankUser
            {
                Name = "Maria Santos",
                Email = "maria@email.com",
                Password = "senha456",
                Conta = "67890123-45",
                Agencia = "56789-0",
                Balance = 1500.00m
            },
            new BankUser
            {
                Name = "Pedro Costa",
                Email = "pedro@email.com",
                Password = "senha789",
                Conta = "11111111-22",
                Agencia = "99999-8",
                Balance = 1000.00m
            },
            new BankUser
            {
                Name = "Admin",
                Email = "admin@paymentgateway.com",
                Password = "admin123",
                Conta = "00000000-01",
                Agencia = "00000-1",
                Balance = 10000.00m
            },
            new BankUser
            {
                Name = "Teste User",
                Email = "teste15@gmail.com",
                Password = "teste123",
                Conta = "55555555-55",
                Agencia = "55555-5",
                Balance = 2000.00m
            }
        };

        foreach (var user in testUsers)
        {
            Users.TryAdd(user.Email, user);
        }

        _logger.LogInformation("Initialized {Count} test users for payment processing", testUsers.Length);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _consumer.Subscribe(TopicName);
            _logger.LogInformation("Kafka consumer started, listening to topic: {Topic}", TopicName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(1000));
                    if (result?.Message?.Value != null)
                    {
                        await ProcessTransactionAsync(result.Message.Value);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Kafka consumer operation was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Kafka consumer");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer stopped due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Kafka consumer");
        }
        finally
        {
            try
            {
                _consumer?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Kafka consumer");
            }
        }
    }

    private async Task ProcessTransactionAsync(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();

        try
        {
            var transaction = JsonSerializer.Deserialize<Transaction>(message);
            if (transaction == null)
            {
                _logger.LogWarning("Invalid transaction message received");
                return;
            }

            _logger.LogInformation("Processing transaction {TransactionId}", transaction.Id);

            
            transaction.Status = TransactionStatus.Processing;
            await redisService.SetTransactionStatusAsync(transaction.Id, TransactionStatus.Processing);
            await redisService.PublishTransactionUpdateAsync(transaction);

            
            await Task.Delay(3000);

            
            await ProcessPaymentAsync(transaction);

            
            await redisService.SetTransactionStatusAsync(transaction.Id, transaction.Status);
            await redisService.PublishTransactionUpdateAsync(transaction);

            _logger.LogInformation("Transaction {TransactionId} processed with status {Status}", 
                transaction.Id, transaction.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction");
        }
    }

    private async Task ProcessPaymentAsync(Transaction transaction)
    {
        try
        {
            
            await _hubContext.Clients.All.SendAsync("TransactionStatusUpdated", 
                transaction.Id, "Processing", "Iniciando processamento da transação");

            
            await Task.Delay(3000);

            
            lock (LockObject)
            {
                
                var recipient = Users.Values.FirstOrDefault(u => 
                    u.Agencia == transaction.ToAgencia && u.Conta == transaction.ToConta);

                if (recipient == null)
                {
                    transaction.Status = TransactionStatus.Failed;
                    transaction.ErrorMessage = "Destinatário não encontrado";
                    _logger.LogWarning("Recipient not found: {Agency}-{Account}", 
                        transaction.ToAgencia, transaction.ToConta);
                    return;
                }

                transaction.ToUserName = recipient.Name;

                
                if (!Users.TryGetValue(transaction.FromEmail, out var sender))
                {
                    transaction.Status = TransactionStatus.Failed;
                    transaction.ErrorMessage = "Remetente não encontrado";
                    _logger.LogWarning("Sender not found: {Email}", transaction.FromEmail);
                    return;
                }

                
                if (sender.Balance < transaction.Amount)
                {
                    transaction.Status = TransactionStatus.Failed;
                    transaction.ErrorMessage = "Saldo insuficiente";
                    _logger.LogWarning("Insufficient balance: {Balance} < {Amount}", 
                        sender.Balance, transaction.Amount);
                    return;
                }

                
                sender.Balance -= transaction.Amount;
                recipient.Balance += transaction.Amount;

                
                transaction.Status = TransactionStatus.Completed;
                transaction.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation("Payment processed successfully: {Amount} from {FromEmail} to {ToName} ({ToEmail})", 
                    transaction.Amount, transaction.FromEmail, recipient.Name, recipient.Email);
            }

            
            await _hubContext.Clients.All.SendAsync("TransactionStatusUpdated", 
                transaction.Id, "Completed", "Transação processada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during payment processing for transaction {TransactionId}", transaction.Id);
            transaction.Status = TransactionStatus.Failed;
            transaction.ErrorMessage = "Erro interno durante o processamento";

            
            await _hubContext.Clients.All.SendAsync("TransactionStatusUpdated", 
                transaction.Id, "Failed", transaction.ErrorMessage);
        }
    }
}
