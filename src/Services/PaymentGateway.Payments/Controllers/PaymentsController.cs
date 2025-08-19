using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Payments.Models;
using PaymentGateway.Payments.Services;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace PaymentGateway.Payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ILogger<PaymentsController> _logger;
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly IRedisService _redisService;
    private static readonly ConcurrentDictionary<string, Transaction> _transactions = new();

    public PaymentsController(
        ILogger<PaymentsController> logger, 
        IKafkaProducerService kafkaProducer,
        IRedisService redisService)
    {
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _redisService = redisService;
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> ProcessTransfer([FromBody] TransactionRequest request)
    {
        try
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "joao@email.com"; 

            _logger.LogInformation("Processing transfer from {FromEmail} to {ToAgencia}-{ToConta} amount: {Amount}", 
                userEmail, request.ToAgencia, request.ToConta, request.Amount);

            if (request.Amount <= 0)
            {
                return BadRequest(new TransactionResponse 
                { 
                    Status = "Failed", 
                    Message = "Valor deve ser maior que zero" 
                });
            }

            if (string.IsNullOrEmpty(request.ToAgencia) || string.IsNullOrEmpty(request.ToConta))
            {
                return BadRequest(new TransactionResponse 
                { 
                    Status = "Failed", 
                    Message = "Agência e conta são obrigatórios" 
                });
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                FromEmail = userEmail,
                ToAgencia = request.ToAgencia,
                ToConta = request.ToConta,
                Amount = request.Amount,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _transactions.TryAdd(transaction.Id, transaction);

            await _redisService.SetTransactionStatusAsync(transaction.Id, TransactionStatus.Pending);

            await _kafkaProducer.SendTransactionAsync(transaction);

            return Ok(new TransactionResponse
            {
                TransactionId = transaction.Id,
                Status = "Pending",
                Message = "Transação enviada para processamento. Você receberá atualizações em tempo real."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transfer");
            return StatusCode(500, new TransactionResponse 
            { 
                Status = "Failed", 
                Message = "Erro interno do servidor" 
            });
        }
    }

    [HttpPost("transfer-by-email")]
    public async Task<IActionResult> ProcessTransferByEmail([FromBody] EmailTransferRequest request)
    {
        try
        {
            _logger.LogInformation("Processing transfer from {FromEmail} to {ToEmail} amount: {Amount}", 
                request.FromEmail, request.ToEmail, request.Amount);

            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Valor deve ser maior que zero" });
            }

            if (string.IsNullOrEmpty(request.ToEmail))
            {
                return BadRequest(new { error = "Email do destinatário é obrigatório" });
            }

            string toAgencia;
            string toConta;
            
            var testUsers = new Dictionary<string, (string Agencia, string Conta)>
            {
                ["joao@email.com"] = ("12345-6", "12345678-90"),
                ["maria@email.com"] = ("56789-0", "67890123-45"),
                ["pedro@email.com"] = ("99999-8", "11111111-22"),
                ["admin@paymentgateway.com"] = ("00000-1", "00000000-01"),
                ["teste15@gmail.com"] = ("55555-5", "55555555-55")
            };

            if (testUsers.TryGetValue(request.ToEmail, out var userData))
            {
                toAgencia = userData.Agencia;
                toConta = userData.Conta;
                _logger.LogInformation("Found recipient: {Email} -> Agencia: {Agencia}, Conta: {Conta}", 
                    request.ToEmail, toAgencia, toConta);
            }
            else
            {
                _logger.LogWarning("Recipient not found: {Email}", request.ToEmail);
                return BadRequest(new { error = $"Usuário destinatário não encontrado: {request.ToEmail}" });
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                FromEmail = request.FromEmail,
                ToEmail = request.ToEmail,
                ToAgencia = toAgencia,
                ToConta = toConta,
                Amount = request.Amount,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _transactions.TryAdd(transaction.Id, transaction);

            await _redisService.SetTransactionStatusAsync(transaction.Id, TransactionStatus.Pending);

            await _kafkaProducer.SendTransactionAsync(transaction);

            return Ok(new
            {
                success = true,
                transactionId = transaction.Id,
                message = "Transação enviada para processamento"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email transfer");
            return StatusCode(500, new { error = "Erro interno do servidor" });
        }
    }

    [HttpGet("status/{transactionId}")]
    public async Task<IActionResult> GetTransactionStatus(string transactionId)
    {
        try
        {
            var redisStatus = await _redisService.GetTransactionStatusAsync(transactionId);
            
            if (_transactions.TryGetValue(transactionId, out var transaction))
            {
                if (redisStatus.HasValue && redisStatus.Value != transaction.Status)
                {
                    transaction.Status = redisStatus.Value;
                    if (redisStatus.Value == TransactionStatus.Completed)
                    {
                        transaction.ProcessedAt = DateTime.UtcNow;
                    }
                }

                return Ok(new
                {
                    TransactionId = transactionId,
                    Status = transaction.Status.ToString(),
                    Amount = transaction.Amount,
                    ToAgencia = transaction.ToAgencia,
                    ToConta = transaction.ToConta,
                    ToUserName = transaction.ToUserName,
                    CreatedAt = transaction.CreatedAt,
                    ProcessedAt = transaction.ProcessedAt,
                    ErrorMessage = transaction.ErrorMessage
                });
            }

            if (redisStatus.HasValue)
            {
                return Ok(new
                {
                    TransactionId = transactionId,
                    Status = redisStatus.Value.ToString()
                });
            }

            return NotFound(new { Message = "Transação não encontrada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction status for {TransactionId}", transactionId);
            return StatusCode(500, new { Message = "Erro interno do servidor" });
        }
    }

    [HttpGet("history/{email}")]
    public async Task<IActionResult> GetTransactionHistory(string email)
    {
        try
        {
            var historyKey = $"user_transactions:{email}";
            var redisTransactions = await _redisService.GetUserTransactionsAsync(email);
            
            if (redisTransactions?.Any() == true)
            {
                _logger.LogInformation("Found {Count} transactions in Redis for {Email}", redisTransactions.Count, email);
                return Ok(redisTransactions);
            }
            
            var userTransactions = _transactions.Values
                .Where(t => t.FromEmail == email)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    date = t.CreatedAt,
                    description = $"Transferência enviada para {t.ToUserName ?? "Usuário"}",
                    amount = -t.Amount,
                    currency = "BRL",
                    type = "sent",
                    status = t.Status == TransactionStatus.Completed ? "completed" : 
                             t.Status == TransactionStatus.Failed ? "failed" : "pending",
                    recipientEmail = t.ToEmail,
                    senderEmail = (string?)null
                })
                .ToList();

            return Ok(userTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction history for {Email}", email);
            return StatusCode(500, new { Message = "Erro ao buscar histórico" });
        }
    }

    [HttpGet("balance/{email}")]
    public async Task<IActionResult> GetUserBalance(string email)
    {
        try
        {
            var userBalance = await _redisService.GetUserBalanceAsync(email);
            
            if (userBalance.HasValue)
            {
                return Ok(new { 
                    email = email,
                    balance = userBalance.Value,
                    currency = "BRL",
                    lastUpdated = DateTime.UtcNow
                });
            }
            
            return Ok(new { 
                email = email,
                balance = 1000.00m,
                currency = "BRL",
                lastUpdated = DateTime.UtcNow,
                note = "Saldo padrão (não encontrado no Redis)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for {Email}", email);
            return StatusCode(500, new { Message = "Erro ao buscar saldo" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Payments API", Timestamp = DateTime.UtcNow });
    }
}
