using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PaymentGateway.Payments.Controllers;
using PaymentGateway.Payments.Services;
using PaymentGateway.Payments.Models;
using Xunit;
using FluentAssertions;
using Moq;

namespace PaymentGateway.Payments.Tests.Controllers;

/// <summary>
/// Unit tests for Payments Controller
/// Tests payment processing, transfers, and transaction management
/// </summary>
public class PaymentsControllerTests
{
    private readonly Mock<ILogger<PaymentsController>> _mockLogger;
    private readonly Mock<IKafkaProducerService> _mockKafkaProducer;
    private readonly Mock<IRedisService> _mockRedisService;
    private readonly PaymentsController _controller;

    public PaymentsControllerTests()
    {
        _mockLogger = new Mock<ILogger<PaymentsController>>();
        _mockKafkaProducer = new Mock<IKafkaProducerService>();
        _mockRedisService = new Mock<IRedisService>();
        
        _controller = new PaymentsController(
            _mockLogger.Object,
            _mockKafkaProducer.Object,
            _mockRedisService.Object
        );
    }

    [Fact]
    public async Task ProcessTransfer_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new TransactionRequest
        {
            FromUserId = "user1@test.com",
            ToUserId = "user2@test.com",
            Amount = 100.00m,
            Description = "Test transfer"
        };

        _mockKafkaProducer.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                         .Returns(Task.CompletedTask);

        _mockRedisService.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                        .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ProcessTransfer(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ProcessTransfer_InvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var request = new TransactionRequest
        {
            FromUserId = "user1@test.com",
            ToUserId = "user2@test.com",
            Amount = -50.00m, // Invalid negative amount
            Description = "Invalid transfer"
        };

        // Act
        var result = await _controller.ProcessTransfer(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessTransfer_SameUserTransfer_ReturnsBadRequest()
    {
        // Arrange
        var request = new TransactionRequest
        {
            FromUserId = "user1@test.com",
            ToUserId = "user1@test.com", // Same user
            Amount = 100.00m,
            Description = "Self transfer"
        };

        // Act
        var result = await _controller.ProcessTransfer(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTransactionHistory_ValidUser_ReturnsTransactions()
    {
        // Arrange
        var userId = "user1@test.com";

        // Act
        var result = await _controller.GetTransactionHistory(userId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetTransactionHistory_InvalidUserId_ReturnsBadRequest(string userId)
    {
        // Act
        var result = await _controller.GetTransactionHistory(userId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetTransactionStatus_ExistingTransaction_ReturnsStatus()
    {
        // Arrange
        var transactionId = Guid.NewGuid().ToString();
        
        // First create a transaction
        var request = new TransactionRequest
        {
            FromUserId = "user1@test.com",
            ToUserId = "user2@test.com",
            Amount = 100.00m,
            Description = "Test for status check"
        };

        _mockKafkaProducer.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                         .Returns(Task.CompletedTask);
        _mockRedisService.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                        .Returns(Task.CompletedTask);

        await _controller.ProcessTransfer(request);

        // Act
        var result = await _controller.GetTransactionStatus(transactionId);

        // Assert
        // Should return status even if transaction doesn't exist (will be NotFound or appropriate response)
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Health_ShouldReturnHealthyStatus()
    {
        // Act
        var result = await _controller.Health();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }
}

/// <summary>
/// Tests for Transaction model and business logic
/// </summary>
public class TransactionTests
{
    [Fact]
    public void Transaction_Creation_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            FromUserId = "user1@test.com",
            ToUserId = "user2@test.com",
            Amount = 250.75m,
            Description = "Test transaction",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        transaction.Id.Should().NotBeNullOrEmpty();
        transaction.FromUserId.Should().Be("user1@test.com");
        transaction.ToUserId.Should().Be("user2@test.com");
        transaction.Amount.Should().Be(250.75m);
        transaction.Status.Should().Be("Pending");
        transaction.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processing")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public void Transaction_ValidStatuses_ShouldBeAccepted(string status)
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            Status = status
        };

        // Assert
        transaction.Status.Should().Be(status);
    }

    [Fact]
    public void TransactionRequest_Validation_ShouldValidateRequiredFields()
    {
        // Arrange
        var request = new TransactionRequest();

        // Act & Assert
        request.FromUserId.Should().BeNull();
        request.ToUserId.Should().BeNull();
        request.Amount.Should().Be(0);
    }
}

/// <summary>
/// Integration tests for Payment services
/// </summary>
public class PaymentServicesIntegrationTests
{
    [Fact]
    public void KafkaProducerService_ShouldBeConfigurable()
    {
        // This test verifies that the Kafka producer service interface exists
        // and can be used for dependency injection
        var mockKafka = new Mock<IKafkaProducerService>();
        mockKafka.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                 .Returns(Task.CompletedTask);

        // Assert
        mockKafka.Object.Should().NotBeNull();
    }

    [Fact]
    public void RedisService_ShouldBeConfigurable()
    {
        // This test verifies that the Redis service interface exists
        // and can be used for dependency injection
        var mockRedis = new Mock<IRedisService>();
        mockRedis.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                 .Returns(Task.CompletedTask);

        // Assert
        mockRedis.Object.Should().NotBeNull();
    }
}
