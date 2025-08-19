using PaymentGateway.TransactionProcessor;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<TransactionProcessorService>();

var host = builder.Build();
host.Run();
