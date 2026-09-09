using Microsoft.Net.Http.Headers;

namespace CMS.API.Infrastructure;

/// <summary>
/// The single JSON body every unexpected failure answers with: a safe, generic message plus the request's trace id
/// so a user can quote it and the operator can find the logged exception. Never carries exception text.
/// </summary>
public sealed class UnexpectedErrorResponse
{
    public string Message { get; init; } = GlobalExceptionMiddleware.Message;
    public string TraceId { get; init; } = string.Empty;
}

/// <summary>
/// First middleware in the pipeline and the last line of defence: any exception no controller mapped (a Dapper /
/// SqlClient failure, a bug, a null) is logged in full — type, message and stack trace, on the server only — and
/// answered with <b>500</b> and <see cref="UnexpectedErrorResponse"/>. Responses a controller or filter already
/// produced (401 / 403 / 400 <c>ValidationProblem</c> / 409 / the explicit 500 for <see cref="AppConfigException"/>)
/// are not exceptions, so they pass through unchanged.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /// <summary>The only text the client ever sees for an unexpected error.</summary>
    public const string Message = "系統發生未預期的錯誤，請稍後再試。";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var request = context.Request;
        var traceId = context.TraceIdentifier;

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            // The client went away mid-request; there is nobody to answer and nothing went wrong server-side.
            logger.LogInformation("Request {Method} {Path} was cancelled by the client (trace {TraceId}).",
                request.Method, request.Path, traceId);
            return;
        }

        // ILogger renders the exception with its type, message and full stack trace; none of that leaves the server.
        logger.LogError(exception, "Unhandled exception for {Method} {Path} (trace {TraceId}).",
            request.Method, request.Path, traceId);

        if (context.Response.HasStarted)
        {
            // Too late to replace whatever was already sent; the log line above is all that can be done.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        await context.Response.WriteAsJsonAsync(new UnexpectedErrorResponse { TraceId = traceId }, context.RequestAborted);
    }
}
