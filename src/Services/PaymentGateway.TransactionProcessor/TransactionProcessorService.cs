using Confluent.Kafka;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Collections.Concurrent;

namespace PaymentGateway.TransactionProcessor;

public class TransactionProcessorService : BackgroundService
{
    private readonly ILogger<TransactionProcessorService> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly IConnectionMultiplexer _redis;
    private readonly HubConnection _signalRConnection;
    private const string TopicName = "payment-transactions";
    
    private static readonly ConcurrentDictionary<string, BankUser> Users = new();
    private static readonly object LockObject = new();

    public TransactionProcessorService(ILogger<TransactionProcessorService> logger)
    {
        _logger = logger;

        var kafkaConfig = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = $"transaction-processor-group-{Guid.NewGuid().ToString()[..8]}",
            ClientId = "transaction-processor-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();

        _redis = ConnectionMultiplexer.Connect("localhost:6379");

        _signalRConnection = new HubConnectionBuilder()
            .WithUrl("http:
            .WithAutomaticReconnect()
            .Build();

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

        _logger.LogInformation("Initialized {Count} test users for transaction processing", testUsers.Length);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _signalRConnection.StartAsync(stoppingToken);
            _logger.LogInformation("SignalR connection established");

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
                    _logger.LogInformation("Transaction processor operation was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in transaction processor");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transaction processor stopped due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in transaction processor");
        }
        finally
        {
            try
            {
                _consumer?.Close();
                if (_signalRConnection != null)
                    await _signalRConnection.DisposeAsync();
                _redis?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing resources");
            }
        }
    }

    private async Task ProcessTransactionAsync(string transactionJson)
    {
        Transaction? transaction = null;
        try
        {
            transaction = JsonSerializer.Deserialize<Transaction>(transactionJson);
            if (transaction == null)
            {
                _logger.LogWarning("Received null transaction");
                return;
            }

            _logger.LogInformation("Processing transaction {TransactionId}", transaction.Id);

            await SendSignalRNotificationSafeAsync(transaction.FromEmail, "TransactionStatusUpdated", 
                transaction.Id, "Processing", "Processando transação...");

            await Task.Delay(5000);

            var database = _redis.GetDatabase();
            
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

                _logger.LogInformation("Payment processed: {Amount} from {FromEmail} to {ToName} ({ToEmail})", 
                    transaction.Amount, transaction.FromEmail, recipient.Name, recipient.Email);
                
                _logger.LogInformation("Saldos atualizados - {SenderEmail}: R$ {SenderBalance} | {RecipientEmail}: R$ {RecipientBalance}", 
                    sender.Email, sender.Balance, recipient.Email, recipient.Balance);
            }

            if (transaction.Status == TransactionStatus.Completed)
            {
                var sender = Users[transaction.FromEmail];
                var recipient = Users.Values.First(u => u.Agencia == transaction.ToAgencia && u.Conta == transaction.ToConta);
                
                await database.StringSetAsync($"user_balance:{sender.Email}", 
                    JsonSerializer.Serialize(sender), TimeSpan.FromHours(24));
                await database.StringSetAsync($"user_balance:{recipient.Email}", 
                    JsonSerializer.Serialize(recipient), TimeSpan.FromHours(24));
                
                await CreateTransactionHistoryEntry(database, sender.Email, transaction, "sent", recipient.Name);
                await CreateTransactionHistoryEntry(database, recipient.Email, transaction, "received", sender.Name);
            }

            await database.StringSetAsync($"transaction:{transaction.Id}", 
                JsonSerializer.Serialize(transaction), TimeSpan.FromHours(24));
            
            await database.StringSetAsync($"transaction_status:{transaction.Id}", 
                transaction.Status.ToString(), TimeSpan.FromHours(24));

            _logger.LogInformation("Sending SignalR notification for transaction {TransactionId}, status: {Status}", 
                transaction.Id, transaction.Status);
            
            try
            {
                await SendSignalRNotificationSafeAsync(transaction.FromEmail, "TransactionStatusUpdated", 
                    transaction.Id, transaction.Status.ToString(), 
                    transaction.Status == TransactionStatus.Completed ? 
                        "Transação processada com sucesso" : transaction.ErrorMessage);
                
                _logger.LogInformation("SignalR notification sent successfully for transaction {TransactionId}", 
                    transaction.Id);
            }
            catch (Exception signalREx)
            {
                _logger.LogError(signalREx, "Error sending SignalR notification for transaction {TransactionId}", 
                    transaction.Id);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction {TransactionId}", transaction?.Id ?? "unknown");
            
            if (transaction != null)
            {
                transaction.Status = TransactionStatus.Failed;
                transaction.ErrorMessage = "Erro interno durante o processamento";

                try
                {
                    await SendSignalRNotificationSafeAsync(transaction.FromEmail, "TransactionStatusUpdated", 
                        transaction.Id, "Failed", transaction.ErrorMessage);
                }
                catch (Exception signalREx)
                {
                    _logger.LogError(signalREx, "Error sending SignalR notification");
                }
            }
        }
    }

    private async Task SendSignalRNotificationSafeAsync(string userEmail, string method, params object[] args)
    {
        try
        {
            if (_signalRConnection.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                _logger.LogWarning("SignalR connection is not active. State: {State}. Attempting to reconnect...", 
                    _signalRConnection.State);
                
                if (_signalRConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected)
                {
                    await _signalRConnection.StartAsync();
                    _logger.LogInformation("SignalR reconnected successfully");
                }
                else
                {
                    _logger.LogWarning("SignalR connection is in {State} state, cannot send notification", 
                        _signalRConnection.State);
                    return;
                }
            }

            var groupName = $"user_{userEmail}";
            _logger.LogInformation("Sending SignalR notification to group: {GroupName}", groupName);
            
            await _signalRConnection.InvokeAsync("SendToGroup", groupName, method, args[0], args[1], args[2]);
            _logger.LogInformation("SignalR notification sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR notification");
        }
    }

    private async Task CreateTransactionHistoryEntry(IDatabase database, string userEmail, Transaction transaction, string type, string otherPartyName)
    {
        try
        {
            var historyEntry = new
            {
                Id = Guid.NewGuid().ToString(),
                TransactionId = transaction.Id,
                Date = transaction.ProcessedAt ?? DateTime.UtcNow,
                Description = type == "sent" 
                    ? $"Transferência enviada para {otherPartyName}" 
                    : $"Transferência recebida de {otherPartyName}",
                Amount = type == "sent" ? -transaction.Amount : transaction.Amount,
                Currency = "BRL",
                Type = type,
                Status = "completed",
                RecipientEmail = type == "sent" ? $"{transaction.ToAgencia}-{transaction.ToConta}" : null,
                SenderEmail = type == "received" ? transaction.FromEmail : null,
                Balance = type == "sent" 
                    ? Users[userEmail].Balance 
                    : Users[userEmail].Balance
            };

            var historyKey = $"user_transactions:{userEmail}";
            await database.ListLeftPushAsync(historyKey, JsonSerializer.Serialize(historyEntry));
            
            await database.ListTrimAsync(historyKey, 0, 99);
            
            _logger.LogInformation("Entrada no extrato criada para {UserEmail}: {Type} R$ {Amount}", 
                userEmail, type, Math.Abs(historyEntry.Amount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar entrada no extrato para {UserEmail}", userEmail);
        }
    }
}
