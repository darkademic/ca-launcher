using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace CALauncher.Models;

public class GitHubRelease
{
    [JsonConstructor]
    public GitHubRelease()
    {
        // Explicit constructor for JSON deserialization
    }

    [JsonProperty("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonProperty("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();

    [JsonProperty("prerelease")]
    public bool PreRelease { get; set; }

    [JsonProperty("draft")]
    public bool Draft { get; set; }

    public ReleaseType ReleaseType
    {
        get
        {
            if (TagName.Contains("-DevTest-", StringComparison.OrdinalIgnoreCase))
                return ReleaseType.DevTest;

            if (TagName.Contains("-PreRelease-", StringComparison.OrdinalIgnoreCase))
                return ReleaseType.PreRelease;

            // Stable releases should have prerelease: false and follow version patterns like 0.90, 1.01, 1.07.1
            if (!PreRelease && System.Text.RegularExpressions.Regex.IsMatch(TagName, @"^\d+\.\d+(\.\d+)?$"))
                return ReleaseType.Stable;

            return ReleaseType.Unknown;
        }
    }

    public bool IsStable => ReleaseType == ReleaseType.Stable;

    public GitHubAsset? WindowsPortableAsset => Assets.FirstOrDefault(a =>
        a.Name.EndsWith("-x64-winportable.zip", StringComparison.OrdinalIgnoreCase));
}

public enum ReleaseType
{
    Unknown,
    Stable,
    PreRelease,
    DevTest
}

public class GitHubAsset
{
    [JsonConstructor]
    public GitHubAsset()
    {
        // Explicit constructor for JSON deserialization
    }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonProperty("size")]
    public long Size { get; set; }
}

public class LocalRelease : INotifyPropertyChanged
{
    private bool _isLatest;
    private bool _isLatestStable;

    public string Version { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime InstallDate { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public bool IsInstalled => File.Exists(ExecutablePath);

    public bool IsLatest
    {
        get => _isLatest;
        set
        {
            if (_isLatest != value)
            {
                _isLatest = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public bool IsLatestStable
    {
        get => _isLatestStable;
        set
        {
            if (_isLatestStable != value)
            {
                _isLatestStable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName
    {
        get
        {
            if (IsLatest && IsStable)
                return $"{Version} (Latest)";
            else if (IsLatestStable && IsStable)
                return $"{Version} (Latest Stable)";
            else if (IsLatest && !IsStable)
                return $"{Version} (Latest)";
            else
                return Version;
        }
    }

    public ReleaseType ReleaseType
    {
        get
        {
            if (Version.Contains("-DevTest-", StringComparison.OrdinalIgnoreCase))
                return ReleaseType.DevTest;

            if (Version.Contains("-PreRelease-", StringComparison.OrdinalIgnoreCase))
                return ReleaseType.PreRelease;

            // Stable releases follow version patterns like 0.90, 1.01, 1.07.1
            if (System.Text.RegularExpressions.Regex.IsMatch(Version, @"^\d+\.\d+(\.\d+)?$"))
                return ReleaseType.Stable;

            return ReleaseType.Unknown;
        }
    }

    public bool IsStable => ReleaseType == ReleaseType.Stable;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DownloadProgress
{
    public double Percentage { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }

    public string FormattedSpeed
    {
        get
        {
            if (SpeedBytesPerSecond < 1024)
                return $"{SpeedBytesPerSecond:F0} B/s";
            else if (SpeedBytesPerSecond < 1024 * 1024)
                return $"{SpeedBytesPerSecond / 1024:F1} KB/s";
            else if (SpeedBytesPerSecond < 1024 * 1024 * 1024)
                return $"{SpeedBytesPerSecond / (1024 * 1024):F1} MB/s";
            else
                return $"{SpeedBytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
        }
    }

    public string FormattedSize
    {
        get
        {
            if (TotalBytes < 1024)
                return $"{DownloadedBytes} / {TotalBytes} B";
            else if (TotalBytes < 1024 * 1024)
                return $"{(double)DownloadedBytes / 1024:F2} / {(double)TotalBytes / 1024:F2} KB";
            else if (TotalBytes < 1024 * 1024 * 1024)
                return $"{(double)DownloadedBytes / (1024 * 1024):F2} / {(double)TotalBytes / (1024 * 1024):F2} MB";
            else
                return $"{(double)DownloadedBytes / (1024 * 1024 * 1024):F2} / {(double)TotalBytes / (1024 * 1024 * 1024):F2} GB";
        }
    }
}
