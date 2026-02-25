namespace FirmwareKit.Lp;

/// <summary>
/// Provides a centralized logging mechanism for the FirmwareKit.Lp library.
/// </summary>
public static class LpLogger
{
    /// <summary>
    /// Delegate for info-level log messages.
    /// </summary>
    public static Action<string>? LogMessage { get; set; }

    /// <summary>
    /// Delegate for warning-level log messages.
    /// </summary>
    public static Action<string>? LogWarning { get; set; }

    /// <summary>
    /// Delegate for error-level log messages.
    /// </summary>
    public static Action<string>? LogError { get; set; }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message string.</param>
    public static void Info(string message) => LogMessage?.Invoke(message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message string.</param>
    public static void Warning(string message) => LogWarning?.Invoke(message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The message string.</param>
    public static void Error(string message) => LogError?.Invoke(message);
}
