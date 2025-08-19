using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Collections.Concurrent;

namespace PaymentGateway.ApiGateway.Controllers;

public class RegisteredUser
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Agencia { get; set; } = string.Empty;
    public string Conta { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 1000.00m; 
}

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    
    
    private static readonly ConcurrentDictionary<string, RegisteredUser> _registeredUsers = new();

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    
    
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Email}", request.Email);

        try
        {
            
            var httpClient = new HttpClient();
            var validateResponse = await httpClient.PostAsJsonAsync("http:
            {
                Email = request.Email,
                Password = request.Password
            });

            if (!validateResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Login failed for user: {Email}", request.Email);
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var validationResult = await validateResponse.Content.ReadFromJsonAsync<ValidationResult>();
            if (!validationResult.Valid)
            {
                _logger.LogWarning("Login failed for user: {Email}", request.Email);
                return Unauthorized(new { error = "Invalid credentials" });
            }

            
            var userResponse = await httpClient.GetAsync($"http:
            if (!userResponse.IsSuccessStatusCode)
            {
                return StatusCode(500, new { error = "Error retrieving user data" });
            }

            var user = await userResponse.Content.ReadFromJsonAsync<UserInfo>();
            var token = GenerateJwtToken(request.Email, user.Role);

            _logger.LogInformation("Login successful for user: {Email}", request.Email);

            return Ok(new LoginResponse
            {
                Token = token,
                ExpiresIn = 3600,
                TokenType = "Bearer",
                User = user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Email}", request.Email);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    
    
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Registration attempt for user: {Email}", request.Email);

        try
        {
            
            var httpClient = new HttpClient();
            var registerResponse = await httpClient.PostAsJsonAsync("http:
            {
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                Role = request.Role ?? "User"
            });

            if (!registerResponse.IsSuccessStatusCode)
            {
                var errorContent = await registerResponse.Content.ReadAsStringAsync();
                return Conflict(new { error = "Usuário já existe ou erro na criação" });
            }

            var user = await registerResponse.Content.ReadFromJsonAsync<UserInfo>();
            var token = GenerateJwtToken(request.Email, user.Role);

            _logger.LogInformation("User registered successfully: {Email}", request.Email);

            return Created("/api/auth/login", new LoginResponse
            {
                Token = token,
                ExpiresIn = 3600,
                TokenType = "Bearer",
                User = user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Email}", request.Email);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    
    
    
    [HttpPost("validate")]
    public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? "PaymentGateway-Secret-Key-2025-Super-Secure");
            
            tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var email = jwtToken.Claims.First(x => x.Type == ClaimTypes.Email).Value;
            var role = jwtToken.Claims.First(x => x.Type == ClaimTypes.Role).Value;

            return Ok(new
            {
                Valid = true,
                Email = email,
                Role = role,
                ExpiresAt = jwtToken.ValidTo
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return BadRequest(new { Valid = false, Error = "Invalid token" });
        }
    }

    
    
    
    [HttpPost("refresh")]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
    {
        
        
        
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? "PaymentGateway-Secret-Key-2025-Super-Secure");
            
            var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false 
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var email = jwtToken.Claims.First(x => x.Type == ClaimTypes.Email).Value;
            var role = jwtToken.Claims.First(x => x.Type == ClaimTypes.Role).Value;

            var newToken = GenerateJwtToken(email, role);

            return Ok(new
            {
                Token = newToken,
                ExpiresIn = 3600,
                TokenType = "Bearer"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed");
            return BadRequest(new { error = "Invalid refresh token" });
        }
    }

    
    
    
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        _logger.LogInformation("Transfer request from {FromEmail} amount: {Amount}", request.FromEmail, request.Amount);

        try
        {
            
            var httpClient = new HttpClient();
            var transferResponse = await httpClient.PostAsJsonAsync("http:

            if (!transferResponse.IsSuccessStatusCode)
            {
                var errorContent = await transferResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Transfer failed: {ErrorContent}", errorContent);
                return BadRequest(new { error = errorContent });
            }

            var result = await transferResponse.Content.ReadFromJsonAsync<object>();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transfer");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    
    
    
    [HttpGet("balance/{email}")]
    public IActionResult GetBalance(string email)
    {
        var user = FindUserByEmail(email);
        if (user == null)
        {
            return NotFound(new { error = "Usuário não encontrado" });
        }

        return Ok(new
        {
            Email = user.Email,
            Name = user.Name,
            Agencia = user.Agencia,
            Conta = user.Conta,
            Balance = user.Balance
        });
    }

    private string GenerateJwtToken(string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? "PaymentGateway-Secret-Key-2025-Super-Secure"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
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

    
    
    
    private RegisteredUser? FindUserByEmail(string email)
    {
        
        if (email == "admin@paymentgateway.com")
        {
            return new RegisteredUser
            {
                Email = email,
                Name = "Administrator",
                Role = "Admin",
                Agencia = "34567-8",
                Conta = "12345678-90",
                Balance = 5000.00m
            };
        }
        else if (email == "user@paymentgateway.com")
        {
            return new RegisteredUser
            {
                Email = email,
                Name = "Regular User",
                Role = "User",
                Agencia = "23456-7",
                Conta = "98765432-10",
                Balance = 2500.00m
            };
        }

        
        if (_registeredUsers.TryGetValue(email, out var user))
        {
            return user;
        }

        return null;
    }

    
    
    
    private RegisteredUser? FindUserByAccount(string agencia, string conta)
    {
        
        if (agencia == "34567-8" && conta == "12345678-90")
        {
            return new RegisteredUser
            {
                Email = "admin@paymentgateway.com",
                Name = "Administrator",
                Role = "Admin",
                Agencia = agencia,
                Conta = conta,
                Balance = 5000.00m
            };
        }
        else if (agencia == "23456-7" && conta == "98765432-10")
        {
            return new RegisteredUser
            {
                Email = "user@paymentgateway.com",
                Name = "Regular User",
                Role = "User",
                Agencia = agencia,
                Conta = conta,
                Balance = 2500.00m
            };
        }

        
        foreach (var user in _registeredUsers.Values)
        {
            if (user.Agencia == agencia && user.Conta == conta)
            {
                return user;
            }
        }

        return null;
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Name, string Email, string Password, string? Role = "User");
public record ValidateTokenRequest(string Token);
public record RefreshTokenRequest(string Token);
public record TransferRequest(
    string FromEmail,
    string? ToEmail = null,
    string? ToAgencia = null,
    string? ToConta = null,
    decimal Amount = 0
);

public class ValidationResult
{
    public bool Valid { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class TransferResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public decimal NewBalance { get; set; }
    public string Message { get; set; } = string.Empty;
}

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string TokenType { get; init; } = string.Empty;
    public UserInfo User { get; init; } = new();
}

public record UserInfo
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Agencia { get; init; } = string.Empty;
    public string Conta { get; init; } = string.Empty;
    public decimal Balance { get; init; } = 0;
}
