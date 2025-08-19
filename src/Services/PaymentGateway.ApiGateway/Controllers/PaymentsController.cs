using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(ILogger<PaymentsController> logger)
    {
        _logger = logger;
    }

    
    
    
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        _logger.LogInformation("Processing payment for amount {Amount} {Currency}", request.Amount, request.Currency);

        
        if (request.Amount <= 0)
        {
            return BadRequest(new { error = "Valor deve ser maior que zero" });
        }

        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return BadRequest(new { error = "Email do destinatário é obrigatório" });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "Descrição é obrigatória" });
        }

        
        await Task.Delay(1000);

        
        var random = new Random();
        var isSuccess = random.NextDouble() > 0.1;

        if (isSuccess)
        {
            var paymentId = Guid.NewGuid();
            var response = new
            {
                Id = paymentId,
                Amount = request.Amount,
                Currency = request.Currency,
                RecipientEmail = request.RecipientEmail,
                Description = request.Description,
                Status = "Completed",
                TransactionId = $"TXN_{DateTime.UtcNow:yyyyMMddHHmmss}_{paymentId.ToString()[..8]}",
                ProcessedAt = DateTime.UtcNow,
                Fee = Math.Round(request.Amount * 0.029m, 2), 
                NetAmount = Math.Round(request.Amount * 0.971m, 2)
            };

            _logger.LogInformation("Payment {PaymentId} processed successfully", paymentId);
            return Ok(response);
        }
        else
        {
            _logger.LogWarning("Payment processing failed for amount {Amount}", request.Amount);
            return BadRequest(new { 
                error = "Falha no processamento do pagamento",
                reason = "Fundos insuficientes ou cartão recusado",
                code = "PAYMENT_DECLINED"
            });
        }
    }

    
    
    
    [HttpGet("history")]
    public IActionResult GetPaymentHistory()
    {
        _logger.LogInformation("Getting payment history");

        
        var history = new[]
        {
            new {
                Id = Guid.NewGuid(),
                Amount = 150.00m,
                Currency = "BRL",
                RecipientEmail = "cliente1@email.com",
                Description = "Pagamento de serviços",
                Status = "Completed",
                ProcessedAt = DateTime.UtcNow.AddHours(-2)
            },
            new {
                Id = Guid.NewGuid(),
                Amount = 75.50m,
                Currency = "BRL",
                RecipientEmail = "cliente2@email.com",
                Description = "Compra de produtos",
                Status = "Completed",
                ProcessedAt = DateTime.UtcNow.AddHours(-5)
            },
            new {
                Id = Guid.NewGuid(),
                Amount = 230.99m,
                Currency = "BRL",
                RecipientEmail = "cliente3@email.com",
                Description = "Assinatura mensal",
                Status = "Failed",
                ProcessedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        return Ok(history);
    }

    
    
    
    [HttpGet("stats")]
    public IActionResult GetPaymentStats()
    {
        _logger.LogInformation("Getting payment statistics");

        var stats = new
        {
            TotalPayments = 1247,
            CompletedPayments = 1189,
            FailedPayments = 58,
            TotalAmount = 187450.75m,
            TotalFees = 5436.07m,
            Currency = "BRL",
            SuccessRate = 95.3m,
            AverageAmount = 150.36m,
            Today = new
            {
                Payments = 23,
                Amount = 3456.78m,
                Fees = 100.25m
            }
        };

        return Ok(stats);
    }
}

public record ProcessPaymentRequest(
    decimal Amount,
    string Currency,
    string RecipientEmail,
    string Description
);
