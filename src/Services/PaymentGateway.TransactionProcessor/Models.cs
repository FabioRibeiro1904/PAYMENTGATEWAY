using System.Text.Json;

namespace PaymentGateway.TransactionProcessor;

public enum TransactionStatus
{
    Pending,
    Processing, 
    Completed,
    Failed
}

public class Transaction
{
    public string Id { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ToAgencia { get; set; } = string.Empty;
    public string ToConta { get; set; } = string.Empty;
    public string ToUserName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

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
