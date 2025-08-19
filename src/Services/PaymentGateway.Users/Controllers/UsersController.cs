using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace PaymentGateway.Users.Controllers;

public class BankUser
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;
    public string Agencia { get; set; } = string.Empty;
    public string Conta { get; set; } = string.Empty;
    
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Checking"; 
    public string BankBranch { get; set; } = string.Empty;
    public string RoutingNumber { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; } = 0;
    public decimal AvailableCredit => CreditLimit - Balance;
    public string Currency { get; set; } = "BRL";
    public List<string> AllowedCountries { get; set; } = new() { "BR", "US", "EU" };
    
    public string FullName => Name;
    public string DisplayName => !string.IsNullOrEmpty(Name) ? Name.Split(' ')[0] : Email.Split('@')[0];
    public string ProfilePicture { get; set; } = "/assets/default-avatar.png";
    public Dictionary<string, object> Preferences { get; set; } = new();
    public List<string> NotificationSettings { get; set; } = new() { "email", "sms", "push" };
    
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }
    public List<string> LoginHistory { get; set; } = new();
    public DateTime? LastPasswordChange { get; set; }
    public bool RequiresPasswordChange { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? AccountLockedUntil { get; set; }
    
    public bool IsAccountLocked => AccountLockedUntil.HasValue && AccountLockedUntil > DateTime.UtcNow;
    public bool CanMakeTransaction(decimal amount) => IsActive && !IsAccountLocked && (Balance + AvailableCredit) >= amount;
    public string GetAccountDisplayNumber() => $"****{AccountNumber[^4..]}";
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private static readonly ConcurrentDictionary<string, BankUser> _users = new();

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public IActionResult RegisterUser([FromBody] RegisterUserRequest request)
    {
        _logger.LogInformation("Registering user: {Email}", request.Email);

        if (_users.ContainsKey(request.Email))
        {
            return Conflict(new { error = "Usuário já existe" });
        }

        var user = new BankUser
        {
            Email = request.Email,
            Name = request.Name,
            Password = request.Password,
            Role = request.Role ?? "User",
            Agencia = GenerateAgencia(),
            Conta = GenerateConta(),
            Balance = 1000.00m
        };

        _users[request.Email] = user;

        return Ok(new
        {
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            Agencia = user.Agencia,
            Conta = user.Conta,
            Balance = user.Balance
        });
    }

    [HttpGet("{email}")]
    public IActionResult GetUser(string email)
    {
        _logger.LogInformation("Getting user: {Email}", email);

        if (email == "admin@paymentgateway.com")
        {
            return Ok(new
            {
                Email = email,
                Name = "Administrator",
                Role = "Admin",
                Agencia = "34567-8",
                Conta = "12345678-90",
                Balance = 5000.00m
            });
        }
        else if (email == "user@paymentgateway.com")
        {
            return Ok(new
            {
                Email = email,
                Name = "Regular User",
                Role = "User",
                Agencia = "23456-7",
                Conta = "98765432-10",
                Balance = 2500.00m
            });
        }

        if (_users.TryGetValue(email, out var user))
        {
            return Ok(new
            {
                user.Email,
                user.Name,
                user.Role,
                user.Agencia,
                user.Conta,
                user.Balance
            });
        }

        return NotFound(new { error = "Usuário não encontrado" });
    }

    [HttpPost("validate")]
    public IActionResult ValidateUser([FromBody] ValidateUserRequest request)
    {
        if (request.Email == "admin@paymentgateway.com" && request.Password == "Admin123!")
        {
            return Ok(new { Valid = true, Role = "Admin" });
        }
        else if (request.Email == "user@paymentgateway.com" && request.Password == "User123!")
        {
            return Ok(new { Valid = true, Role = "User" });
        }

        if (_users.TryGetValue(request.Email, out var user) && user.Password == request.Password)
        {
            return Ok(new { Valid = true, Role = user.Role });
        }

        return Ok(new { Valid = false });
    }

    [HttpPut("{email}/balance")]
    public IActionResult UpdateBalance(string email, [FromBody] UpdateBalanceRequest request)
    {
        if (_users.TryGetValue(email, out var user))
        {
            user.Balance = request.NewBalance;
            return Ok(new { user.Email, user.Balance });
        }

        return NotFound(new { error = "Usuário não encontrado" });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Users API" });
    }

    private string GenerateAgencia()
    {
        var random = new Random();
        var agencia = random.Next(10000, 99999);
        var digitoVerificador = random.Next(0, 9);
        return $"{agencia}-{digitoVerificador}";
    }

    private string GenerateConta()
    {
        var random = new Random();
        var conta = random.Next(10000000, 99999999);
        var digitoVerificador = random.Next(10, 99);
        return $"{conta}-{digitoVerificador}";
    }
}

public record RegisterUserRequest(string Name, string Email, string Password, string? Role = "User");
public record ValidateUserRequest(string Email, string Password);
public record UpdateBalanceRequest(decimal NewBalance);
