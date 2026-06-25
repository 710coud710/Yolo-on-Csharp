using System.IO;
using System.Text.Json;
using Client.Constants;
using Client.Models;

namespace Client.Services
{
    public interface ISettingsService
    {
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
        string GetSettingsFilePath();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;
        private const string SettingsFileName = "settings.json";

        public SettingsService()
        {
            _settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisionCounter"
            );

            _settingsFilePath = Path.Combine(_settingsDirectory, SettingsFileName);

            EnsureSettingsDirectoryExists();
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    var defaultSettings = GetDefaultSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                string json = File.ReadAllText(_settingsFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                var settings = JsonSerializer.Deserialize<AppSettings>(json, options);
                
                return settings ?? GetDefaultSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return GetDefaultSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureSettingsDirectoryExists();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        public string GetSettingsFilePath()
        {
            return _settingsFilePath;
        }

        private void EnsureSettingsDirectoryExists()
        {
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }
        }

        private AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                AiModels = new AiModelSettings
                {
                    ModelsDirectory = DefaultSettings.ModelsDirectory,
                    ConfidenceThreshold = DefaultSettings.ConfidenceThreshold,
                    NmsThreshold = DefaultSettings.NmsThreshold,
                    UseGpu = DefaultSettings.UseGpu
                },
                Camera = new CameraSettings
                {
                    IpAddress = DefaultSettings.CameraIpAddress,
                    TriggerMode = DefaultSettings.CameraTriggerMode,
                    CaptureWidth = DefaultSettings.CameraCaptureWidth,
                    CaptureHeight = DefaultSettings.CameraCaptureHeight,
                    Fps = DefaultSettings.CameraFps
                },
                Image = new ImageSettings
                {
                    AutoSave = DefaultSettings.ImageAutoSave,
                    SaveDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "VisionCounter",
                        DefaultSettings.ImageSaveDirectoryName
                    ),
                    Quality = DefaultSettings.ImageQuality,
                    SaveMode = DefaultSettings.ImageSaveMode
                },
                DatabaseConnectionString = DefaultSettings.DatabaseConnectionString
            };
        }
    }
}
