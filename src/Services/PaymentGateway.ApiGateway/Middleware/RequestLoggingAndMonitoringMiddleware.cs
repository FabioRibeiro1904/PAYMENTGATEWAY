using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.ApiGateway.Middleware;

public class RequestLoggingAndMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingAndMonitoringMiddleware> _logger;
    private static readonly ActivitySource ActivitySource = new("PaymentGateway.ApiGateway");

    public RequestLoggingAndMonitoringMiddleware(RequestDelegate next, ILogger<RequestLoggingAndMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetOrGenerateCorrelationId(context);
        var requestId = Guid.NewGuid().ToString();

        
        context.Response.Headers.Add("X-Correlation-Id", correlationId);
        context.Response.Headers.Add("X-Request-Id", requestId);

        using var activity = ActivitySource.StartActivity("HTTP Request");
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.url", GetSanitizedUrl(context.Request));
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("request.id", requestId);

        
        var requestBody = await CaptureRequestBody(context);

        
        LogRequestStart(context, correlationId, requestId, requestBody);

        
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        Exception? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            
            var responseBody = await CaptureResponseBody(responseBodyStream, originalBodyStream);
            
            
            LogRequestEnd(context, correlationId, requestId, stopwatch.ElapsedMilliseconds, responseBody, exception);
            
            
            RecordMetrics(context, stopwatch.ElapsedMilliseconds, exception);
            
            activity?.SetTag("http.status_code", context.Response.StatusCode);
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var existingId) && 
            !string.IsNullOrEmpty(existingId))
        {
            return existingId.ToString();
        }

        
        return Guid.NewGuid().ToString();
    }

    private string GetSanitizedUrl(HttpRequest request)
    {
        var url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        
        
        var sensitiveParams = new[] { "password", "token", "secret", "key", "authorization" };
        
        foreach (var param in sensitiveParams)
        {
            url = System.Text.RegularExpressions.Regex.Replace(url, 
                $@"({param}=)[^&]*", 
                $"$1***REDACTED***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return url;
    }

    private async Task<string> CaptureRequestBody(HttpContext context)
    {
        
        if (!ShouldLogRequestBody(context))
        {
            return "[Not Logged]";
        }

        context.Request.EnableBuffering();
        var buffer = new byte[Convert.ToInt32(context.Request.ContentLength ?? 0)];
        
        if (buffer.Length > 0)
        {
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            context.Request.Body.Position = 0; 
            
            var body = Encoding.UTF8.GetString(buffer);
            return SanitizeRequestBody(body);
        }

        return string.Empty;
    }

    private async Task<string> CaptureResponseBody(MemoryStream responseBodyStream, Stream originalBodyStream)
    {
        responseBodyStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
        responseBodyStream.Seek(0, SeekOrigin.Begin);
        await responseBodyStream.CopyToAsync(originalBodyStream);
        
        return SanitizeResponseBody(responseBody);
    }

    private bool ShouldLogRequestBody(HttpContext context)
    {
        
        if (context.Request.Method is "GET" or "HEAD" or "OPTIONS")
        {
            return false;
        }

        
        var contentType = context.Request.ContentType?.ToLowerInvariant();
        if (contentType == null || !contentType.Contains("application/json"))
        {
            return false;
        }

        
        if (context.Request.ContentLength > 10240)
        {
            return false;
        }

        return true;
    }

    private string SanitizeRequestBody(string body)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        try
        {
            
            using var doc = JsonDocument.Parse(body);
            var sanitized = SanitizeJsonElement(doc.RootElement);
            return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            
            return SanitizeTextContent(body);
        }
    }

    private string SanitizeResponseBody(string body)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        
        if (body.Length > 1000)
        {
            return body.Substring(0, 1000) + "... [TRUNCATED]";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var sanitized = SanitizeJsonElement(doc.RootElement);
            return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return SanitizeTextContent(body);
        }
    }

    private object SanitizeJsonElement(JsonElement element)
    {
        var sensitiveFields = new[] { "password", "token", "secret", "key", "authorization", "creditCard", "cvv", "cardNumber" };

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    prop => prop.Name,
                    prop => sensitiveFields.Any(field => prop.Name.Contains(field, StringComparison.OrdinalIgnoreCase))
                        ? "***REDACTED***"
                        : SanitizeJsonElement(prop.Value)
                ),
            JsonValueKind.Array => element.EnumerateArray().Select(SanitizeJsonElement).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private string SanitizeTextContent(string content)
    {
        var sensitivePatterns = new[]
        {
            @"password[""':\s]*[""']([^""']+)[""']",
            @"token[""':\s]*[""']([^""']+)[""']",
            @"authorization[""':\s]*[""']([^""']+)[""']"
        };

        foreach (var pattern in sensitivePatterns)
        {
            content = System.Text.RegularExpressions.Regex.Replace(content, pattern, 
                match => match.Value.Replace(match.Groups[1].Value, "***REDACTED***"),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return content;
    }

    private void LogRequestStart(HttpContext context, string correlationId, string requestId, string requestBody)
    {
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
        var clientIp = GetClientIpAddress(context);
        var userId = context.User?.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Anonymous";

        _logger.LogInformation(
            " REQUEST START - {Method} {Path} | CorrelationId: {CorrelationId} | RequestId: {RequestId} | UserId: {UserId} | ClientIP: {ClientIP} | UserAgent: {UserAgent} | Body: {RequestBody}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            requestId,
            userId,
            clientIp,
            userAgent,
            requestBody
        );
    }

    private void LogRequestEnd(HttpContext context, string correlationId, string requestId, long durationMs, string responseBody, Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception,
                "REQUEST ERROR - {Method} {Path} | CorrelationId: {CorrelationId} | RequestId: {RequestId} | Duration: {Duration}ms | StatusCode: {StatusCode} | Error: {ErrorMessage}",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                requestId,
                durationMs,
                context.Response.StatusCode,
                exception.Message
            );
        }
        else
        {
            var logLevel = context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
            var statusLabel = context.Response.StatusCode switch
            {
                >= 200 and < 300 => "SUCCESS",
                >= 300 and < 400 => "REDIRECT",
                >= 400 and < 500 => "CLIENT_ERROR",
                >= 500 => "SERVER_ERROR",
                _ => "UNKNOWN"
            };

            _logger.Log(logLevel,
                "{StatusLabel} REQUEST END - {Method} {Path} | CorrelationId: {CorrelationId} | RequestId: {RequestId} | Duration: {Duration}ms | StatusCode: {StatusCode} | Response: {ResponseBody}",
                statusLabel,
                context.Request.Method,
                context.Request.Path,
                correlationId,
                requestId,
                durationMs,
                context.Response.StatusCode,
                responseBody
            );
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private void RecordMetrics(HttpContext context, long durationMs, Exception? exception)
    {
        
        var tags = new Dictionary<string, object?>
        {
            ["method"] = context.Request.Method,
            ["endpoint"] = context.Request.Path.Value,
            ["status_code"] = context.Response.StatusCode,
            ["has_error"] = exception != null
        };

        
        _logger.LogDebug(" METRICS - Duration: {Duration}ms | Tags: {@Tags}", durationMs, tags);
    }
}
public static class RequestLoggingExtensions
{
    public static IApplicationBuilder UseRequestLoggingAndMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingAndMonitoringMiddleware>();
    }
}
