using System.Text.Json;
using CMS.API.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CMS.API.Tests.Infrastructure;

public class GlobalExceptionMiddlewareTests
{
    private readonly CapturingLoggerProvider _logs = new();

    private GlobalExceptionMiddleware Middleware(RequestDelegate next) =>
        new(next, _logs.CreateLogger<GlobalExceptionMiddleware>());

    /// <summary>A pipeline tail that throws, so the exception carries a real stack trace like a controller's would.</summary>
    private static RequestDelegate Throwing(Exception exception) => _ => throw exception;

    private static DefaultHttpContext Context(string method = "GET", string path = "/api/courses/7")
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task PassesASuccessfulRequestThrough_Untouched()
    {
        var context = Context();
        var middleware = Middleware(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            await Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Empty(_logs.Entries);
    }

    [Fact]
    public async Task Answers500_WithTheGenericMessageAndTheTraceId_AsJson()
    {
        var context = Context();

        await Middleware(Throwing(new InvalidOperationException("boom"))).InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        using var json = JsonDocument.Parse(await Body(context));
        Assert.Equal(["message", "traceId"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(GlobalExceptionMiddleware.Message, json.RootElement.GetProperty("message").GetString());
        Assert.Equal("trace-123", json.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task TheBody_NeverContainsTheExceptionMessageTypeOrStackTrace()
    {
        var context = Context();
        var exception = new InvalidOperationException(
            "SELECT PasswordHash FROM AppUser -- Server=.\\SQLEXPRESS;Database=CMS;Trusted_Connection=True");

        await Middleware(Throwing(exception)).InvokeAsync(context);

        var body = await Body(context);
        Assert.DoesNotContain("SELECT", body);
        Assert.DoesNotContain("PasswordHash", body);
        Assert.DoesNotContain("SQLEXPRESS", body);
        Assert.DoesNotContain("Trusted_Connection", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain(nameof(GlobalExceptionMiddlewareTests), body);
    }

    [Fact]
    public async Task LogsTheFullException_WithMethodPathAndTraceId_AtErrorLevel()
    {
        var context = Context("DELETE", "/api/partners/3");
        var exception = new InvalidOperationException("SELECT 1 -- Server=.\\SQLEXPRESS");

        await Middleware(Throwing(exception)).InvokeAsync(context);

        var entry = Assert.Single(_logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("DELETE", entry.Message);
        Assert.Contains("/api/partners/3", entry.Message);
        Assert.Contains("trace-123", entry.Message);
        // What an ILogger sink renders for the exception: type, message and the stack trace of the throw site.
        var rendered = entry.Exception!.ToString();
        Assert.Contains("SELECT 1 -- Server=.\\SQLEXPRESS", rendered);
        Assert.Contains(nameof(InvalidOperationException), rendered);
        Assert.Contains(nameof(GlobalExceptionMiddlewareTests), rendered);
        Assert.NotNull(entry.Exception.StackTrace);
    }

    [Fact]
    public async Task ReplacesWhateverTheFailedActionHadStagedInTheResponse()
    {
        var context = Context();
        var middleware = Middleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.Headers.Location = "/api/courses/8";
            throw new InvalidOperationException("after setting headers, before the body");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task AClientCancellation_IsLoggedAsInformation_AndGetsNoBody()
    {
        var context = Context();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        await Middleware(Throwing(new OperationCanceledException())).InvokeAsync(context);

        var entry = Assert.Single(_logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal(string.Empty, await Body(context));
    }

    [Fact]
    public async Task AnOperationCanceledException_WhileTheClientIsStillThere_IsAnUnexpectedError()
    {
        var context = Context();

        await Middleware(Throwing(new OperationCanceledException("a timeout inside the API"))).InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(LogLevel.Error, Assert.Single(_logs.Entries).Level);
    }
}
