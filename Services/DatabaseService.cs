using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Client.Models;

namespace Client.Services
{
    public class DatabaseService
    {
        // Removed FixedConnectionString with masked password. Use DefaultSettings.DatabaseConnectionString instead.

        public static int CurrentMachineId { get; set; } = 1;

        private readonly ISettingsService _settingsService;

        public DatabaseService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        private string GetConnectionString()
        {
            var settings = _settingsService?.LoadSettings();
            if (settings != null && !string.IsNullOrWhiteSpace(settings.DatabaseConnectionString))
            {
                return settings.DatabaseConnectionString;
            }
            return Client.Constants.DefaultSettings.DatabaseConnectionString;
        }

        public bool TestConnection(string connectionString, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void InitializeDatabase(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                string databaseName = builder.InitialCatalog;

                // Connect to master to check/create the database
                builder.InitialCatalog = "master";
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();

                    // Check if database exists
                    var checkDbCmd = new SqlCommand($"SELECT database_id FROM sys.databases WHERE name = '{databaseName}'", connection);
                    var dbId = checkDbCmd.ExecuteScalar();

                    if (dbId == null)
                    {
                        var createDbCmd = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
                        createDbCmd.ExecuteNonQuery();
                    }
                }

                // Connect to actual database to create tables
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // 1. Table: models
                    string createModels = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'models')
                        BEGIN
                            CREATE TABLE models (
                                id INT IDENTITY(1,1) PRIMARY KEY,
                                model_code VARCHAR(100) UNIQUE NOT NULL,
                                model_name VARCHAR(255) NOT NULL,
                                model_path VARCHAR(500) NOT NULL,
                                is_active BIT NOT NULL DEFAULT 0,
                                description NVARCHAR(MAX) NULL,
                                created_at DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END";
                    using (var cmd = new SqlCommand(createModels, connection)) cmd.ExecuteNonQuery();

                    // 2. Table: classes
                    string createClasses = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'classes')
                        BEGIN
                            CREATE TABLE classes (
                                id INT IDENTITY(1,1) PRIMARY KEY,
                                class_code INT NOT NULL,
                                class_name VARCHAR(100) NOT NULL,
                                description NVARCHAR(MAX) NULL,
                                created_at DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END";
                    using (var cmd = new SqlCommand(createClasses, connection)) cmd.ExecuteNonQuery();

                    // 3. Table: items
                    string createItems = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'items')
                        BEGIN
                            CREATE TABLE items (
                                id INT IDENTITY(1,1) PRIMARY KEY,
                                class_id INT NOT NULL,
                                item_code VARCHAR(100) UNIQUE NOT NULL,
                                item_name NVARCHAR(255) NOT NULL,
                                item_type VARCHAR(50) NOT NULL,
                                is_active BIT NOT NULL DEFAULT 1,
                                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_items_classes FOREIGN KEY (class_id) REFERENCES classes(id)
                            );
                        END";
                    using (var cmd = new SqlCommand(createItems, connection)) cmd.ExecuteNonQuery();

