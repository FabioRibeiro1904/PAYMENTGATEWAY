namespace PaymentGateway.Payments.Models;

public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromEmail { get; set; } = string.Empty;
    public string? ToEmail { get; set; }
    public string ToAgencia { get; set; } = string.Empty;
    public string ToConta { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ToUserName { get; set; }
}

public enum TransactionStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class TransactionRequest
{
    public string ToAgencia { get; set; } = string.Empty;
    public string ToConta { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TransactionResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class EmailTransferRequest
{
    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
