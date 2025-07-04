using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CALauncher.Models;

namespace CALauncher.Services;

public class ReleaseService
{
	private const string StableApiUrl = "https://api.github.com/repos/Inq8/CAmod/releases";
	private const string DevTestApiUrl = "https://api.github.com/repos/darkademic/CAmod/releases";
    private readonly HttpClient _httpClient;
    private readonly string _releasesFolder;
    private readonly ReleaseMetadataService _metadataService;
    public long MaxDownloadSpeedBytesPerSecond { get; set; } = 0;

    public ReleaseService()
    {
        var handler = new HttpClientHandler();

		System.Net.ServicePointManager.SecurityProtocol =
			System.Net.SecurityProtocolType.Tls12 |
			System.Net.SecurityProtocolType.Tls13;

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CALauncher/1.0 (Windows)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _metadataService = new ReleaseMetadataService();
        _releasesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Releases");
        Directory.CreateDirectory(_releasesFolder);
    }

    public async Task<List<GitHubRelease>> GetAvailableReleasesAsync(bool includeTestReleases = false)
    {
        try
        {
            var allReleases = new List<GitHubRelease>();

            var stableReleases = await FetchReleasesFromUrlAsync(StableApiUrl);
            allReleases.AddRange(stableReleases);

			var devTestReleases = await FetchReleasesFromUrlAsync(DevTestApiUrl);
			allReleases.AddRange(devTestReleases);

            // Filter out drafts, unknown types, and releases without Windows portable assets
            var filteredReleases = allReleases
                .Where(r => !r.Draft &&
                           r.ReleaseType != ReleaseType.Unknown &&
                           r.WindowsPortableAsset != null)
                .OrderByDescending(r => r.PublishedAt)
                .ToList();

            // Store metadata for each release (preserves existing metadata, only adds/updates new releases)
            _metadataService.BulkStoreReleaseMetadata(filteredReleases);

            return filteredReleases;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch releases: {ex.Message}", ex);
        }
    }

    private async Task<List<GitHubRelease>> FetchReleasesFromUrlAsync(string apiUrl)
    {
		string response;

		if (apiUrl.StartsWith("http://") || apiUrl.StartsWith("https://"))
		{
			using var httpResponse = await _httpClient.GetAsync(apiUrl);

			if (!httpResponse.IsSuccessStatusCode)
			{
				return new List<GitHubRelease>();
			}

			response = await httpResponse.Content.ReadAsStringAsync();
		}
		else
		{
			if (!File.Exists(apiUrl))
			{
				return new List<GitHubRelease>();
			}

			response = await File.ReadAllTextAsync(apiUrl);
		}

		if (string.IsNullOrWhiteSpace(response))
		{
			return new List<GitHubRelease>();
		}

		var jArray = Newtonsoft.Json.Linq.JArray.Parse(response);
		var releases = new List<GitHubRelease>();

		foreach (var jItem in jArray)
		{
			var release = new GitHubRelease
			{
				TagName = jItem["tag_name"]?.ToString() ?? string.Empty,
				Name = jItem["name"]?.ToString() ?? string.Empty,
				PublishedAt = DateTime.TryParse(jItem["published_at"]?.ToString(), out DateTime pubDate) ? pubDate : DateTime.MinValue,
				PreRelease = bool.TryParse(jItem["prerelease"]?.ToString(), out bool preRel) && preRel,
				Draft = bool.TryParse(jItem["draft"]?.ToString(), out bool draft) && draft,
				Assets = new List<GitHubAsset>()
			};

			// Parse assets
			var assetsArray = jItem["assets"] as Newtonsoft.Json.Linq.JArray;
			if (assetsArray != null)
			{
				foreach (var jAsset in assetsArray)
				{
					var asset = new GitHubAsset
					{
						Name = jAsset["name"]?.ToString() ?? string.Empty,
						BrowserDownloadUrl = jAsset["browser_download_url"]?.ToString() ?? string.Empty,
						Size = long.TryParse(jAsset["size"]?.ToString(), out long size) ? size : 0
					};
					release.Assets.Add(asset);
				}
			}

			releases.Add(release);
		}

		return releases;
    }

    public List<LocalRelease> GetInstalledReleases()
    {
        var releases = new List<LocalRelease>();

        if (!Directory.Exists(_releasesFolder))
            return releases;

        foreach (var folder in Directory.GetDirectories(_releasesFolder))
        {
            var folderName = Path.GetFileName(folder);

            // Only include releases that have metadata (i.e., were downloaded through the launcher)
            var releaseDate = _metadataService.GetReleaseDate(folderName);
            if (!releaseDate.HasValue)
            {
                continue;
            }

            var executablePath = FindCombinedArmsExecutable(folder);

            if (!string.IsNullOrEmpty(executablePath))
            {
                releases.Add(new LocalRelease
                {
                    Version = folderName,
                    FolderPath = Path.GetDirectoryName(executablePath)!,
                    InstallDate = Directory.GetCreationTime(folder),
                    ReleaseDate = releaseDate,
                    ExecutablePath = executablePath
                });
            }
        }

        // Sort by release date (newest first), falling back to install date if release date is not available
        var orderedReleases = releases
            .OrderByDescending(r => r.ReleaseDate ?? r.InstallDate)
            .ToList();

        return orderedReleases;
    }

