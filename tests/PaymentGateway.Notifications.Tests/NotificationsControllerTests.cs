using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PaymentGateway.Notifications.Controllers;
using PaymentGateway.Notifications.Services;
using PaymentGateway.Notifications.Models;
using Xunit;
using FluentAssertions;
using Moq;

namespace PaymentGateway.Notifications.Tests.Controllers;

/// <summary>
/// Unit tests for Notifications Controller
/// Tests email, SMS, and push notification services
/// </summary>
public class NotificationsControllerTests
{
    private readonly Mock<ILogger<NotificationsController>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ICommunicationService> _mockCommunicationService;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _mockLogger = new Mock<ILogger<NotificationsController>>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockCommunicationService = new Mock<ICommunicationService>();
        
        _controller = new NotificationsController(
            _mockLogger.Object,
            _mockNotificationService.Object,
            _mockCommunicationService.Object
        );
    }

    [Fact]
    public async Task SendEmail_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var emailRequest = new EmailRequest
        {
            To = "user@test.com",
            Subject = "Test Subject",
            Body = "Test email body",
            IsHtml = false
        };

        _mockNotificationService.Setup(x => x.SendEmailAsync(It.IsAny<EmailRequest>()))
                               .ReturnsAsync(true);

        // Act
        var result = await _controller.SendEmail(emailRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        _mockNotificationService.Verify(x => x.SendEmailAsync(emailRequest), Times.Once);
    }

    [Fact]
    public async Task SendEmail_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var emailRequest = new EmailRequest
        {
            To = "invalid-email",
            Subject = "Test Subject",
            Body = "Test email body"
        };

        // Act
        var result = await _controller.SendEmail(emailRequest);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SendSMS_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var smsRequest = new SMSRequest
        {
            PhoneNumber = "+5511999887766",
            Message = "Your transaction was successful!",
            Priority = "High"
        };

        _mockCommunicationService.Setup(x => x.SendSMSAsync(It.IsAny<SMSRequest>()))
                                 .ReturnsAsync(true);

        // Act
        var result = await _controller.SendSMS(smsRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        _mockCommunicationService.Verify(x => x.SendSMSAsync(smsRequest), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("invalid-phone")]
    public async Task SendSMS_InvalidPhoneNumber_ReturnsBadRequest(string phoneNumber)
    {
        // Arrange
        var smsRequest = new SMSRequest
        {
            PhoneNumber = phoneNumber,
            Message = "Test message"
        };

        // Act
        var result = await _controller.SendSMS(smsRequest);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SendPushNotification_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var pushRequest = new PushNotificationRequest
        {
            UserId = "user123",
            Title = "Payment Processed",
            Body = "Your payment of R$ 150.00 was successful",
            Data = new Dictionary<string, string>
            {
                { "transactionId", "txn_123456" },
                { "amount", "150.00" }
            }
        };

        _mockNotificationService.Setup(x => x.SendPushNotificationAsync(It.IsAny<PushNotificationRequest>()))
                               .ReturnsAsync(true);

        // Act
        var result = await _controller.SendPushNotification(pushRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetNotificationHistory_ValidUser_ReturnsHistory()
    {
        // Arrange
        var userId = "user123";
        var expectedHistory = new List<NotificationHistory>
        {
            new() { Id = "1", UserId = userId, Type = "Email", Status = "Sent", CreatedAt = DateTime.UtcNow },
            new() { Id = "2", UserId = userId, Type = "SMS", Status = "Delivered", CreatedAt = DateTime.UtcNow }
        };

        _mockNotificationService.Setup(x => x.GetNotificationHistoryAsync(userId))
                               .ReturnsAsync(expectedHistory);

        // Act
        var result = await _controller.GetNotificationHistory(userId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
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
/// Tests for Notification Service business logic
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly Mock<IEmailProvider> _mockEmailProvider;
    private readonly Mock<IPushNotificationProvider> _mockPushProvider;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _mockEmailProvider = new Mock<IEmailProvider>();
        _mockPushProvider = new Mock<IPushNotificationProvider>();
        
        _service = new NotificationService(
            _mockLogger.Object,
            _mockEmailProvider.Object,
            _mockPushProvider.Object
        );
    }

    [Fact]
    public async Task SendEmailAsync_ValidEmail_ReturnsTrue()
    {
        // Arrange
        var emailRequest = new EmailRequest
        {
            To = "user@test.com",
            Subject = "Welcome!",
            Body = "Welcome to our platform"
        };

        _mockEmailProvider.Setup(x => x.SendAsync(It.IsAny<EmailRequest>()))
                          .ReturnsAsync(true);

        // Act
        var result = await _service.SendEmailAsync(emailRequest);

        // Assert
        result.Should().BeTrue();
        _mockEmailProvider.Verify(x => x.SendAsync(emailRequest), Times.Once);
    }

    [Fact]
    public async Task SendPushNotificationAsync_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var pushRequest = new PushNotificationRequest
        {
            UserId = "user123",
            Title = "New Message",
            Body = "You have a new notification"
        };

        _mockPushProvider.Setup(x => x.SendAsync(It.IsAny<PushNotificationRequest>()))
                        .ReturnsAsync(true);

        // Act
        var result = await _service.SendPushNotificationAsync(pushRequest);

        // Assert
        result.Should().BeTrue();
        _mockPushProvider.Verify(x => x.SendAsync(pushRequest), Times.Once);
    }
}

/// <summary>
/// Tests for Communication Service (SMS, WhatsApp, etc.)
/// </summary>
public class CommunicationServiceTests
{
    private readonly Mock<ILogger<CommunicationService>> _mockLogger;
    private readonly Mock<ISMSProvider> _mockSMSProvider;
    private readonly Mock<IWhatsAppProvider> _mockWhatsAppProvider;
    private readonly CommunicationService _service;

    public CommunicationServiceTests()
    {
        _mockLogger = new Mock<ILogger<CommunicationService>>();
        _mockSMSProvider = new Mock<ISMSProvider>();
        _mockWhatsAppProvider = new Mock<IWhatsAppProvider>();
        
        _service = new CommunicationService(
            _mockLogger.Object,
            _mockSMSProvider.Object,
            _mockWhatsAppProvider.Object
        );
    }

    [Fact]
    public async Task SendSMSAsync_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var smsRequest = new SMSRequest
        {
            PhoneNumber = "+5511999887766",
            Message = "Your verification code is: 123456"
        };

        _mockSMSProvider.Setup(x => x.SendAsync(It.IsAny<SMSRequest>()))
                       .ReturnsAsync(true);

        // Act
        var result = await _service.SendSMSAsync(smsRequest);

        // Assert
        result.Should().BeTrue();
        _mockSMSProvider.Verify(x => x.SendAsync(smsRequest), Times.Once);
    }

    [Theory]
    [InlineData("+5511999887766")]
    [InlineData("+1234567890")]
    [InlineData("+447911123456")]
    public async Task SendSMSAsync_ValidPhoneFormats_ReturnsTrue(string phoneNumber)
    {
        // Arrange
        var smsRequest = new SMSRequest
        {
            PhoneNumber = phoneNumber,
            Message = "Test message"
        };

        _mockSMSProvider.Setup(x => x.SendAsync(It.IsAny<SMSRequest>()))
                       .ReturnsAsync(true);

        // Act
        var result = await _service.SendSMSAsync(smsRequest);

        // Assert
        result.Should().BeTrue();
    }
}

/// <summary>
/// Tests for Notification models and DTOs
/// </summary>
public class NotificationModelsTests
{
    [Fact]
    public void EmailRequest_Creation_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        var emailRequest = new EmailRequest
        {
            To = "test@example.com",
            Cc = "cc@example.com",
            Bcc = "bcc@example.com",
            Subject = "Test Subject",
            Body = "Test Body",
            IsHtml = true
        };

        // Assert
        emailRequest.To.Should().Be("test@example.com");
        emailRequest.Cc.Should().Be("cc@example.com");
        emailRequest.Bcc.Should().Be("bcc@example.com");
        emailRequest.Subject.Should().Be("Test Subject");
        emailRequest.Body.Should().Be("Test Body");
        emailRequest.IsHtml.Should().BeTrue();
    }

    [Fact]
    public void SMSRequest_Creation_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        var smsRequest = new SMSRequest
        {
            PhoneNumber = "+5511999887766",
            Message = "Test SMS message",
            Priority = "High"
        };

        // Assert
        smsRequest.PhoneNumber.Should().Be("+5511999887766");
        smsRequest.Message.Should().Be("Test SMS message");
        smsRequest.Priority.Should().Be("High");
    }

    [Fact]
    public void NotificationHistory_Creation_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        var history = new NotificationHistory
        {
            Id = "hist123",
            UserId = "user456",
            Type = "Email",
            Status = "Delivered",
            CreatedAt = DateTime.UtcNow,
            DeliveredAt = DateTime.UtcNow.AddMinutes(2)
        };

        // Assert
        history.Id.Should().Be("hist123");
        history.UserId.Should().Be("user456");
        history.Type.Should().Be("Email");
        history.Status.Should().Be("Delivered");
        history.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        history.DeliveredAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(2), TimeSpan.FromSeconds(1));
    }
}
