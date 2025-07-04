using System;
using System.IO;
using Newtonsoft.Json;

namespace CALauncher.Services;

public class SettingsService
{
    private readonly string _settingsFile;
    private Settings _settings = null!;

    public SettingsService()
    {
        _settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        LoadSettings();
    }

    public string? SelectedVersion
    {
        get => _settings.SelectedVersion;
        set
        {
            _settings.SelectedVersion = value;
            SaveSettings();
        }
    }

    public DateTime LastUpdateCheck
    {
        get => _settings.LastUpdateCheck;
        set
        {
            _settings.LastUpdateCheck = value;
            SaveSettings();
        }
    }

    public bool IncludeTestReleases
    {
        get => _settings.IncludeTestReleases;
        set
        {
            _settings.IncludeTestReleases = value;
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                _settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
            }
            else
            {
                _settings = new Settings();
            }
        }
        catch
        {
            _settings = new Settings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(_settingsFile, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

public class Settings
{
    public string? SelectedVersion { get; set; }
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    public bool IncludeTestReleases { get; set; } = false;
}
