namespace AudibleUtilities;

/// <summary>
/// Stored Audible credentials are missing or invalid and interactive login is required.
/// Thrown instead of opening login UI when the caller disallows interactive login (e.g. auto-scan).
/// </summary>
public sealed class AuthenticationRequiredException : Exception
{
	public Account? Account { get; }

	public AuthenticationRequiredException(Account? account, string? message = null, Exception? innerException = null)
		: base(message ?? "Audible authentication is required.", innerException)
		=> Account = account;
}
