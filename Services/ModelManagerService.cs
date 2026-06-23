using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Client.Services
{
    public class ActiveModelConfig
    {
        public string CurrentProduct { get; set; } = string.Empty;
        public string CurrentModel { get; set; } = string.Empty;
    }

    public class ModelManagerService : IDisposable
    {
        private readonly string _modelsDir;
        private readonly string _configPath;
        private FileSystemWatcher? _watcher;
        private readonly object _lock = new();

        public string ModelsDir => _modelsDir;

        public event EventHandler<string>? ActiveModelChanged;

        public ModelManagerService(string? modelsDir = null)
        {
            // Mặc định thư mục Models nằm ở AppData/Roaming để không bị xóa khi update app
            _modelsDir = modelsDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisionCounter",
                "Models"
            );
            _configPath = Path.Combine(_modelsDir, "active.json");

            EnsureDirectoriesExist();
            StartWatcher();
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_modelsDir))
                {
                    Directory.CreateDirectory(_modelsDir);
                }

                if (!File.Exists(_configPath))
                {
                    // Tạo active.json mẫu nếu chưa có
                    var defaultConfig = new ActiveModelConfig
                    {
                        CurrentProduct = "MockProduct",
                        CurrentModel = "MockMode"
                    };
                    WriteConfig(defaultConfig);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing models directory: {ex.Message}");
            }
        }

        private void StartWatcher()
        {
            try
            {
                _watcher = new FileSystemWatcher(_modelsDir, "active.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                _watcher.Changed += OnConfigChanged;
                _watcher.Created += OnConfigChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start FileSystemWatcher: {ex.Message}");
            }
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Tránh đọc file khi đang ghi hoặc bị lock ngắn hạn
            System.Threading.Thread.Sleep(100); 
            
            var modelPath = GetActiveModelPath();
            ActiveModelChanged?.Invoke(this, modelPath);
        }

        public string GetActiveModelPath()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configPath))
                    {
                        var json = File.ReadAllText(_configPath);
                        var config = JsonSerializer.Deserialize<ActiveModelConfig>(json);
                        if (config != null && !string.IsNullOrWhiteSpace(config.CurrentModel))
                        {
                            // Nếu model là MockMode, trả về MockMode trực tiếp
                            if (config.CurrentModel.Equals("MockMode", StringComparison.OrdinalIgnoreCase))
                            {
                                return "MockMode";
                            }
                            
                            // Nếu là đường dẫn tương đối, chuyển thành tuyệt đối
                            if (!Path.IsPathRooted(config.CurrentModel))
                            {
                                return Path.Combine(_modelsDir, config.CurrentModel);
                            }
                            return config.CurrentModel;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading active model config: {ex.Message}");
                }
                return "MockMode";
            }
        }

        public string GetActiveProduct()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configPath))
                    {
                        var json = File.ReadAllText(_configPath);
                        var config = JsonSerializer.Deserialize<ActiveModelConfig>(json);
                        return config?.CurrentProduct ?? "MockProduct";
                    }
                }
                catch
                {
                    // Ignore
                }
                return "MockProduct";
            }
        }

        public List<string> GetAvailableModels()
        {
            var onnxFiles = new List<string>();
            try
            {
                if (Directory.Exists(_modelsDir))
                {
                    // Lấy tất cả file .onnx ở thư mục gốc và thư mục con
                    var files = Directory.GetFiles(_modelsDir, "*.onnx", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        // Lưu đường dẫn tương đối so với thư mục Models
                        var relativePath = Path.GetRelativePath(_modelsDir, file);
                        onnxFiles.Add(relativePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning models directory: {ex.Message}");
            }
            return onnxFiles;
        }

        public void SetActiveModel(string product, string modelPath)
        {
            lock (_lock)
            {
                try
                {
                    // Tạm dừng watcher để tránh kích hoạt sự kiện lặp lại khi tự ghi
                    if (_watcher != null) _watcher.EnableRaisingEvents = false;

                    var config = new ActiveModelConfig
                    {
                        CurrentProduct = product,
                        CurrentModel = modelPath
                    };
                    WriteConfig(config);
                }
                finally
                {
                    if (_watcher != null) _watcher.EnableRaisingEvents = true;
                }
            }
        }

        private void WriteConfig(ActiveModelConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }
}
