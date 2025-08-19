using Grpc.Core;
using PaymentGateway.Users.Grpc;
using PaymentGateway.Users.Controllers;
using System.Collections.Concurrent;

namespace PaymentGateway.Users.Services;

public class GrpcUsersService : UsersService.UsersServiceBase
{
    private static readonly ConcurrentDictionary<string, Controllers.BankUser> Users = new();
    private readonly ILogger<GrpcUsersService> _logger;

    public GrpcUsersService(ILogger<GrpcUsersService> logger)
    {
        _logger = logger;
        InitializeTestUsers();
    }

    private void InitializeTestUsers()
    {
        
        var testUsers = new[]
        {
            new Controllers.BankUser
            {
                Name = "João Silva",
                Email = "joao@email.com",
                Password = "senha123",
                Conta = "12345678-90",
                Agencia = "12345-6",
                Balance = 1000.00m
            },
            new Controllers.BankUser
            {
                Name = "Maria Santos",
                Email = "maria@email.com",
                Password = "senha456",
                Conta = "67890123-45",
                Agencia = "56789-0",
                Balance = 1500.00m
            },
            new Controllers.BankUser
            {
                Name = "Pedro Costa",
                Email = "pedro@email.com",
                Password = "senha789",
                Conta = "11111111-22",
                Agencia = "99999-8",
                Balance = 1000.00m
            }
        };

        foreach (var user in testUsers)
        {
            Users.TryAdd(user.Email, user);
        }
    }

    public override Task<Grpc.RegisterUserResponse> RegisterUser(Grpc.RegisterUserRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Registering user: {Email}", request.Email);

            if (Users.ContainsKey(request.Email))
            {
                return Task.FromResult(new Grpc.RegisterUserResponse
                {
                    Success = false,
                    Message = "User already exists"
                });
            }

            var newUser = new Controllers.BankUser
            {
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                Conta = GenerateAccountNumber(),
                Agencia = GenerateAgency(),
                Balance = 1000.00m
            };

            Users.TryAdd(request.Email, newUser);

            return Task.FromResult(new Grpc.RegisterUserResponse
            {
                Success = true,
                Message = "User registered successfully",
                User = MapToGrpcUser(newUser)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return Task.FromResult(new Grpc.RegisterUserResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    public override Task<Grpc.GetUserResponse> GetUser(Grpc.GetUserRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Getting user: {Email}", request.Email);

            if (Users.TryGetValue(request.Email, out var user))
            {
                return Task.FromResult(new Grpc.GetUserResponse
                {
                    Success = true,
                    User = MapToGrpcUser(user)
                });
            }

            return Task.FromResult(new Grpc.GetUserResponse
            {
                Success = false,
                Message = "User not found"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user");
            return Task.FromResult(new Grpc.GetUserResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    public override Task<Grpc.ValidateUserResponse> ValidateUser(Grpc.ValidateUserRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Validating user: {Email}", request.Email);

            if (Users.TryGetValue(request.Email, out var user) && user.Password == request.Password)
            {
                return Task.FromResult(new Grpc.ValidateUserResponse
                {
                    Valid = true,
                    Role = "User",
                    Message = "User validated successfully"
                });
            }

            return Task.FromResult(new Grpc.ValidateUserResponse
            {
                Valid = false,
                Message = "Invalid credentials"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user");
            return Task.FromResult(new Grpc.ValidateUserResponse
            {
                Valid = false,
                Message = "Internal server error"
            });
        }
    }

    public override async Task<Grpc.UpdateBalanceResponse> UpdateBalance(Grpc.UpdateBalanceRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Updating balance for user: {Email}, Balance: {Balance}", request.Email, request.NewBalance);

            if (Users.TryGetValue(request.Email, out var user))
            {
                user.Balance = (decimal)request.NewBalance;

                return new Grpc.UpdateBalanceResponse
                {
                    Success = true,
                    NewBalance = (double)user.Balance,
                    Message = "Balance updated successfully"
                };
            }

            return new Grpc.UpdateBalanceResponse
            {
                Success = false,
                Message = "User not found"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating balance");
            return new Grpc.UpdateBalanceResponse
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }

    public override async Task<Grpc.GetUserResponse> GetUserByAccount(Grpc.GetUserByAccountRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Getting user by account: {Agency}-{Account}", request.Agencia, request.Conta);

            var user = Users.Values.FirstOrDefault(u => u.Agencia == request.Agencia && u.Conta == request.Conta);

            if (user != null)
            {
                return new Grpc.GetUserResponse
                {
                    Success = true,
                    User = MapToGrpcUser(user)
                };
            }

            return new Grpc.GetUserResponse
            {
                Success = false,
                Message = "User not found"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by account");
            return new Grpc.GetUserResponse
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }

    private Grpc.BankUser MapToGrpcUser(Controllers.BankUser user)
    {
        return new Grpc.BankUser
        {
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            Agencia = user.Agencia,
            Conta = user.Conta,
            Balance = (double)user.Balance
        };
    }

    private string GenerateAccountNumber()
    {
        var random = new Random();
        var conta = random.Next(10000000, 99999999);
        var digitoVerificador = random.Next(10, 99);
        return $"{conta}-{digitoVerificador}";
    }

    private string GenerateAgency()
    {
        var random = new Random();
        var agencia = random.Next(10000, 99999);
        var digitoVerificador = random.Next(0, 9);
        return $"{agencia}-{digitoVerificador}";
    }
}