                    // 4. Table: machines
                    string createMachines = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'machines')
                        BEGIN
                            CREATE TABLE machines (
                                id INT IDENTITY(1,1) PRIMARY KEY,
                                hostname VARCHAR(100) NOT NULL,
                                machine_name NVARCHAR(100) NOT NULL,
                                ip VARCHAR(50) NULL,
                                password VARCHAR(255) NOT NULL,
                                status VARCHAR(20) NOT NULL DEFAULT 'Offline',
                                last_seen DATETIME NULL,
                                created_at DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END";
                    using (var cmd = new SqlCommand(createMachines, connection)) cmd.ExecuteNonQuery();

                    // 5. Table: detections
                    string createDetections = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'detections')
                        BEGIN
                            CREATE TABLE detections (
                                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                                machine_id INT NOT NULL,
                                item_id INT NOT NULL,
                                model_id INT NOT NULL,
                                processing_time_ms FLOAT NOT NULL,
                                image_path VARCHAR(500) NULL,
                                result_image_path VARCHAR(500) NULL,
                                total_objects INT NOT NULL DEFAULT 0,
                                status_result VARCHAR(50) NOT NULL DEFAULT 'Pending',
                                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_detections_machines FOREIGN KEY (machine_id) REFERENCES machines(id),
                                CONSTRAINT FK_detections_items FOREIGN KEY (item_id) REFERENCES items(id),
                                CONSTRAINT FK_detections_models FOREIGN KEY (model_id) REFERENCES models(id)
                            );
                        END
                        ELSE
                        BEGIN
                            -- Migration for existing table that might be missing item_id
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('detections') AND name = 'item_id')
                            BEGIN
                                ALTER TABLE detections ADD item_id INT NULL;
                            END
                            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('detections') AND name = 'item_id' AND is_nullable = 1)
                            BEGIN
                                ALTER TABLE detections ALTER COLUMN item_id INT NOT NULL;
                            END
                            IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_detections_items')
                            BEGIN
                                ALTER TABLE detections ADD CONSTRAINT FK_detections_items FOREIGN KEY (item_id) REFERENCES items(id);
                            END
                            -- Add status_result if missing
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('detections') AND name = 'status_result')
                            BEGIN
                                ALTER TABLE detections ADD status_result VARCHAR(50) NOT NULL DEFAULT 'Pending';
                            END
                        END";
                    using (var cmd = new SqlCommand(createDetections, connection)) cmd.ExecuteNonQuery();

                    // 6. Table: detection_details
                    string createDetails = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'detection_details')
                        BEGIN
                            CREATE TABLE detection_details (
                                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                                detection_id BIGINT NOT NULL,
                                item_id INT NOT NULL,
                                quantity INT NOT NULL DEFAULT 0,
                                avg_confidence FLOAT NOT NULL DEFAULT 0.0,
                                result_json NVARCHAR(MAX) NULL,
                                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_detection_details_detections FOREIGN KEY (detection_id) REFERENCES detections(id) ON DELETE CASCADE,
                                CONSTRAINT FK_detection_details_items FOREIGN KEY (item_id) REFERENCES items(id)
                            );
                        END";
                    using (var cmd = new SqlCommand(createDetails, connection)) cmd.ExecuteNonQuery();

                    // 7. Table: logs
                    string createLogs = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'logs')
                        BEGIN
                            CREATE TABLE logs (
                                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                                detection_id BIGINT NULL,
                                action VARCHAR(50) NOT NULL,
                                actor NVARCHAR(100) NOT NULL DEFAULT 'System',
                                description NVARCHAR(MAX) NOT NULL,
                                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_logs_detections FOREIGN KEY (detection_id) REFERENCES detections(id) ON DELETE SET NULL
                            );
                        END";
                    using (var cmd = new SqlCommand(createLogs, connection)) cmd.ExecuteNonQuery();

                    // SeedDefaultData(connection); // removed automatic seeding
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization failed: {ex.Message}");
                throw;
            }
        }

        private void SeedDefaultData(SqlConnection connection)
        {
            // Automatic seeding removed. Data should be inserted manually when needed.
        }

        public async Task<List<DbItem>> GetActiveItemsAsync()
        {
            var list = new List<DbItem>();
            string connString = GetConnectionString();

            if (string.IsNullOrWhiteSpace(connString)) return list;

            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT i.id, i.class_id, i.item_code, i.item_name, i.item_type, i.is_active, 
                               c.class_code, c.class_name
                        FROM items i
                        JOIN classes c ON i.class_id = c.id
                        WHERE i.is_active = 1";

                    using (var cmd = new SqlCommand(query, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbItem
                            {
                                Id = reader.GetInt32(0),
                                ClassId = reader.GetInt32(1),
                                ItemCode = reader.GetString(2),
                                ItemName = reader.GetString(3),
                                ItemType = reader.GetString(4),
                                IsActive = reader.GetBoolean(5),
                                ClassCode = reader.GetInt32(6),
                                ClassName = reader.GetString(7)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading items from database: {ex.Message}");
            }
            return list;
        }

        public async Task<(List<DbItem> Items, int TotalCount)> GetActiveItemsPagedAsync(string? searchText, int pageNumber, int pageSize = 100, bool includeInactive = false)
        {
            var list = new List<DbItem>();
            int totalCount = 0;
            string connString = GetConnectionString();

            if (string.IsNullOrWhiteSpace(connString)) return (list, 0);

            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();

                    // Xây dựng mệnh đề WHERE lọc tìm kiếm
                    string whereClause = "WHERE 1 = 1";
                    if (!includeInactive)
                    {
                        whereClause += " AND i.is_active = 1";
                    }
                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        whereClause += " AND (i.item_code LIKE @SearchPattern OR i.item_name LIKE @SearchPattern)";
                    }

                    // 1. Đếm tổng số bản ghi khớp bộ lọc
                    string countQuery = $"SELECT COUNT(*) FROM items i JOIN classes c ON i.class_id = c.id {whereClause}";
                    using (var countCmd = new SqlCommand(countQuery, connection))
                    {
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            countCmd.Parameters.AddWithValue("@SearchPattern", $"%{searchText.Trim()}%");
                        }
                        totalCount = (int)await countCmd.ExecuteScalarAsync();
                    }

                    // 2. Truy vấn dữ liệu phân trang
                    int offset = (pageNumber - 1) * pageSize;
                    string itemsQuery = $@"
                        SELECT i.id, i.class_id, i.item_code, i.item_name, i.item_type, i.is_active, 
                               c.class_code, c.class_name
                        FROM items i
                        JOIN classes c ON i.class_id = c.id
                        {whereClause}
                        ORDER BY i.item_code, i.id
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    using (var cmd = new SqlCommand(itemsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Offset", offset);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            cmd.Parameters.AddWithValue("@SearchPattern", $"%{searchText.Trim()}%");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new DbItem
                                {
                                    Id = reader.GetInt32(0),
                                    ClassId = reader.GetInt32(1),
                                    ItemCode = reader.GetString(2),
                                    ItemName = reader.GetString(3),
                                    ItemType = reader.GetString(4),
                                    IsActive = reader.GetBoolean(5),
                                    ClassCode = reader.GetInt32(6),
                                    ClassName = reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading paged items from database: {ex.Message}");
            }
            return (list, totalCount);
        }

        public async Task<DbItem?> GetItemByIdAsync(int id)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return null;

            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT i.id, i.class_id, i.item_code, i.item_name, i.item_type, i.is_active, 
                               c.class_code, c.class_name
                        FROM items i
                        JOIN classes c ON i.class_id = c.id
                        WHERE i.id = @Id";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new DbItem
                                {
                                    Id = reader.GetInt32(0),
                                    ClassId = reader.GetInt32(1),
                                    ItemCode = reader.GetString(2),
                                    ItemName = reader.GetString(3),
                                    ItemType = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    IsActive = reader.GetBoolean(5),
                                    ClassCode = reader.GetInt32(6),
                                    ClassName = reader.GetString(7)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting item by ID {id}: {ex.Message}");
            }
            return null;
        }

        public async Task<long> SaveDetectionResultAsync(DetectionResult result, DbItem selectedItem, string? rawImagePath, string? resultImagePath)
        {
            string connString = GetConnectionString();

            if (string.IsNullOrWhiteSpace(connString))
            {
                InMemoryLogService.Instance.LogError("Database connection string is empty or null.");
                return 0;
            }

            try
            {
                InMemoryLogService.Instance.LogInfo($"Saving detection to database (MachineId={CurrentMachineId}, ItemId={selectedItem.Id})...");
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();

                    // 1. Use cached machine ID
                    int machineId = CurrentMachineId;

                    // 2. Get or create model ID
                    int modelId = await GetOrCreateModelIdAsync(connection, result.ModelPath);

                    // 3. Insert detection
                    string insertDetQuery = @"
                        INSERT INTO detections (machine_id, item_id, model_id, processing_time_ms, image_path, result_image_path, total_objects, status_result, created_at)
                        OUTPUT INSERTED.id
                        VALUES (@MachineId, @ItemId, @ModelId, @ProcessingTime, @ImagePath, @ResultImagePath, @TotalObjects, 'Pending', @CreatedAt)";

                    long detectionId;
                    using (var cmd = new SqlCommand(insertDetQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@MachineId", machineId);
                        cmd.Parameters.AddWithValue("@ItemId", selectedItem.Id);
                        cmd.Parameters.AddWithValue("@ModelId", modelId);
                        cmd.Parameters.AddWithValue("@ProcessingTime", result.ProcessingTimeMs);
                        cmd.Parameters.AddWithValue("@ImagePath", (object?)rawImagePath ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ResultImagePath", (object?)resultImagePath ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TotalObjects", result.Count);
                        cmd.Parameters.AddWithValue("@CreatedAt", result.Timestamp);

                        detectionId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    // // 4. Insert detection details
                    // double avgConfidence = 0.0;
                    // if (result.Count > 0 && result.DetectedObjects.Any())
                    // {
                    //     avgConfidence = result.DetectedObjects.Average(o => o.Confidence);
                    // }

                    // var detailObj = new
                    // {
                    //     class_id = selectedItem.ClassCode,
                    //     count = result.Count,
                    //     avg_confidence = avgConfidence
                    // };
                    // string resultJson = System.Text.Json.JsonSerializer.Serialize(detailObj);

                    // string insertDetailQuery = @"
                    //     INSERT INTO detection_details (detection_id, item_id, quantity, avg_confidence, result_json, created_at)
                    //     VALUES (@DetectionId, @ItemId, @Quantity, @AvgConfidence, @ResultJson, @CreatedAt)";

                    // using (var cmd = new SqlCommand(insertDetailQuery, connection))
                    // {
                    //     cmd.Parameters.AddWithValue("@DetectionId", detectionId);
                    //     cmd.Parameters.AddWithValue("@ItemId", selectedItem.Id);
                    //     cmd.Parameters.AddWithValue("@Quantity", result.Count);
                    //     cmd.Parameters.AddWithValue("@AvgConfidence", avgConfidence);
                    //     cmd.Parameters.AddWithValue("@ResultJson", resultJson);
                    //     cmd.Parameters.AddWithValue("@CreatedAt", result.Timestamp);

                    //     await cmd.ExecuteNonQueryAsync();
                    // }

                    InMemoryLogService.Instance.LogInfo($"Saved detection to database successfully. ID: {detectionId}");
                    return detectionId;
                }
            }
            catch (Exception ex)
            {
                InMemoryLogService.Instance.LogError($"Error saving detection to database: {ex.Message}", ex);
                return 0;
            }
        }

        public async Task<List<HistoryLog>> GetHistoryLogsAsync()
        {
            var list = new List<HistoryLog>();
            string connString = GetConnectionString();

            if (string.IsNullOrWhiteSpace(connString)) return list;

            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT d.id, m.machine_name, i.item_code, i.item_name, d.total_objects, d.created_at, d.image_path, d.result_image_path, d.status_result
                        FROM detections d
                        JOIN machines m ON d.machine_id = m.id
                        JOIN items i ON d.item_id = i.id
                        WHERE d.machine_id = @MachineId
                        ORDER BY d.created_at DESC";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MachineId", CurrentMachineId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int total = reader.GetInt32(4);
                                list.Add(new HistoryLog
                                {
                                    Id = (int)reader.GetInt64(0),
                                    MachineName = reader.GetString(1),
                                    Action = "Detection",
                                    Status = reader.IsDBNull(8) ? "Pending" : reader.GetString(8),
                                    Message = $"Counted {total} of {reader.GetString(2)} ({reader.GetString(3)})",
                                    CreatedAt = reader.GetDateTime(5),
                                    ItemCode = reader.GetString(2),
                                    ItemName = reader.GetString(3),
                                    TotalObjects = total,
                                    ImagePath = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    ResultImagePath = reader.IsDBNull(7) ? null : reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading history from database: {ex.Message}");
            }
            return list;
        }

        public async Task<(List<HistoryLog> Logs, int TotalCount)> GetHistoryLogsPagedAsync(string? searchText, int pageNumber, int pageSize = 100)
        {
            var list = new List<HistoryLog>();
            int totalCount = 0;
            string connString = GetConnectionString();

            if (string.IsNullOrWhiteSpace(connString)) return (list, 0);

            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();

                    // Xây dựng mệnh đề WHERE - luôn lọc theo machine hiện tại
                    string whereClause = "WHERE d.machine_id = @MachineId";
                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        whereClause += " AND (i.item_code LIKE @SearchPattern OR i.item_name LIKE @SearchPattern OR m.machine_name LIKE @SearchPattern)";
                    }

                    // 1. Đếm tổng số bản ghi khớp bộ lọc
                    string countQuery = $@"
                        SELECT COUNT(*) 
                        FROM detections d
                        JOIN machines m ON d.machine_id = m.id
                        JOIN items i ON d.item_id = i.id
                        {whereClause}";

                    using (var countCmd = new SqlCommand(countQuery, connection))
                    {
                        countCmd.Parameters.AddWithValue("@MachineId", CurrentMachineId);
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            countCmd.Parameters.AddWithValue("@SearchPattern", $"%{searchText.Trim()}%");
                        }
                        totalCount = (int)await countCmd.ExecuteScalarAsync();
                    }

                    // 2. Truy vấn dữ liệu phân trang
                    int offset = (pageNumber - 1) * pageSize;
                    string query = $@"
                        SELECT d.id, m.machine_name, i.item_code, i.item_name, d.total_objects, d.created_at, d.image_path, d.result_image_path, d.status_result
                        FROM detections d
                        JOIN machines m ON d.machine_id = m.id
                        JOIN items i ON d.item_id = i.id
                        {whereClause}
                        ORDER BY d.created_at DESC
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MachineId", CurrentMachineId);
                        cmd.Parameters.AddWithValue("@Offset", offset);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            cmd.Parameters.AddWithValue("@SearchPattern", $"%{searchText.Trim()}%");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int total = reader.GetInt32(4);
                                list.Add(new HistoryLog
                                {
                                    Id = (int)reader.GetInt64(0),
                                    MachineName = reader.GetString(1),
                                    Action = "Detection",
                                    Status = reader.IsDBNull(8) ? "Pending" : reader.GetString(8),
                                    Message = $"Counted {total} of {reader.GetString(2)} ({reader.GetString(3)})",
                                    CreatedAt = reader.GetDateTime(5),
                                    ItemCode = reader.GetString(2),
                                    ItemName = reader.GetString(3),
                                    TotalObjects = total,
                                    ImagePath = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    ResultImagePath = reader.IsDBNull(7) ? null : reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading paged history from database: {ex.Message}");
            }
            return (list, totalCount);
        }

        public async Task<bool> UpdateDetectionStatusAsync(long detectionId, string status)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE detections SET status_result = @Status WHERE id = @Id";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@Id", detectionId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating detection status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cập nhật cả status_result và result_image_path trong một lần truy vấn cơ sở dữ liệu.
        /// Được sử dụng khi đổi tên tệp hình ảnh kết quả sau khi xác nhận pass/fail.
        /// </summary>
        public async Task<bool> UpdateDetectionStatusAndImagePathAsync(long detectionId, string status, string newImagePath)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE detections SET status_result = @Status, result_image_path = @ImagePath WHERE id = @Id";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@ImagePath", newImagePath);
                        cmd.Parameters.AddWithValue("@Id", detectionId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating detection status and image path: {ex.Message}");
                return false;
            }
        }

        private async Task<int> GetOrCreateMachineIdAsync(SqlConnection connection, string hostname)
        {
            string query = "SELECT id FROM machines WHERE hostname = @Hostname";
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Hostname", hostname);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null) return (int)result;
            }

            string ip = GetLocalIpAddress();
            string insertQuery = @"
                INSERT INTO machines (hostname, machine_name, ip, password, status, last_seen, created_at)
                OUTPUT INSERTED.id
                VALUES (@Hostname, @MachineName, @Ip, 'admin', 'Online', GETDATE(), GETDATE())";

            using (var cmd = new SqlCommand(insertQuery, connection))
            {
                cmd.Parameters.AddWithValue("@Hostname", hostname);
                cmd.Parameters.AddWithValue("@MachineName", hostname);
                cmd.Parameters.AddWithValue("@Ip", ip);
                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        private async Task<int> GetOrCreateModelIdAsync(SqlConnection connection, string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath)) modelPath = "MockMode";
            string modelCode = Path.GetFileNameWithoutExtension(modelPath);
            if (string.IsNullOrWhiteSpace(modelCode)) modelCode = "UNKNOWN";

            string query = "SELECT id FROM models WHERE model_path = @ModelPath OR model_code = @ModelCode";
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ModelPath", modelPath);
                cmd.Parameters.AddWithValue("@ModelCode", modelCode);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null) return (int)result;
            }

            string insertQuery = @"
                INSERT INTO models (model_code, model_name, model_path, is_active, description, created_at)
                OUTPUT INSERTED.id
                VALUES (@ModelCode, @ModelName, @ModelPath, 1, 'Auto-registered model', GETDATE())";

            using (var cmd = new SqlCommand(insertQuery, connection))
            {
                cmd.Parameters.AddWithValue("@ModelCode", modelCode);
                cmd.Parameters.AddWithValue("@ModelName", modelCode);
                cmd.Parameters.AddWithValue("@ModelPath", modelPath);
                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        public async Task<List<ModelClass>> GetModelsFromDbAsync()
        {
            var list = new List<ModelClass>();
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return list;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT id, model_code, model_name, model_path, is_active, description, created_at FROM models ORDER BY id";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ModelClass
                                {
                                    Id = reader.GetInt32(0),
                                    ModelCode = reader.GetString(1),
                                    ModelName = reader.GetString(2),
                                    ModelPath = reader.GetString(3),
                                    IsActive = reader.GetBoolean(4),
                                    Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    CreatedAt = reader.GetDateTime(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting models from DB: {ex.Message}");
            }
            return list;
        }

        public async Task<bool> SetActiveModelInDbAsync(int modelId)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string query1 = "UPDATE models SET is_active = 0";
                            using (var cmd = new SqlCommand(query1, connection, transaction))
                            {
                                await cmd.ExecuteNonQueryAsync();
                            }

                            string query2 = "UPDATE models SET is_active = 1 WHERE id = @Id";
                            using (var cmd = new SqlCommand(query2, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", modelId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting active model in DB: {ex.Message}");
                return false;
            }
        }

        public async Task InitializeMachineIdAsync()
        {
            string hostname = Environment.MachineName;
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return;

            using (var connection = new SqlConnection(connString))
            {
                await connection.OpenAsync();
                
                string query = "SELECT id FROM machines WHERE hostname = @Hostname";
                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Hostname", hostname);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        CurrentMachineId = Convert.ToInt32(result);
                        return;
                    }
                }

                string ip = GetLocalIpAddress();
                string insertQuery = @"
                    INSERT INTO machines (hostname, machine_name, ip, password, status, last_seen, created_at)
                    OUTPUT INSERTED.id
                    VALUES (@Hostname, @MachineName, @Ip, 'admin', 'Online', GETDATE(), GETDATE())";

                using (var cmd = new SqlCommand(insertQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@Hostname", hostname);
                    cmd.Parameters.AddWithValue("@MachineName", hostname);
                    cmd.Parameters.AddWithValue("@Ip", ip);
                    CurrentMachineId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // ignored
            }
            return "127.0.0.1";
        }

        public async Task<List<DbClass>> GetClassesAsync()
        {
            var list = new List<DbClass>();
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return list;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT id, class_code, class_name, description FROM classes ORDER BY class_code";
                    using (var cmd = new SqlCommand(query, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbClass
                            {
                                Id = reader.GetInt32(0),
                                ClassCode = reader.GetInt32(1),
                                ClassName = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading classes: {ex.Message}");
            }
            return list;
        }

        public async Task<List<string>> GetExistingItemTypesAsync()
        {
            var list = new List<string>();
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return list;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT DISTINCT item_type FROM items WHERE is_active = 1 ORDER BY item_type";
                    using (var cmd = new SqlCommand(query, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading item types: {ex.Message}");
            }
            return list;
        }

        public async Task<bool> UpdateItemActiveStatusAsync(int itemId, bool isActive)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE items SET is_active = @IsActive WHERE id = @Id";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", itemId);
                        cmd.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating item active status: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddItemAsync(int classId, string itemCode, string itemName, string itemType)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        INSERT INTO items (class_id, item_code, item_name, item_type, is_active)
                        VALUES (@ClassId, @ItemCode, @ItemName, @ItemType, 1)";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@ClassId", classId);
                        cmd.Parameters.AddWithValue("@ItemCode", itemCode.Trim());
                        cmd.Parameters.AddWithValue("@ItemName", itemName.Trim());
                        cmd.Parameters.AddWithValue("@ItemType", itemType.Trim());
                        await cmd.ExecuteNonQueryAsync();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding item: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> VerifyMachinePasswordAsync(string hostname, string password)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    // Kiểm tra máy đã được đăng ký chưa bằng hostname.
                    string checkQuery = "SELECT password FROM machines WHERE hostname = @Hostname";
                    using (var checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Hostname", hostname);
                        var dbPassword = await checkCmd.ExecuteScalarAsync() as string;
                        if (dbPassword != null)
                        {
                            return string.Equals(dbPassword, password);
                        }
                    }

                    //Máy chưa tồn tại. Hãy đăng ký mới với mật khẩu mặc định 'admin'.
                    string ip = GetLocalIpAddress();
                    string insertQuery = @"
                        INSERT INTO machines (hostname, machine_name, ip, password, status, last_seen, created_at)
                        VALUES (@Hostname, @MachineName, @Ip, 'admin', 'Online', GETDATE(), GETDATE())";
                    using (var insertCmd = new SqlCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@Hostname", hostname);
                        insertCmd.Parameters.AddWithValue("@MachineName", hostname);
                        insertCmd.Parameters.AddWithValue("@Ip", ip);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                    
                    // If it was just registered, default password is 'admin'
                    return string.Equals("admin", password);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying machine password: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateMachinePasswordAsync(string hostname, string newPassword)
        {
            string connString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connString)) return false;
            try
            {
                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE machines SET password = @NewPassword WHERE hostname = @Hostname";
                    using (var cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@NewPassword", newPassword);
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        int rows = await cmd.ExecuteNonQueryAsync();
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating machine password: {ex.Message}");
                return false;
            }
        }
    }
}
