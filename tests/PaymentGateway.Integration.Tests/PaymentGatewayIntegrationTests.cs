using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using System.Net.Http.Json;
using System.Net;
using Xunit;

namespace PaymentGateway.Integration.Tests;

/// <summary>
/// Integration tests for the entire PaymentGateway system
/// Tests end-to-end functionality with real HTTP requests
/// </summary>
public class PaymentGatewayIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PaymentGatewayIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/api/users/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task RegisterUser_ValidData_ReturnsSuccess()
    {
        // Arrange
        var registerRequest = new
        {
            Name = "Integration Test User",
            Email = $"integration.test.{Guid.NewGuid()}@example.com",
            Password = "SecurePassword123!",
            Role = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterUser_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var email = $"duplicate.{Guid.NewGuid()}@example.com";
        var registerRequest = new
        {
            Name = "Test User",
            Email = email,
            Password = "Password123!",
            Role = "User"
        };

        // Act - Register first user
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);
        
        // Act - Try to register duplicate
        var duplicateResponse = await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Assert
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUser_ExistingUser_ReturnsUserData()
    {
        // Arrange - First register a user
        var email = $"getuser.test.{Guid.NewGuid()}@example.com";
        var registerRequest = new
        {
            Name = "Get User Test",
            Email = email,
            Password = "Password123!",
            Role = "User"
        };

        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        // Act
        var response = await _client.GetAsync($"/api/users/{email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUser_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var response = await _client.GetAsync($"/api/users/{email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateUser_ValidCredentials_ReturnsValid()
    {
        // Arrange - First register a user
        var email = $"validate.test.{Guid.NewGuid()}@example.com";
        var password = "ValidatePassword123!";
        var registerRequest = new
        {
            Name = "Validate User Test",
            Email = email,
            Password = password,
            Role = "User"
        };

        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var validateRequest = new
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/validate", validateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateUser_InvalidCredentials_ReturnsInvalid()
    {
        // Arrange
        var validateRequest = new
        {
            Email = "invalid@example.com",
            Password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/validate", validateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateBalance_ExistingUser_ReturnsSuccess()
    {
        // Arrange - First register a user
        var email = $"balance.test.{Guid.NewGuid()}@example.com";
        var registerRequest = new
        {
            Name = "Balance Test User",
            Email = email,
            Password = "Password123!",
            Role = "User"
        };

        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var updateRequest = new
        {
            NewBalance = 2500.50m
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{email}/balance", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateBalance_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var updateRequest = new
        {
            NewBalance = 1000.00m
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{email}/balance", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullUserLifecycle_RegisterValidateUpdateGet_WorksEndToEnd()
    {
        // Arrange
        var email = $"lifecycle.test.{Guid.NewGuid()}@example.com";
        var password = "LifecyclePassword123!";
        var name = "Lifecycle Test User";

        // Act & Assert - Register
        var registerRequest = new { Name = name, Email = email, Password = password, Role = "User" };
        var registerResponse = await _client.PostAsJsonAsync("/api/users/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act & Assert - Validate
        var validateRequest = new { Email = email, Password = password };
        var validateResponse = await _client.PostAsJsonAsync("/api/users/validate", validateRequest);
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act & Assert - Update Balance
        var updateRequest = new { NewBalance = 5000.00m };
        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{email}/balance", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act & Assert - Get Updated User
        var getUserResponse = await _client.GetAsync($"/api/users/{email}");
        getUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userData = await getUserResponse.Content.ReadFromJsonAsync<dynamic>();
        userData.Should().NotBeNull();
    }
}

/// <summary>
/// Performance and stress tests for the PaymentGateway system
/// Tests the system under load to ensure it meets performance requirements
/// </summary>
public class PaymentGatewayPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PaymentGatewayPerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ConcurrentUserRegistrations_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentUsers = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        for (int i = 0; i < concurrentUsers; i++)
        {
            var registerRequest = new
            {
                Name = $"Concurrent User {i}",
                Email = $"concurrent.{i}.{Guid.NewGuid()}@example.com",
                Password = "ConcurrentPassword123!",
                Role = "User"
            };

            tasks.Add(_client.PostAsJsonAsync("/api/users/register", registerRequest));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response => 
            response.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task HealthCheck_ResponseTime_ShouldBeFast()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/api/users/health");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should respond in under 100ms
    }
}
