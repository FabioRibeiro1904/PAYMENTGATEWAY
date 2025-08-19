using PaymentGateway.Payments.Hubs;
using PaymentGateway.Payments.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
var useRealInfrastructure = await CheckInfrastructureAsync();

if (useRealInfrastructure)
{
    Console.WriteLine("Infrastructure detected! Using real Kafka and Redis services");
    
    
    builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        return ConnectionMultiplexer.Connect(connectionString);
    });
    builder.Services.AddSingleton<IRedisService, RedisService>();
    
    
    builder.Services.AddSingleton<IKafkaProducerService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<KafkaProducerService>>();
        return new KafkaProducerService(logger);
    });
}
else
{
    Console.WriteLine("Infrastructure not detected. Using mock services for development");
    
    
    builder.Services.AddSingleton<IConnectionMultiplexer>(provider => null!);
    builder.Services.AddSingleton<IRedisService, MockRedisService>();
    builder.Services.AddSingleton<IKafkaProducerService, MockKafkaProducerService>();
}

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TransactionHub>("/transactionHub");
app.MapGet("/health", () => new { Status = "Healthy", Service = "PaymentGateway.Payments", Timestamp = DateTime.UtcNow });

app.Run();
static async Task<bool> CheckInfrastructureAsync()
{
    try
    {
        
        using var redis = ConnectionMultiplexer.Connect("localhost:6379");
        var database = redis.GetDatabase();
        await database.PingAsync();
        Console.WriteLine("🔗 Redis connection successful");

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Infrastructure check failed: {ex.Message}");
        return false;
    }
}
