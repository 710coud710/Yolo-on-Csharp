using System.Text.Json;
using Client.Models;
using Client.Services;

namespace Client.Utils
{
    public static class SettingsTestHelper
    {
        /// <summary>
        /// Test serialization và deserialization của AppSettings
        /// </summary>
        public static bool TestSettingsSerialization()
        {
            try
            {
                // Tạo settings mẫu
                var originalSettings = new AppSettings
                {
                    AiModels = new AiModelSettings
                    {
                        ModelsDirectory = "Models",
                        ConfidenceThreshold = 0.25,
                        NmsThreshold = 0.45,
                        UseGpu = false
                    },
                    Camera = new CameraSettings
                    {
                        IpAddress = "192.168.1.100",
                        TriggerMode = "Software",
                        CaptureWidth = 1920,
                        CaptureHeight = 1080
                    },
                    Image = new ImageSettings
                    {
                        AutoSave = true,
                        SaveDirectory = @"C:\Temp\Images",
                        Quality = 90
                    },
                    SelectedClassIds = new List<int> { 0, 1, 2, 3 }
                };

                // Serialize
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(originalSettings, options);
                Console.WriteLine("Serialized JSON:");
                Console.WriteLine(json);
                Console.WriteLine();

                // Deserialize
                var deserializedSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

                // Verify
                if (deserializedSettings == null)
                {
                    Console.WriteLine("❌ Deserialization failed: Result is null");
                    return false;
                }

                bool isValid = true;

                // Check AiModels
                if (deserializedSettings.AiModels.ModelsDirectory != originalSettings.AiModels.ModelsDirectory)
                {
                    Console.WriteLine($"❌ AiModels.ModelsDirectory mismatch: {deserializedSettings.AiModels.ModelsDirectory} != {originalSettings.AiModels.ModelsDirectory}");
                    isValid = false;
                }

                // Check Camera
                if (deserializedSettings.Camera.CaptureWidth != originalSettings.Camera.CaptureWidth)
                {
                    Console.WriteLine($"❌ Camera.CaptureWidth mismatch: {deserializedSettings.Camera.CaptureWidth} != {originalSettings.Camera.CaptureWidth}");
                    isValid = false;
                }

                // Check Image
                if (deserializedSettings.Image.Quality != originalSettings.Image.Quality)
                {
                    Console.WriteLine($"❌ Image.Quality mismatch: {deserializedSettings.Image.Quality} != {originalSettings.Image.Quality}");
                    isValid = false;
                }

                // Check SelectedClassIds
                if (deserializedSettings.SelectedClassIds.Count != originalSettings.SelectedClassIds.Count)
                {
                    Console.WriteLine($"❌ SelectedClassIds count mismatch: {deserializedSettings.SelectedClassIds.Count} != {originalSettings.SelectedClassIds.Count}");
                    isValid = false;
                }

                if (isValid)
                {
                    Console.WriteLine("✅ Settings serialization/deserialization test PASSED");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test SettingsService với file thực tế
        /// </summary>
        public static bool TestSettingsService()
        {
            try
            {
                var settingsService = new SettingsService();
                
                Console.WriteLine($"Settings file path: {settingsService.GetSettingsFilePath()}");
                
                // Load settings
                var settings = settingsService.LoadSettings();
                Console.WriteLine("✅ LoadSettings() successful");

                // Modify settings
                settings.SelectedClassIds.Add(99);
                Console.WriteLine($"Added class ID 99. Total selected: {settings.SelectedClassIds.Count}");

                // Save settings
                settingsService.SaveSettings(settings);
                Console.WriteLine("✅ SaveSettings() successful");

                // Load again to verify
                var reloadedSettings = settingsService.LoadSettings();
                
                if (reloadedSettings.SelectedClassIds.Contains(99))
                {
                    Console.WriteLine("✅ Settings persistence test PASSED");
                    return true;
                }
                else
                {
                    Console.WriteLine("❌ Settings persistence test FAILED: Class ID 99 not found after reload");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Chạy tất cả tests
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Settings Tests");
            Console.WriteLine("========================================");
            Console.WriteLine();

            Console.WriteLine("Test 1: Serialization/Deserialization");
            Console.WriteLine("--------------------------------------");
            bool test1 = TestSettingsSerialization();
            Console.WriteLine();

            Console.WriteLine("Test 2: SettingsService with File");
            Console.WriteLine("--------------------------------------");
            bool test2 = TestSettingsService();
            Console.WriteLine();

            Console.WriteLine("========================================");
            Console.WriteLine($"Results: {(test1 && test2 ? "✅ ALL TESTS PASSED" : "❌ SOME TESTS FAILED")}");
            Console.WriteLine("========================================");
        }
    }
}