    public void DeleteRelease(LocalRelease release)
    {
        if (string.IsNullOrEmpty(release.Version))
            throw new ArgumentException("Release version cannot be null or empty");

        var releaseFolder = Path.Combine(_releasesFolder, release.Version);

        if (!Directory.Exists(releaseFolder))
            throw new DirectoryNotFoundException($"Release folder not found: {releaseFolder}");

        try
        {
            Directory.Delete(releaseFolder, recursive: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete release {release.Version}: {ex.Message}", ex);
        }
    }

    private string FindCombinedArmsExecutable(string searchFolder)
    {
        var directPath = Path.Combine(searchFolder, "CombinedArms.exe");
        if (File.Exists(directPath))
            return directPath;

        try
        {
            var files = Directory.GetFiles(searchFolder, "CombinedArms.exe", SearchOption.AllDirectories);
            return files.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> DownloadAndInstallReleaseAsync(GitHubRelease release, IProgress<DownloadProgress>? progress = null, Action? extractionStarted = null, CancellationToken cancellationToken = default)
    {
        var asset = release.WindowsPortableAsset;
        if (asset == null)
            throw new InvalidOperationException("No Windows portable asset found for this release");

        var releaseFolder = Path.Combine(_releasesFolder, release.TagName);

        // Check if already installed
        if (Directory.Exists(releaseFolder) && File.Exists(Path.Combine(releaseFolder, "CombinedArms.exe")))
        {
            return releaseFolder;
        }

        // Download the asset
        var tempFile = Path.GetTempFileName();
        try
        {
            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			response.EnsureSuccessStatusCode();

			using (var fileStream = new FileStream(tempFile, FileMode.Create))
			using (var downloadStream = await response.Content.ReadAsStreamAsync())
			{
				var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;

				if (totalBytes <= 0)
				{
					totalBytes = 50 * 1024 * 1024;
				}

				var downloadedBytes = 0L;
				var startTime = DateTime.Now;
				var lastReportTime = startTime;
				var lastReportedBytes = 0L;
				var lastThrottleTime = startTime;

				var speedSamples = new Queue<(DateTime time, long bytes)>();
				const int maxSpeedSamples = 10;
				const double speedCalculationWindowSeconds = 2.0;

				var buffer = new byte[8192];
				int bytesRead;

				while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();

					await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
					downloadedBytes += bytesRead;

					if (MaxDownloadSpeedBytesPerSecond > 0)
					{
						var now = DateTime.Now;
						var timeSinceThrottle = (now - lastThrottleTime).TotalSeconds;
						var expectedTimeForBytes = (double)bytesRead / MaxDownloadSpeedBytesPerSecond;

						if (timeSinceThrottle < expectedTimeForBytes)
						{
							var delayMs = (int)((expectedTimeForBytes - timeSinceThrottle) * 1000);
							if (delayMs > 0)
								await Task.Delay(delayMs, cancellationToken);
						}

						lastThrottleTime = DateTime.Now;
					}

					var now2 = DateTime.Now;
					var timeSinceLastReport = (now2 - lastReportTime).TotalSeconds;

					// Report progress every 1 second or when download completes, or after every significant chunk
					if (timeSinceLastReport >= 1.0 || downloadedBytes == totalBytes || downloadedBytes - lastReportedBytes >= 131072)
					{
						speedSamples.Enqueue((now2, downloadedBytes));

						while (speedSamples.Count > 0)
						{
							var oldestSample = speedSamples.Peek();
							if ((now2 - oldestSample.time).TotalSeconds > speedCalculationWindowSeconds)
							{
								speedSamples.Dequeue();
							}
							else
							{
								break;
							}
						}

						while (speedSamples.Count > maxSpeedSamples)
						{
							speedSamples.Dequeue();
						}

						var progressPercentage = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0;

						double speedBytesPerSecond = 0;
						if (speedSamples.Count >= 2)
						{
							var firstSample = speedSamples.First();
							var lastSample = speedSamples.Last();
							var timeSpan = (lastSample.time - firstSample.time).TotalSeconds;
							var bytesSpan = lastSample.bytes - firstSample.bytes;

							if (timeSpan > 0)
							{
								speedBytesPerSecond = bytesSpan / timeSpan;
							}
						}

						var progressInfo = new DownloadProgress
						{
							Percentage = progressPercentage,
							DownloadedBytes = downloadedBytes,
							TotalBytes = totalBytes,
							SpeedBytesPerSecond = speedBytesPerSecond
						};

                        if (progressPercentage >= 100)
                        {
                            extractionStarted?.Invoke();
                        }
                        else
                        {
                            progress?.Report(progressInfo);
                        }

                        lastReportTime = now2;
						lastReportedBytes = downloadedBytes;
					}
				}
			}

            if (!File.Exists(tempFile))
                throw new InvalidOperationException("Downloaded file does not exist");

            var fileInfo = new FileInfo(tempFile);
            if (fileInfo.Length == 0)
                throw new InvalidOperationException("Downloaded file is empty");

			if (Directory.Exists(releaseFolder))
				Directory.Delete(releaseFolder, true);

            Directory.CreateDirectory(releaseFolder);

            try
            {
                ZipFile.ExtractToDirectory(tempFile, releaseFolder);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract ZIP file: {ex.Message}", ex);
            }

            if (!Directory.GetFileSystemEntries(releaseFolder).Any())
                throw new InvalidOperationException("ZIP extraction completed but release folder is empty");

            return releaseFolder;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    public void LaunchRelease(LocalRelease release)
    {
        if (!release.IsInstalled)
            throw new InvalidOperationException($"Release {release.Version} is not installed");

        var workingDirectory = Path.GetDirectoryName(release.ExecutablePath);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = release.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(startInfo);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public async Task<List<GitHubRelease>> GetCachedAvailableReleasesAsync(bool includeTestReleases = false)
    {
        try
        {
			var cachedReleases = _metadataService.GetCachedReleases(includeTestReleases);
            return await Task.FromResult(cachedReleases);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to read cached releases: {ex.Message}", ex);
        }
    }
}
