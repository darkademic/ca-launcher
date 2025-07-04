using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CALauncher.Models;
using Newtonsoft.Json;

namespace CALauncher.Services;

public class ReleaseMetadataService
{
    private readonly string _metadataFile;
    private Dictionary<string, ReleaseMetadata> _metadata = new();

    public ReleaseMetadataService()
    {
        _metadataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "release-metadata.json");
        LoadMetadata();
    }

    public void StoreReleaseMetadata(string version, DateTime releaseDate)
    {
        _metadata[version] = new ReleaseMetadata
        {
            Version = version,
            ReleaseDate = releaseDate
        };
        SaveMetadata();
    }

    public void StoreReleaseMetadata(GitHubRelease release)
    {
        var windowsAsset = release.WindowsPortableAsset;

        _metadata[release.TagName] = new ReleaseMetadata
        {
            Version = release.TagName,
            Name = release.Name,
            ReleaseDate = release.PublishedAt,
            PreRelease = release.PreRelease,
            Draft = release.Draft,
            WindowsAssetName = windowsAsset?.Name ?? string.Empty,
            WindowsAssetDownloadUrl = windowsAsset?.BrowserDownloadUrl ?? string.Empty,
            WindowsAssetSize = windowsAsset?.Size ?? 0
        };
        SaveMetadata();
    }

    public void BulkStoreReleaseMetadata(IEnumerable<GitHubRelease> releases)
    {
        var existingCount = _metadata.Count;
        var updatedCount = 0;
        var addedCount = 0;

        foreach (var release in releases)
        {
            var windowsAsset = release.WindowsPortableAsset;
            var isExisting = _metadata.ContainsKey(release.TagName);

            _metadata[release.TagName] = new ReleaseMetadata
            {
                Version = release.TagName,
                Name = release.Name,
                ReleaseDate = release.PublishedAt,
                PreRelease = release.PreRelease,
                Draft = release.Draft,
                WindowsAssetName = windowsAsset?.Name ?? string.Empty,
                WindowsAssetDownloadUrl = windowsAsset?.BrowserDownloadUrl ?? string.Empty,
                WindowsAssetSize = windowsAsset?.Size ?? 0
            };

            if (isExisting)
                updatedCount++;
            else
                addedCount++;
        }

        SaveMetadata();
    }

    public DateTime? GetReleaseDate(string version)
    {
        return _metadata.TryGetValue(version, out var metadata) ? metadata.ReleaseDate : null;
    }

	public List<GitHubRelease> GetCachedReleases(bool includeTestReleases = false)
	{
		var releases = new List<GitHubRelease>();

		foreach (var metadata in _metadata.Values)
		{
			// Skip if we don't have complete Windows asset information
			if (string.IsNullOrEmpty(metadata.WindowsAssetDownloadUrl))
			{
				continue;
			}

			// Create GitHubRelease from cached metadata
			var release = new GitHubRelease
			{
				TagName = metadata.Version,
				Name = metadata.Name,
				PublishedAt = metadata.ReleaseDate,
				PreRelease = metadata.PreRelease,
				Draft = metadata.Draft,
				Assets = new List<GitHubAsset>()
			};

			// Add the Windows portable asset
			if (!string.IsNullOrEmpty(metadata.WindowsAssetName))
			{
				release.Assets.Add(new GitHubAsset
				{
					Name = metadata.WindowsAssetName,
					BrowserDownloadUrl = metadata.WindowsAssetDownloadUrl,
					Size = metadata.WindowsAssetSize
				});
			}

			releases.Add(release);
		}

		// Filter out drafts, unknown types, and releases without Windows portable assets
		var filteredReleases = releases
			.Where(r => !r.Draft &&
					   r.ReleaseType != ReleaseType.Unknown &&
					   r.WindowsPortableAsset != null)
			.OrderByDescending(r => r.PublishedAt)
			.ToList();

		// If not including test releases, filter out prerelease versions
		if (!includeTestReleases)
		{
			var beforeFilter = filteredReleases.Count;
			filteredReleases = filteredReleases
				.Where(r => !r.PreRelease)
				.ToList();
		}

		return filteredReleases;
	}

    public void RemoveReleaseMetadata(string version)
    {
        if (_metadata.ContainsKey(version))
        {
            _metadata.Remove(version);
            SaveMetadata();
        }
    }

    private void LoadMetadata()
    {
        try
        {
            if (File.Exists(_metadataFile))
            {
                var json = File.ReadAllText(_metadataFile);
                _metadata = JsonConvert.DeserializeObject<Dictionary<string, ReleaseMetadata>>(json) ?? new();
            }
        }
        catch
        {
            _metadata = new Dictionary<string, ReleaseMetadata>();
        }
    }

    private void SaveMetadata()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_metadata, Formatting.Indented);
            File.WriteAllText(_metadataFile, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

public class ReleaseMetadata
{
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public bool PreRelease { get; set; }
    public bool Draft { get; set; }
    public string WindowsAssetName { get; set; } = string.Empty;
    public string WindowsAssetDownloadUrl { get; set; } = string.Empty;
    public long WindowsAssetSize { get; set; }
}
