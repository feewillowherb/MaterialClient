namespace MaterialClient.Common.Events;

/// <summary>
///     Message sent when a user-initiated logout is requested.
///     Published via MessageBus after session and credential cleanup is complete.
/// </summary>
public class LogoutRequestedMessage;
