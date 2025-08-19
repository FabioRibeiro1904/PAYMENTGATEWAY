using System.Collections.Concurrent;
using System.Net;

namespace PaymentGateway.ApiGateway.Middleware;

public class AdvancedRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdvancedRateLimitingMiddleware> _logger;
    private readonly IConfiguration _configuration;
    
    
    private static readonly ConcurrentDictionary<string, List<DateTime>> _requestTracker = new();
    private static readonly ConcurrentDictionary<string, UserRateLimit> _userLimits = new();

    public AdvancedRateLimitingMiddleware(RequestDelegate next, ILogger<AdvancedRateLimitingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var endpoint = GetNormalizedEndpoint(context);
        var key = $"{clientId}:{endpoint}";

        
        var rateLimitConfig = GetRateLimitConfig(context);
        
        if (IsRateLimited(key, rateLimitConfig))
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}", clientId, endpoint);
            
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Add("X-RateLimit-Limit", rateLimitConfig.RequestsPerMinute.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
            context.Response.Headers.Add("X-RateLimit-Reset", GetResetTime().ToString());
            context.Response.Headers.Add("Retry-After", "60");

            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        
        RecordRequest(key);
        
        
        var remaining = GetRemainingRequests(key, rateLimitConfig);
        context.Response.Headers.Add("X-RateLimit-Limit", rateLimitConfig.RequestsPerMinute.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", remaining.ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", GetResetTime().ToString());

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        
        var userClaim = context.User?.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (!string.IsNullOrEmpty(userClaim))
        {
            return $"user:{userClaim}";
        }

        
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var clientIp = forwardedFor?.Split(',').FirstOrDefault()?.Trim() ?? 
                       context.Connection.RemoteIpAddress?.ToString() ?? 
                       "unknown";
        
        return $"ip:{clientIp}";
    }

    private string GetNormalizedEndpoint(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
        var method = context.Request.Method.ToUpperInvariant();
        
        
        path = NormalizePathParameters(path);
        
        return $"{method}:{path}";
    }

    private string NormalizePathParameters(string path)
    {
        
        return System.Text.RegularExpressions.Regex.Replace(path, @"/\d+", "/{id}");
    }

    private RateLimitConfig GetRateLimitConfig(HttpContext context)
    {
        var endpoint = GetNormalizedEndpoint(context);
        var userRole = context.User?.Claims?.FirstOrDefault(c => c.Type == "role")?.Value ?? "anonymous";

        
        return endpoint switch
        {
            var e when e.StartsWith("POST:/api/payments") => new RateLimitConfig
            {
                RequestsPerMinute = userRole == "Admin" ? 1000 : 100,
                BurstLimit = userRole == "Admin" ? 50 : 10
            },
            var e when e.StartsWith("GET:/api/payments") => new RateLimitConfig
            {
                RequestsPerMinute = userRole == "Admin" ? 2000 : 500,
                BurstLimit = userRole == "Admin" ? 100 : 25
            },
            var e when e.StartsWith("POST:/api/auth") => new RateLimitConfig
            {
                RequestsPerMinute = 10, 
                BurstLimit = 3
            },
            var e when e.StartsWith("GET:/api/gateway") => new RateLimitConfig
            {
                RequestsPerMinute = userRole == "Admin" ? 1000 : 100,
                BurstLimit = userRole == "Admin" ? 50 : 10
            },
            _ => new RateLimitConfig
            {
                RequestsPerMinute = userRole == "Admin" ? 500 : 200,
                BurstLimit = userRole == "Admin" ? 25 : 10
            }
        };
    }

    private bool IsRateLimited(string key, RateLimitConfig config)
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        _requestTracker.AddOrUpdate(key, 
            new List<DateTime> { now },
            (k, existing) =>
            {
                
                var recent = existing.Where(t => t > oneMinuteAgo).ToList();
                recent.Add(now);
                return recent;
            });

        var recentRequests = _requestTracker[key];
        
        
        var requestsInLastMinute = recentRequests.Count(t => t > oneMinuteAgo);
        if (requestsInLastMinute > config.RequestsPerMinute)
        {
            return true;
        }

        
        var tenSecondsAgo = now.AddSeconds(-10);
        var burstRequests = recentRequests.Count(t => t > tenSecondsAgo);
        if (burstRequests > config.BurstLimit)
        {
            return true;
        }

        return false;
    }

    private void RecordRequest(string key)
    {
        var now = DateTime.UtcNow;
        _requestTracker.AddOrUpdate(key,
            new List<DateTime> { now },
            (k, existing) => 
            {
                existing.Add(now);
                return existing;
            });
    }

    private int GetRemainingRequests(string key, RateLimitConfig config)
    {
        if (!_requestTracker.TryGetValue(key, out var requests))
        {
            return config.RequestsPerMinute;
        }

        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recentRequests = requests.Count(t => t > oneMinuteAgo);
        
        return Math.Max(0, config.RequestsPerMinute - recentRequests);
    }

    private long GetResetTime()
    {
        var nextMinute = DateTime.UtcNow.AddMinutes(1);
        var startOfNextMinute = new DateTime(nextMinute.Year, nextMinute.Month, nextMinute.Day, 
                                           nextMinute.Hour, nextMinute.Minute, 0, DateTimeKind.Utc);
        
        return ((DateTimeOffset)startOfNextMinute).ToUnixTimeSeconds();
    }
}

public record RateLimitConfig
{
    public int RequestsPerMinute { get; init; } = 100;
    public int BurstLimit { get; init; } = 10;
}

public record UserRateLimit
{
    public DateTime LastRequest { get; init; }
    public int RequestCount { get; init; }
}
public static class RateLimitingExtensions
{
    public static IApplicationBuilder UseAdvancedRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AdvancedRateLimitingMiddleware>();
    }
}
