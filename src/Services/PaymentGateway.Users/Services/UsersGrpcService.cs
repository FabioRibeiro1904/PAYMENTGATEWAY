using Grpc.Core;
using PaymentGateway.Users.Grpc;
using System.Collections.Concurrent;

namespace PaymentGateway.Users.Services;

public class BankUser
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Agencia { get; set; } = string.Empty;
    public string Conta { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 1000.00m;
}

public class UsersGrpcService : UsersService.UsersServiceBase
{
    private readonly ILogger<UsersGrpcService> _logger;
    private static readonly ConcurrentDictionary<string, BankUser> _users = new();

    public UsersGrpcService(ILogger<UsersGrpcService> logger)
    {
        _logger = logger;
    }

    public override Task<RegisterUserResponse> RegisterUser(RegisterUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC RegisterUser called for: {Email}", request.Email);

        try
        {
            if (_users.ContainsKey(request.Email))
            {
                return Task.FromResult(new RegisterUserResponse
                {
                    Success = false,
                    Message = "Usuário já existe"
                });
            }

            var user = new BankUser
            {
                Email = request.Email,
                Name = request.Name,
                Password = request.Password,
                Role = string.IsNullOrEmpty(request.Role) ? "User" : request.Role,
                Agencia = GenerateAgencia(),
                Conta = GenerateConta(),
                Balance = 1000.00m
            };

            _users[request.Email] = user;

            return Task.FromResult(new RegisterUserResponse
            {
                Success = true,
                Message = "Usuário criado com sucesso",
                User = new Grpc.BankUser
                {
                    Email = user.Email,
                    Name = user.Name,
                    Role = user.Role,
                    Agencia = user.Agencia,
                    Conta = user.Conta,
                    Balance = (double)user.Balance
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user: {Email}", request.Email);
            return Task.FromResult(new RegisterUserResponse
            {
                Success = false,
                Message = "Erro interno do servidor"
            });
        }
    }

    public override Task<GetUserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetUser called for: {Email}", request.Email);

        try
        {
            
            if (request.Email == "admin@paymentgateway.com")
            {
                return Task.FromResult(new GetUserResponse
                {
                    Success = true,
                    Message = "Usuário encontrado",
                    User = new Grpc.BankUser
                    {
                        Email = request.Email,
                        Name = "Administrator",
                        Role = "Admin",
                        Agencia = "34567-8",
                        Conta = "12345678-90",
                        Balance = 5000.00
                    }
                });
            }
            else if (request.Email == "user@paymentgateway.com")
            {
                return Task.FromResult(new GetUserResponse
                {
                    Success = true,
                    Message = "Usuário encontrado",
                    User = new Grpc.BankUser
                    {
                        Email = request.Email,
                        Name = "Regular User",
                        Role = "User",
                        Agencia = "23456-7",
                        Conta = "98765432-10",
                        Balance = 2500.00
                    }
                });
            }

            if (_users.TryGetValue(request.Email, out var user))
            {
                return Task.FromResult(new GetUserResponse
                {
                    Success = true,
                    Message = "Usuário encontrado",
                    User = new Grpc.BankUser
                    {
                        Email = user.Email,
                        Name = user.Name,
                        Role = user.Role,
                        Agencia = user.Agencia,
                        Conta = user.Conta,
                        Balance = (double)user.Balance
                    }
                });
            }

            return Task.FromResult(new GetUserResponse
            {
                Success = false,
                Message = "Usuário não encontrado"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user: {Email}", request.Email);
            return Task.FromResult(new GetUserResponse
            {
                Success = false,
                Message = "Erro interno do servidor"
            });
        }
    }

    public override Task<ValidateUserResponse> ValidateUser(ValidateUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC ValidateUser called for: {Email}", request.Email);

        try
        {
            
            if (request.Email == "admin@paymentgateway.com" && request.Password == "Admin123!")
            {
                return Task.FromResult(new ValidateUserResponse
                {
                    Valid = true,
                    Role = "Admin",
                    Message = "Credenciais válidas"
                });
            }
            else if (request.Email == "user@paymentgateway.com" && request.Password == "User123!")
            {
                return Task.FromResult(new ValidateUserResponse
                {
                    Valid = true,
                    Role = "User",
                    Message = "Credenciais válidas"
                });
            }

            if (_users.TryGetValue(request.Email, out var user) && user.Password == request.Password)
            {
                return Task.FromResult(new ValidateUserResponse
                {
                    Valid = true,
                    Role = user.Role,
                    Message = "Credenciais válidas"
                });
            }

            return Task.FromResult(new ValidateUserResponse
            {
                Valid = false,
                Role = "",
                Message = "Credenciais inválidas"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user: {Email}", request.Email);
            return Task.FromResult(new ValidateUserResponse
            {
                Valid = false,
                Role = "",
                Message = "Erro interno do servidor"
            });
        }
    }

    public override Task<UpdateBalanceResponse> UpdateBalance(UpdateBalanceRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC UpdateBalance called for: {Email}, NewBalance: {Balance}", request.Email, request.NewBalance);

        try
        {
            if (_users.TryGetValue(request.Email, out var user))
            {
                user.Balance = (decimal)request.NewBalance;
                return Task.FromResult(new UpdateBalanceResponse
                {
                    Success = true,
                    Message = "Saldo atualizado com sucesso",
                    NewBalance = request.NewBalance
                });
            }

            return Task.FromResult(new UpdateBalanceResponse
            {
                Success = false,
                Message = "Usuário não encontrado",
                NewBalance = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating balance for user: {Email}", request.Email);
            return Task.FromResult(new UpdateBalanceResponse
            {
                Success = false,
                Message = "Erro interno do servidor",
                NewBalance = 0
            });
        }
    }

    public override Task<GetUserResponse> GetUserByAccount(GetUserByAccountRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetUserByAccount called for: {Agencia}-{Conta}", request.Agencia, request.Conta);

        try
        {
            
            if (request.Agencia == "34567-8" && request.Conta == "12345678-90")
            {
                return Task.FromResult(new GetUserResponse
                {
                    Success = true,
                    Message = "Usuário encontrado",
                    User = new Grpc.BankUser
                    {
                        Email = "admin@paymentgateway.com",
                        Name = "Administrator",
                        Role = "Admin",
                        Agencia = request.Agencia,
                        Conta = request.Conta,
                        Balance = 5000.00
                    }
                });
            }
            else if (request.Agencia == "23456-7" && request.Conta == "98765432-10")
            {
                return Task.FromResult(new GetUserResponse
                {
                    Success = true,
                    Message = "Usuário encontrado",
                    User = new Grpc.BankUser
                    {
                        Email = "user@paymentgateway.com",
                        Name = "Regular User",
                        Role = "User",
                        Agencia = request.Agencia,
                        Conta = request.Conta,
                        Balance = 2500.00
                    }
                });
            }

            
            foreach (var user in _users.Values)
            {
                if (user.Agencia == request.Agencia && user.Conta == request.Conta)
                {
                    return Task.FromResult(new GetUserResponse
                    {
                        Success = true,
                        Message = "Usuário encontrado",
                        User = new Grpc.BankUser
                        {
                            Email = user.Email,
                            Name = user.Name,
                            Role = user.Role,
                            Agencia = user.Agencia,
                            Conta = user.Conta,
                            Balance = (double)user.Balance
                        }
                    });
                }
            }

            return Task.FromResult(new GetUserResponse
            {
                Success = false,
                Message = "Usuário não encontrado"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by account: {Agencia}-{Conta}", request.Agencia, request.Conta);
            return Task.FromResult(new GetUserResponse
            {
                Success = false,
                Message = "Erro interno do servidor"
            });
        }
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
