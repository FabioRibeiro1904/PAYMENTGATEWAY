using Confluent.Kafka;
using PaymentGateway.Payments.Models;
using System.Text.Json;

namespace PaymentGateway.Payments.Services;

public interface IKafkaProducerService
{
    Task SendTransactionAsync(Transaction transaction);
}

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;
    private const string TopicName = "payment-transactions";

    public KafkaProducerService(ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        
        var config = new ProducerConfig
        {
            BootstrapServers = "localhost:9092",
            ClientId = "payment-gateway-producer"
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task SendTransactionAsync(Transaction transaction)
    {
        try
        {
            var message = JsonSerializer.Serialize(transaction);
            var kafkaMessage = new Message<string, string>
            {
                Key = transaction.Id,
                Value = message
            };

            var result = await _producer.ProduceAsync(TopicName, kafkaMessage);
            _logger.LogInformation("Transaction {TransactionId} sent to Kafka topic {Topic}", 
                transaction.Id, TopicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transaction {TransactionId} to Kafka", transaction.Id);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
