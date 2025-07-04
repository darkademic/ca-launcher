using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CALauncher.Models;
using CALauncher.Services;

namespace CALauncher.ViewModels;

public enum StatusType
{
    Normal,
    Success,
    Warning,
    Error
}

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ReleaseService _releaseService;
    private readonly SettingsService _settingsService;
    private readonly Func<string, string, Task<bool>>? _showConfirmationDialog;
    private bool _isCheckingForUpdates;
    private bool _isDownloading;
    private double _downloadProgress;
    private string _downloadSpeed = string.Empty;
    private string _downloadSize = string.Empty;
    private string _statusText = "Ready";
    private LocalRelease? _selectedRelease;
    private bool _updateAvailable;
    private GitHubRelease? _latestAvailableRelease;
    private CancellationTokenSource? _downloadCancellationTokenSource;
    private StatusType _statusType = StatusType.Normal;

	public MainWindowViewModel(Func<string, string, Task<bool>>? showConfirmationDialog = null)
	{
		_releaseService = new ReleaseService();
		_settingsService = new SettingsService();
		_showConfirmationDialog = showConfirmationDialog;

		InstalledReleases = new ObservableCollection<LocalRelease>();

		// Initialize commands
		PlayCommand = new RelayCommand(PlaySelectedRelease, () => CanPlay);
		UpdateCommand = new AsyncRelayCommand(HandleUpdateCommandAsync, () => CanUpdate, () => IsCancelButton);
		DeleteSelectedReleaseCommand = new AsyncRelayCommand(DeleteSelectedReleaseAsync, () => CanDeleteSelectedRelease);
		ToggleIncludeTestReleasesCommand = new AsyncRelayCommand(ToggleIncludeTestReleasesAsync);

		_ = InitializeAsync();
	}

    public ObservableCollection<LocalRelease> InstalledReleases { get; }

    public ICommand PlayCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteSelectedReleaseCommand { get; }
    public ICommand ToggleIncludeTestReleasesCommand { get; }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        set
        {
            if (SetProperty(ref _isCheckingForUpdates, value))
            {
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanDeleteSelectedRelease));
                RefreshCommands();
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(UpdateButtonText));
                OnPropertyChanged(nameof(IsUpdateNowButton));
                OnPropertyChanged(nameof(IsCancelButton));
                OnPropertyChanged(nameof(IsUpdateButtonVisible));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanDeleteSelectedRelease));
                RefreshCommands();
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set => SetProperty(ref _downloadSpeed, value);
    }

    public string DownloadSize
    {
        get => _downloadSize;
        set => SetProperty(ref _downloadSize, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public LocalRelease? SelectedRelease
    {
        get => _selectedRelease;
        set
        {
            if (SetProperty(ref _selectedRelease, value))
            {
                _settingsService.SelectedVersion = value?.Version;
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanDeleteSelectedRelease));
                RefreshCommands();
            }
        }
    }

    public string UpdateButtonText
    {
        get
        {
            if (IsDownloading) return "Cancel Download";

            if (_updateAvailable)
            {
                return HasInstalledReleases ? "Update Now" : "Install Now";
            }

            return "Check for Updates";
        }
    }

    public bool IsUpdateNowButton => _updateAvailable && !IsDownloading;

    public bool IsCancelButton => IsDownloading;

    public bool IsUpdateButtonVisible
    {
        get
        {
            if (IsDownloading) return true;

            if (IsUpdateNowButton) return true;

            return CanCheckForUpdates;
        }
    }

    public bool CanPlay => SelectedRelease?.IsInstalled == true && !IsCheckingForUpdates && !IsDownloading;

    public bool CanUpdate => !IsCheckingForUpdates && (IsUpdateNowButton || CanCheckForUpdates);

    private bool CanCheckForUpdates
    {
        get
        {
            if (_settingsService.LastUpdateCheck == DateTime.MinValue)
                return true;

            var timeSinceLastCheck = DateTime.Now - _settingsService.LastUpdateCheck;
            return timeSinceLastCheck.TotalMinutes >= 5;
        }
    }

    public bool CanDeleteSelectedRelease => SelectedRelease?.IsInstalled == true && !IsCheckingForUpdates && !IsDownloading;

    public bool HasInstalledReleases => InstalledReleases.Any();

	public bool IncludeTestReleases
	{
		get => _settingsService.IncludeTestReleases;
		set
		{
			if (_settingsService.IncludeTestReleases != value)
			{
				_settingsService.IncludeTestReleases = value;
				OnPropertyChanged();
				RefreshInstalledReleases();
				SelectedRelease = InstalledReleases.FirstOrDefault();
				_ = CheckForUpdatesAsync(forceApiCall: false, preserveExistingState: false);
			}
		}
	}

    private async Task InitializeAsync()
	{
		try
		{
			SetStatus("Initializing...", StatusType.Normal);
			await CheckForUpdatesAsync(forceApiCall: true);
			RefreshInstalledReleases();

			// Update from cache to catch any releases that might've been installed before the launcher creates the releases metadata
			await CheckForUpdatesAsync(forceApiCall: false, preserveExistingState: false);

			if (!string.IsNullOrEmpty(_settingsService.SelectedVersion))
			{
				SelectedRelease = InstalledReleases.FirstOrDefault(r => r.Version == _settingsService.SelectedVersion);
			}

			SelectedRelease ??= InstalledReleases.FirstOrDefault();
		}
		catch (Exception ex)
		{
			SetStatus($"Error: {ex.Message}", StatusType.Error);
		}
	}

    private async Task CheckForUpdatesAsync(bool forceApiCall = false, bool preserveExistingState = false)
    {
        if (IsCheckingForUpdates || IsDownloading) return;

        try
        {
            IsCheckingForUpdates = true;
            SetStatus("Checking for updates...", StatusType.Normal);

            List<GitHubRelease> availableReleases;

            if (forceApiCall)
            {
                // Full API check - this will update the metadata
                availableReleases = await _releaseService.GetAvailableReleasesAsync(IncludeTestReleases);
                _settingsService.LastUpdateCheck = DateTime.Now;
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(IsUpdateButtonVisible));
            }
            else
            {
                availableReleases = await _releaseService.GetCachedAvailableReleasesAsync(IncludeTestReleases);
            }

            if (!preserveExistingState)
            {
                _updateAvailable = false;
                _latestAvailableRelease = null;
            }

            if (!availableReleases.Any())
            {
                SetStatus("No releases found", StatusType.Error);
                _updateAvailable = false;
                OnPropertyChanged(nameof(UpdateButtonText));
                OnPropertyChanged(nameof(IsUpdateNowButton));
                OnPropertyChanged(nameof(IsUpdateButtonVisible));
                OnPropertyChanged(nameof(CanUpdate));
                return;
            }

            var latestAvailable = IncludeTestReleases
                ? availableReleases.FirstOrDefault()
                : availableReleases.FirstOrDefault(r => r.IsStable);

            if (latestAvailable == null)
            {
                _updateAvailable = false;
                _latestAvailableRelease = null;
                SetStatus("No releases found", StatusType.Error);
            }
            else
            {
                var latestInstalled = IncludeTestReleases
                    ? InstalledReleases.OrderByDescending(r => r.ReleaseDate ?? DateTime.MinValue).FirstOrDefault()
                    : InstalledReleases.Where(r => r.IsStable).OrderByDescending(r => r.ReleaseDate ?? DateTime.MinValue).FirstOrDefault();

                if (latestInstalled == null || latestAvailable.PublishedAt > (latestInstalled.ReleaseDate ?? DateTime.MinValue))
                {
                    _updateAvailable = true;
                    _latestAvailableRelease = latestAvailable;
                    SetStatus($"Update available: {latestAvailable.TagName}", StatusType.Warning);
                }
                else
                {
                    _updateAvailable = false;
                    _latestAvailableRelease = null;
                    SetStatus("No new updates.", StatusType.Success);
                }
            }

            await UpdateLatestVersionMarkingAsync(availableReleases);

            OnPropertyChanged(nameof(UpdateButtonText));
            OnPropertyChanged(nameof(IsUpdateNowButton));
            OnPropertyChanged(nameof(IsUpdateButtonVisible));
            OnPropertyChanged(nameof(CanUpdate));
        }
        catch (Exception ex)
        {
            SetStatus($"Update check failed: {ex.Message}", StatusType.Error);
            _updateAvailable = false;
            OnPropertyChanged(nameof(UpdateButtonText));
            OnPropertyChanged(nameof(IsUpdateNowButton));
            OnPropertyChanged(nameof(IsUpdateButtonVisible));
            OnPropertyChanged(nameof(CanUpdate));
        }
        finally
        {
            IsCheckingForUpdates = false;
            RefreshCommands();
        }
    }

	private async Task HandleUpdateCommandAsync()
    {
        if (IsDownloading)
        {
            _downloadCancellationTokenSource?.Cancel();
            SetStatus("Cancelling download...", StatusType.Normal);
            return;
        }

        if (_updateAvailable && _latestAvailableRelease != null)
        {
            // This is an "Update Now" action - no rate limit check needed
            await DownloadReleaseAsync(_latestAvailableRelease);
            OnPropertyChanged(nameof(UpdateButtonText));
            OnPropertyChanged(nameof(IsUpdateNowButton));
            OnPropertyChanged(nameof(IsCancelButton));
            OnPropertyChanged(nameof(IsUpdateButtonVisible));
        }
        else
        {
            // This is a "Check for Updates" action - apply rate limit
            if (!CanCheckForUpdates)
            {
                var timeSinceLastCheck = DateTime.Now - _settingsService.LastUpdateCheck;
                var remainingMinutes = 5 - timeSinceLastCheck.TotalMinutes;
                SetStatus($"Please wait {remainingMinutes:F1} more minutes before checking for updates again", StatusType.Warning);
                return;
            }

            await CheckForUpdatesAsync(forceApiCall: true);
        }
    }

    private async Task DownloadReleaseAsync(GitHubRelease release)
    {
        _downloadCancellationTokenSource = new CancellationTokenSource();

		try
		{
			IsDownloading = true;
			DownloadProgress = 0;
			DownloadSpeed = string.Empty;
			DownloadSize = string.Empty;

			OnPropertyChanged(nameof(UpdateButtonText));
			OnPropertyChanged(nameof(IsUpdateNowButton));
			OnPropertyChanged(nameof(IsCancelButton));
			OnPropertyChanged(nameof(IsUpdateButtonVisible));

			SetStatus($"Downloading {release.TagName}...", StatusType.Normal);

			var progress = new Progress<DownloadProgress>(progressInfo =>
			{
				DownloadProgress = progressInfo.Percentage;
				DownloadSpeed = progressInfo.FormattedSpeed;
				DownloadSize = progressInfo.FormattedSize;
				SetStatus($"Downloading {release.TagName}  •  {progressInfo.Percentage:F1}%  •  {progressInfo.FormattedSpeed}  •  {progressInfo.FormattedSize}", StatusType.Normal);
			});

			var extractionStarted = new Action(() =>
			{
				SetStatus($"Extracting {release.TagName}...", StatusType.Normal);
			});

			await _releaseService.DownloadAndInstallReleaseAsync(release, progress, extractionStarted, _downloadCancellationTokenSource.Token);

			RefreshInstalledReleases();

			var availableReleases = await _releaseService.GetAvailableReleasesAsync(IncludeTestReleases);
			await UpdateLatestVersionMarkingAsync(availableReleases);

			SelectedRelease = InstalledReleases.FirstOrDefault(r => r.Version == release.TagName);

			SetStatus($"Successfully installed {release.TagName}", StatusType.Success);

			IsDownloading = false;
			await CheckForUpdatesAsync(forceApiCall: false, preserveExistingState: false);
        }
		catch (OperationCanceledException)
		{
			IsDownloading = false;
			SetStatus("Download cancelled", StatusType.Error);

			if (_latestAvailableRelease != null)
			{
				SetStatus($"Update available: {_latestAvailableRelease.TagName}", StatusType.Warning);
			}

			await CheckForUpdatesAsync(forceApiCall: false, preserveExistingState: true);
		}
		catch (Exception ex)
		{
			IsDownloading = false;
			SetStatus($"Download failed: {ex.Message}", StatusType.Error);
		}
		finally
		{
			DownloadProgress = 0;
			DownloadSpeed = string.Empty;
			DownloadSize = string.Empty;
			_downloadCancellationTokenSource?.Dispose();
			_downloadCancellationTokenSource = null;

			OnPropertyChanged(nameof(UpdateButtonText));
			OnPropertyChanged(nameof(IsUpdateNowButton));
			OnPropertyChanged(nameof(IsCancelButton));
			OnPropertyChanged(nameof(IsUpdateButtonVisible));
		}
    }

    public void PlaySelectedRelease()
    {
        if (SelectedRelease?.IsInstalled == true)
        {
            try
            {
                _releaseService.LaunchRelease(SelectedRelease);
                SetStatus($"Launched {SelectedRelease.Version}", StatusType.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"Launch failed: {ex.Message}", StatusType.Error);
            }
        }
    }

	public async Task DeleteSelectedReleaseAsync()
	{
		if (SelectedRelease?.IsInstalled != true)
		{
			return;
		}

		var releaseToDelete = SelectedRelease;

		if (_showConfirmationDialog != null)
		{
			var confirmed = await _showConfirmationDialog(
				"Confirm Deletion",
				$"Are you sure you want to delete {releaseToDelete.Version}?"
			);

			if (!confirmed)
			{
				return;
			}
		}

		try
		{
			StatusText = $"Deleting {releaseToDelete.Version}...";

			// Delete the release using the service (run on background thread to avoid blocking UI)
			await Task.Run(() => _releaseService.DeleteRelease(releaseToDelete));

			RefreshInstalledReleases();

			SelectedRelease = InstalledReleases.FirstOrDefault();

			await CheckForUpdatesAsync(forceApiCall: false, preserveExistingState: false);

			if (!_updateAvailable)
			{
				if (SelectedRelease == null)
				{
					SetStatus($"Successfully deleted {releaseToDelete.Version}. No releases remaining.", StatusType.Success);
				}
				else
				{
					SetStatus($"Successfully deleted {releaseToDelete.Version}", StatusType.Success);
				}
			}
		}
		catch (Exception ex)
		{
			SetStatus($"Failed to delete {releaseToDelete.Version}: {ex.Message}", StatusType.Error);
		}
	}

    public async Task ToggleIncludeTestReleasesAsync()
    {
        IncludeTestReleases = !IncludeTestReleases;
		await Task.CompletedTask;
    }

    private async Task UpdateLatestVersionMarkingAsync(List<GitHubRelease> availableReleases)
    {
        foreach (var release in InstalledReleases)
        {
            release.IsLatest = false;
            release.IsLatestStable = false;
        }

        if (!availableReleases.Any())
            return;

        var latestStableAvailable = availableReleases.FirstOrDefault(r => r.IsStable);
        var latestOverallAvailable = availableReleases.FirstOrDefault();

        if (latestStableAvailable != null && latestOverallAvailable != null)
        {
            if (latestStableAvailable.TagName == latestOverallAvailable.TagName)
            {
                var installedLatest = InstalledReleases.FirstOrDefault(r => r.Version == latestStableAvailable.TagName);
                if (installedLatest != null)
                {
                    installedLatest.IsLatest = true;
                }
            }
            else
            {
                var installedLatestStable = InstalledReleases.FirstOrDefault(r => r.Version == latestStableAvailable.TagName);
                if (installedLatestStable != null)
                {
                    installedLatestStable.IsLatestStable = true;
                }

                if (IncludeTestReleases)
                {
                    var installedLatestOverall = InstalledReleases.FirstOrDefault(r => r.Version == latestOverallAvailable.TagName);
                    if (installedLatestOverall != null)
                    {
                        installedLatestOverall.IsLatest = true;
                    }
                }
            }
        }
        else if (latestStableAvailable != null)
        {
            var installedLatest = InstalledReleases.FirstOrDefault(r => r.Version == latestStableAvailable.TagName);
            if (installedLatest != null)
            {
                installedLatest.IsLatest = true;
            }
        }
        else if (latestOverallAvailable != null && IncludeTestReleases)
        {
            var installedLatest = InstalledReleases.FirstOrDefault(r => r.Version == latestOverallAvailable.TagName);
            if (installedLatest != null)
            {
                installedLatest.IsLatest = true;
            }
        }

        await Task.CompletedTask;
    }

    private void RefreshInstalledReleases()
    {
        var installedReleases = _releaseService.GetInstalledReleases();

        var filteredReleases = IncludeTestReleases
            ? installedReleases
            : installedReleases.Where(r => r.IsStable).ToList();

        InstalledReleases.Clear();
        foreach (var release in filteredReleases)
        {
            InstalledReleases.Add(release);
        }

        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanDeleteSelectedRelease));
        OnPropertyChanged(nameof(HasInstalledReleases));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UpdateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteSelectedReleaseCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public StatusType StatusType
    {
        get => _statusType;
        set => SetProperty(ref _statusType, value);
    }

    private void SetStatus(string text, StatusType type = StatusType.Normal)
    {
        StatusText = text;
        StatusType = type;
    }
}
