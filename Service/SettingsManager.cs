using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NowPlayingPopup.Models;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Manages widget settings persistence and validation
    /// </summary>
    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public WidgetSettings CurrentSettings { get; private set; }

        public event EventHandler<WidgetSettings>? SettingsChanged;

        public SettingsManager()
        {
            _settingsFilePath = GetSettingsFilePath();
            CurrentSettings = new WidgetSettings();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        private static string GetSettingsFilePath()
        {
            string settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NowPlayingPopup");

            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            return Path.Combine(settingsDir, "widget_settings.json");
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loadedSettings = JsonSerializer.Deserialize<WidgetSettings>(json);
                        if (loadedSettings != null)
                        {
                            var errors = loadedSettings.ValidateSettings();
                            if (errors.Length == 0)
                            {
                                CurrentSettings = loadedSettings;
                            }
                            else
                            {
                                Debug.WriteLine($"Settings validation failed: {string.Join(", ", errors)}");
                                CurrentSettings = new WidgetSettings();
                                Save();
                            }
                        }
                        else
                        {
                            CurrentSettings = new WidgetSettings();
                            Save();
                        }
                    }
                    else
                    {
                        CurrentSettings = new WidgetSettings();
                        Save();
                    }
                }
                else
                {
                    CurrentSettings = new WidgetSettings();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadWidgetSettings error: {ex}");
                CurrentSettings = new WidgetSettings();
                Save();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(CurrentSettings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveWidgetSettings error: {ex}");
            }
        }

        public void Update(WidgetSettings newSettings)
        {
            if (newSettings == null)
                throw new ArgumentNullException(nameof(newSettings));

            var errors = newSettings.ValidateSettings();
            if (errors.Length > 0)
            {
                throw new ArgumentException($"Invalid settings: {string.Join(", ", errors)}");
            }

            CurrentSettings = newSettings;
            Save();
            SettingsChanged?.Invoke(this, CurrentSettings);
        }

        public void UpdatePosition(double x, double y)
        {
            if (CurrentSettings.RememberPosition)
            {
                CurrentSettings.PositionX = x;
                CurrentSettings.PositionY = y;
            }
        }

        public void UpdatePopupPosition(string position)
        {
            CurrentSettings.PopupPosition = position;
            Save();
            SettingsChanged?.Invoke(this, CurrentSettings);
        }
    }
}