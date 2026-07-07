using ApplicationServices;
using AudibleUtilities;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LibationUiBase;

/// <summary>
/// Runs background library auto-scan without opening login UI.
/// Pauses the timer after an authentication failure until the user logs in manually.
/// </summary>
public sealed class AutoScanRunner
{
	private readonly Func<bool> isAutoScanEnabled;
	private readonly Action pauseTimer;
	private readonly Action resumeTimer;
	private readonly Func<Task>? notifyAuthRequired;

	private bool pausedForAuthentication;

	public AutoScanRunner(
		Func<bool> isAutoScanEnabled,
		Action pauseTimer,
		Action resumeTimer,
		Func<Task>? notifyAuthRequired = null)
	{
		this.isAutoScanEnabled = isAutoScanEnabled;
		this.pauseTimer = pauseTimer;
		this.resumeTimer = resumeTimer;
		this.notifyAuthRequired = notifyAuthRequired;
	}

	public void OnManualScanSucceeded()
	{
		if (!pausedForAuthentication)
			return;

		pausedForAuthentication = false;
		if (isAutoScanEnabled())
			resumeTimer();
	}

	public void OnAutoScanSettingChanged()
	{
		if (!isAutoScanEnabled())
			pausedForAuthentication = false;
	}

	public async Task RunAsync()
	{
		if (!isAutoScanEnabled() || pausedForAuthentication)
			return;

		using var persister = AudibleApiStorage.GetAccountsSettingsPersister();
		var accounts = persister.AccountsSettings
			.GetAll()
			.Where(a => a.LibraryScan)
			.ToArray();

		if (accounts.Length == 0)
			return;

		try
		{
			await Task.Run(() => LibraryCommands.ImportAccountAsync(accounts, allowInteractiveLogin: false));
		}
		catch (OperationCanceledException)
		{
			Log.Information("Audible login attempt cancelled by user");
		}
		catch (Exception ex) when (AuthenticationExceptionHelper.IsAuthenticationFailure(ex))
		{
			await pauseForAuthenticationAsync(ex);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Error invoking auto-scan");
		}
	}

	private async Task pauseForAuthenticationAsync(Exception ex)
	{
		if (pausedForAuthentication)
			return;

		pausedForAuthentication = true;
		pauseTimer();

		Log.Warning(ex, "Auto-scan paused: Audible login is required. Log in with Import > Scan Library to resume background scans.");

		if (notifyAuthRequired is not null)
			await notifyAuthRequired();
	}
}
