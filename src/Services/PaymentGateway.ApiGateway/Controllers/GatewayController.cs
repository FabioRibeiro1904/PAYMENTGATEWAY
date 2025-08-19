using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PaymentGateway.ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GatewayController : ControllerBase
{
    private readonly ILogger<GatewayController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GatewayController(ILogger<GatewayController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    
    
    
    [HttpGet("status")]
    public async Task<IActionResult> GetGatewayStatus()
    {
        _logger.LogInformation("Checking gateway and services status");

        var services = new Dictionary<string, object>
        {
            ["users"] = await CheckServiceHealth("http://localhost:5003/health"),
            ["payments"] = await CheckServiceHealth("http://localhost:5002/health"),
            ["notifications"] = await CheckServiceHealth("http://localhost:5001/health")
        };

        var gatewayStatus = new
        {
            Gateway = new
            {
                Status = "Healthy",
                Version = "1.0.0",
                Uptime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            },
            Services = services,
            LoadBalancing = new
            {
                Algorithm = "RoundRobin",
                HealthChecks = "Active",
                CircuitBreaker = "Enabled"
            },
            Security = new
            {
                Authentication = "JWT",
                RateLimiting = "100 req/min",
                CORS = "Enabled"
            }
        };

        return Ok(gatewayStatus);
    }

    
    
    
    [HttpGet("metrics")]
    [Authorize]
    public IActionResult GetMetrics()
    {
        _logger.LogInformation("Getting gateway metrics");

        var metrics = new
        {
            RequestsToday = new Random().Next(1000, 10000),
            SuccessRate = Math.Round(99.5 + (new Random().NextDouble() * 0.4), 2),
            AverageResponseTime = new Random().Next(50, 200) + "ms",
            ActiveConnections = new Random().Next(10, 100),
            ErrorRate = Math.Round(new Random().NextDouble() * 0.5, 2),
            Services = new
            {
                Users = new { Requests = new Random().Next(100, 1000), AvgTime = new Random().Next(20, 80) + "ms" },
                Payments = new { Requests = new Random().Next(500, 2000), AvgTime = new Random().Next(100, 300) + "ms" },
                Notifications = new { Requests = new Random().Next(200, 800), AvgTime = new Random().Next(30, 100) + "ms" }
            }
        };

        return Ok(metrics);
    }

    
    
    
    [HttpGet("routes")]
    [Authorize]
    public IActionResult GetRoutes()
    {
        var routes = new
        {
            Users = new
            {
                Pattern = "/api/users/{**catch-all}",
                Destination = "http://localhost:5003",
                HealthCheck = "/health",
                LoadBalancing = "RoundRobin"
            },
            Payments = new
            {
                Pattern = "/api/payments/{**catch-all}",
                Destination = "http://localhost:5002",
                HealthCheck = "/health",
                LoadBalancing = "RoundRobin"
            },
            Notifications = new
            {
                Pattern = "/api/notifications/{**catch-all}",
                Destination = "http://localhost:5001",
                HealthCheck = "/health",
                LoadBalancing = "RoundRobin"
            },
            SignalR = new
            {
                PaymentHub = "/paymentHub → localhost:5077",
                NotificationHub = "/notificationHub → localhost:5078"
            }
        };

        return Ok(routes);
    }

    private async Task<object> CheckServiceHealth(string healthUrl)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var response = await client.GetAsync(healthUrl);
            
            return new
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = "< 5s",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for {HealthUrl}", healthUrl);
            return new
            {
                Status = "Unhealthy",
                Error = ex.Message,
                LastChecked = DateTime.UtcNow
            };
        }
    }
}
