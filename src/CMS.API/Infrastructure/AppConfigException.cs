namespace CMS.API.Infrastructure;

/// <summary>
/// Thrown when <c>dbo.SysConfig</c> lacks a usable <c>appConfig</c> entry (missing row, invalid JSON, or a missing
/// <c>defaultPassword</c>). Controllers translate it to <c>500</c> with the message.
/// </summary>
public sealed class AppConfigException(string message, Exception? innerException = null)
    : Exception(message, innerException);
