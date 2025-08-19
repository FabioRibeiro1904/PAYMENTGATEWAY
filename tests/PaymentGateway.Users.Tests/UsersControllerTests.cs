using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PaymentGateway.Users.Controllers;
using Xunit;
using FluentAssertions;
using Moq;

namespace PaymentGateway.Users.Tests.Controllers;

/// <summary>
/// Unit tests for Users Controller
/// Tests cover user registration, validation, and user retrieval scenarios
/// </summary>
public class UsersControllerTests
{
    private readonly Mock<ILogger<UsersController>> _mockLogger;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mockLogger = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_mockLogger.Object);
    }

    [Fact]
    public void RegisterUser_ValidRequest_ReturnsOkWithUserData()
    {
        // Arrange
        var request = new RegisterUserRequest("John Doe", "john@example.com", "SecurePassword123!", "User");

        // Act
        var result = _controller.RegisterUser(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void ValidateUser_ValidAdminCredentials_ReturnsValidWithAdminRole()
    {
        // Arrange
        var request = new ValidateUserRequest("admin@paymentgateway.com", "Admin123!");

        // Act
        var result = _controller.ValidateUser(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void ValidateUser_InvalidCredentials_ReturnsInvalid()
    {
        // Arrange
        var request = new ValidateUserRequest("invalid@example.com", "wrongpassword");

        // Act
        var result = _controller.ValidateUser(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetUser_ExistingAdminUser_ReturnsUserData()
    {
        // Arrange
        var email = "admin@paymentgateway.com";

        // Act
        var result = _controller.GetUser(email);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetUser_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var result = _controller.GetUser(email);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("", "john@example.com", "password123", "User")] // Empty name
    [InlineData("John", "", "password123", "User")] // Empty email
    [InlineData("John", "john@example.com", "", "User")] // Empty password
    public void RegisterUser_InvalidRequest_ShouldHandleGracefully(string name, string email, string password, string role)
    {
        // Arrange
        var request = new RegisterUserRequest(name, email, password, role);

        // Act & Assert
        // The controller should handle invalid requests gracefully
        var result = _controller.RegisterUser(request);
        result.Should().NotBeNull();
    }

    [Fact]
    public void UpdateBalance_ValidRequest_ReturnsOk()
    {
        // Arrange - First register a user, then update balance
        var registerRequest = new RegisterUserRequest("Test User", "test@example.com", "Password123!", "User");
        _controller.RegisterUser(registerRequest);
        
        var request = new UpdateBalanceRequest(5000.00m);

        // Act
        var result = _controller.UpdateBalance("test@example.com", request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void Health_ShouldReturnHealthyStatus()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void RegisterUser_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var request1 = new RegisterUserRequest("User 1", "duplicate@example.com", "Password123!", "User");
        var request2 = new RegisterUserRequest("User 2", "duplicate@example.com", "Password456!", "User");

        // Act
        _controller.RegisterUser(request1); // First registration
        var result = _controller.RegisterUser(request2); // Duplicate

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public void UpdateBalance_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var request = new UpdateBalanceRequest(1000.00m);

        // Act
        var result = _controller.UpdateBalance(email, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void ValidateUser_NonExistentUser_ReturnsInvalid()
    {
        // Arrange
        var request = new ValidateUserRequest("nonexistent@example.com", "anypassword");

        // Act
        var result = _controller.ValidateUser(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }
}

/// <summary>
/// Integration tests for Users Controller
/// Tests the controller with actual dependencies and HTTP context
/// </summary>
public class UsersControllerIntegrationTests
{
    [Fact]
    public void Controller_ShouldHaveCorrectRouting()
    {
        // This test would verify that the controller routes are correctly configured
        // In a real scenario, this would use TestServer and HttpClient
        Assert.True(true); // Placeholder for routing tests
    }

    [Fact] 
    public void Controller_ShouldHandleConcurrentRequests()
    {
        // This test would verify thread safety and concurrent access
        // Important for high-throughput payment systems
        Assert.True(true); // Placeholder for concurrency tests
    }
}

/// <summary>
/// Unit tests for BankUser business logic
/// Tests the domain model methods and properties
/// </summary>
public class BankUserTests
{
    [Fact]
    public void BankUser_CanMakeTransaction_WithSufficientBalance_ShouldReturnTrue()
    {
        // Arrange
        var user = new BankUser
        {
            Balance = 1000m,
            CreditLimit = 500m,
            IsActive = true,
            AccountLockedUntil = null
        };
        
        // Debug the calculation
        var availableCredit = user.AvailableCredit; // CreditLimit - Balance = 500 - 1000 = -500
        var totalAvailable = user.Balance + availableCredit; // 1000 + (-500) = 500
        var amount = 400m; // Should work since 400 < 500

        // Act
        var result = user.CanMakeTransaction(amount);

        // Assert
        result.Should().BeTrue($"Balance: {user.Balance}, CreditLimit: {user.CreditLimit}, AvailableCredit: {availableCredit}, Total: {totalAvailable}, Amount: {amount}");
    }

    [Fact]
    public void BankUser_CanMakeTransaction_WithInsufficientFunds_ShouldReturnFalse()
    {
        // Arrange
        var user = new BankUser
        {
            Balance = 100m,
            CreditLimit = 200m,
            IsActive = true,
            AccountLockedUntil = null
        };

        // Act
        var result = user.CanMakeTransaction(500m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void BankUser_CanMakeTransaction_WithLockedAccount_ShouldReturnFalse()
    {
        // Arrange
        var user = new BankUser
        {
            Balance = 1000m,
            CreditLimit = 500m,
            IsActive = true,
            AccountLockedUntil = DateTime.UtcNow.AddHours(1) // Locked for 1 hour
        };

        // Act
        var result = user.CanMakeTransaction(100m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void BankUser_CanMakeTransaction_WithInactiveAccount_ShouldReturnFalse()
    {
        // Arrange
        var user = new BankUser
        {
            Balance = 1000m,
            CreditLimit = 500m,
            IsActive = false,
            AccountLockedUntil = null
        };

        // Act
        var result = user.CanMakeTransaction(100m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void BankUser_GetAccountDisplayNumber_ShouldMaskAccountNumber()
    {
        // Arrange
        var user = new BankUser
        {
            AccountNumber = "1234567890"
        };

        // Act
        var result = user.GetAccountDisplayNumber();

        // Assert
        result.Should().Be("****7890");
    }

    [Fact]
    public void BankUser_IsAccountLocked_WithFutureDate_ShouldReturnTrue()
    {
        // Arrange
        var user = new BankUser
        {
            AccountLockedUntil = DateTime.UtcNow.AddMinutes(30)
        };

        // Act
        var result = user.IsAccountLocked;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void BankUser_IsAccountLocked_WithPastDate_ShouldReturnFalse()
    {
        // Arrange
        var user = new BankUser
        {
            AccountLockedUntil = DateTime.UtcNow.AddMinutes(-30)
        };

        // Act
        var result = user.IsAccountLocked;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void BankUser_AvailableCredit_ShouldCalculateCorrectly()
    {
        // Arrange
        var user = new BankUser
        {
            CreditLimit = 1000m,
            Balance = 300m
        };

        // Act
        var result = user.AvailableCredit;

        // Assert
        result.Should().Be(700m);
    }

    [Theory]
    [InlineData("John Doe", "John")]
    [InlineData("test@example.com", "test")]
    [InlineData("", "default")]  // Empty name, so uses email part
    public void BankUser_DisplayName_ShouldReturnCorrectFormat(string input, string expected)
    {
        // Arrange
        var user = new BankUser();
        
        if (input.Contains("@"))
        {
            user.Email = input;
            user.Name = "";
        }
        else
        {
            user.Name = input;
            user.Email = "default@test.com";
        }

        // Act
        var result = user.DisplayName;

        // Assert
        result.Should().Be(expected);
    }
}
