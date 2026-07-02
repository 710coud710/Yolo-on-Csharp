using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Client.Models;

namespace Client.Services
{
    public class YoloDetector : IYoloDetector
    {
        private InferenceSession? _session;
        private List<string> _classNames = new();
        private string _currentModelPath = string.Empty;
        private bool _isModelLoaded = false;
        private readonly object _lock = new();

        public string CurrentModelPath => _currentModelPath;
        public bool IsModelLoaded => _isModelLoaded;

        public YoloDetector()
        {
            // Bắt đầu bằng chế độ giả lập nếu chưa nạp model
            _classNames = new List<string> { "screw", "nut", "washer", "bolt" };
        }

        public void LoadModel(string modelPath)
        {
            lock (_lock)
            {
                try
                {
                    _isModelLoaded = false;
                    _session?.Dispose();
                    _session = null;

                    if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                    {
                        Console.WriteLine($"Model path not found or empty: '{modelPath}'. Running in Mock Mode.");
                        _currentModelPath = "MockMode";
                        _classNames = new List<string> { "screw", "nut", "washer", "bolt" };
                        return;
                    }

                    // Tải model ONNX
                    var options = new SessionOptions();
                    // Có thể cấu hình GPU/DirectML ở đây nếu cần thiết
                    // options.AppendExecutionProvider_CPU();

                    _session = new InferenceSession(modelPath, options);
                    _currentModelPath = modelPath;

                    // Thử trích xuất nhãn từ Metadata của model ONNX
                    _classNames.Clear();
                    if (_session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var namesMetadata))
                    {
                        // YOLOv8 thường lưu metadata dưới dạng JSON dictionary: {0: 'screw', 1: 'nut'}
                        try
                        {
                            var cleanJson = namesMetadata.Replace("'", "\"").Replace("{", "").Replace("}", "");
                            var parts = cleanJson.Split(',');
                            var nameDict = new SortedDictionary<int, string>();
                            foreach (var part in parts)
                            {
                                var kv = part.Split(':');
                                if (kv.Length == 2 && int.TryParse(kv[0].Trim(), out int id))
                                {
                                    var name = kv[1].Trim().Trim('"').Trim();
                                    nameDict[id] = name;
                                }
                            }
                            _classNames = nameDict.Values.ToList();
                        }
                        catch
                        {
                            // Fallback nếu parse JSON thất bại
                        }
                    }

                    // Nếu không đọc được metadata, đặt danh sách nhãn mặc định
                    if (_classNames.Count == 0)
                    {
                        _classNames = new List<string> { "object" };
                    }

                    _isModelLoaded = true;
                    Console.WriteLine($"ONNX Model loaded successfully from {modelPath}. Detected {_classNames.Count} classes.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading ONNX model: {ex.Message}. Falling back to Mock Mode.");
                    _session = null;
                    _currentModelPath = "MockMode";
                    _classNames = new List<string> { "screw", "nut", "washer", "bolt" };
                }
            }
        }

        public LocalDetectionResponse Detect(byte[] imageBytes, float confidenceThreshold = 0.25f, float nmsThreshold = 0.45f, int? targetClassCode = null, double roiX = 0, double roiY = 0, double roiWidth = 100, double roiHeight = 100)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = new LocalDetectionResponse();

            if (imageBytes == null || imageBytes.Length == 0)
            {
                response.Result.IsSuccess = false;
                response.Result.ErrorMessage = "Empty image data";
                return response;
            }

            try
            {
                // Giải mã ảnh thô bằng OpenCVSharp
                using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (src.Empty())
                {
                    response.Result.IsSuccess = false;
                    response.Result.ErrorMessage = "Failed to decode image";
                    return response;
                }

                // Tính toán pixel coordinates cho ROI
                int rx = (int)(src.Width * roiX / 100.0);
                int ry = (int)(src.Height * roiY / 100.0);
                int rw = (int)(src.Width * roiWidth / 100.0);
                int rh = (int)(src.Height * roiHeight / 100.0);

                // Bảo đảm giới hạn của ảnh
                rx = Math.Clamp(rx, 0, src.Width - 1);
                ry = Math.Clamp(ry, 0, src.Height - 1);
                rw = Math.Clamp(rw, 1, src.Width - rx);
                rh = Math.Clamp(rh, 1, src.Height - ry);

                bool isRoiEnabled = rw < src.Width || rh < src.Height || rx > 0 || ry > 0;
                List<DetectedObject> detectedObjects;

                if (isRoiEnabled)
                {
                    // Crop vùng ROI để detect
                    var roiRect = new Rect(rx, ry, rw, rh);
                    using var roiMat = new Mat(src, roiRect);

                    lock (_lock)
                    {
                        if (_session == null || _currentModelPath == "MockMode")
                        {
                            detectedObjects = GenerateMockDetections(roiMat.Width, roiMat.Height, targetClassCode);
                            System.Threading.Thread.Sleep(50);
                        }
                        else
                        {
                            detectedObjects = RunOnnxInference(roiMat, confidenceThreshold, nmsThreshold);
                        }
                    }

                    // Dịch chuyển toạ độ các object được detect về toạ độ của ảnh gốc
                    foreach (var obj in detectedObjects)
                    {
                        obj.X += rx;
                        obj.Y += ry;
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        if (_session == null || _currentModelPath == "MockMode")
                        {
                            detectedObjects = GenerateMockDetections(src.Width, src.Height, targetClassCode);
                            System.Threading.Thread.Sleep(50);
                        }
                        else
                        {
                            detectedObjects = RunOnnxInference(src, confidenceThreshold, nmsThreshold);
                        }
                    }
                }

                if (targetClassCode.HasValue)
                {
                    detectedObjects = detectedObjects.Where(o => o.ClassId == targetClassCode.Value).ToList();
                }

                // 1. Vẽ Bounding Boxes của objects lên ảnh
                DrawDetections(src, detectedObjects);

                // 2. Encode sang byte[] JPEG cho lưu trữ (không có khung ROI)
                Cv2.ImEncode(".jpg", src, out byte[] outputBytes);

                // 3. Vẽ khung ROI (nếu được kích hoạt) lên cùng ảnh để hiển thị lên UI
                if (isRoiEnabled)
                {
                    var roiRect = new Rect(rx, ry, rw, rh);
                    Cv2.Rectangle(src, roiRect, new Scalar(0, 165, 255), 2, LineTypes.Link8); // Màu Orange
                    Cv2.PutText(src, "ROI", new Point(rx + 5, ry + 20), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 165, 255), 2);
                }

                // 4. Encode sang byte[] JPEG cho giao diện
                Cv2.ImEncode(".jpg", src, out byte[] uiOutputBytes);

                stopwatch.Stop();

                response.Result.IsSuccess = true;
                response.Result.Count = detectedObjects.Count;
                response.Result.DetectedObjects = detectedObjects;
                response.Result.Timestamp = DateTime.Now;
                response.Result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Result.ModelPath = _currentModelPath;
                response.AnnotatedImageBytes = outputBytes;
                response.UiAnnotatedImageBytes = uiOutputBytes;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.Result.IsSuccess = false;
                response.Result.ErrorMessage = ex.Message;
                response.Result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return response;
        }

        public List<string> GetClassNames()
        {
            return _classNames.ToList();
        }

        // --- CÁC HÀM XỬ LÝ AI ---

        private List<DetectedObject> RunOnnxInference(Mat src, float confidenceThreshold, float nmsThreshold)
        {
            var results = new List<DetectedObject>();

            // 1. Tiền xử lý ảnh (Resize sang kích thước model yêu cầu, mặc định 640)
            int inputWidth = 1280;
            int inputHeight = 1280;

            if (_session != null)
            {
                string firstInputName = _session.InputMetadata.Keys.First();
                var inputMeta = _session.InputMetadata[firstInputName];
                var dims = inputMeta.Dimensions;
                if (dims != null && dims.Length >= 4)
                {
                    int h = dims[2]; // Đọc chiều cao cấu hình trong file ONNX
                    int w = dims[3]; // Đọc chiều rộng cấu hình trong file ONNX
                    if (h > 0) inputHeight = h;
                    if (w > 0) inputWidth = w;
                }
            }

            using var resized = new Mat();
            Cv2.Resize(src, resized, new Size(inputWidth, inputHeight));

            // 2. Chuyển đổi định dạng và Chuẩn hóa (BGR -> RGB, / 255.0)
            float[] inputData = new float[1 * 3 * inputWidth * inputHeight];
            int channelStride = inputWidth * inputHeight;

            unsafe
            {
                byte* pData = resized.DataPointer;
                for (int y = 0; y < inputHeight; y++)
                {
                    for (int x = 0; x < inputWidth; x++)
                    {
                        int index = (y * inputWidth + x) * 3;
                        // BGR -> RGB
                        float b = pData[index] / 255.0f;
                        float g = pData[index + 1] / 255.0f;
                        float r = pData[index + 2] / 255.0f;

                        int pixelOffset = y * inputWidth + x;
                        inputData[0 * channelStride + pixelOffset] = r; // R
                        inputData[1 * channelStride + pixelOffset] = g; // G
                        inputData[2 * channelStride + pixelOffset] = b; // B
                    }
                }
            }

            // 3. Tạo Tensor đầu vào cho ONNX Runtime
            var inputName = _session!.InputMetadata.Keys.First();
            var inputTensor = new DenseTensor<float>(inputData, new int[] { 1, 3, inputWidth, inputHeight });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

            // 4. Chạy suy luận
            using var outputs = _session.Run(inputs);
            var outputName = _session.OutputMetadata.Keys.First();
            var outputTensor = outputs.First(o => o.Name == outputName).AsTensor<float>();

            // 5. Phân tích kết quả đầu ra
            // Hỗ trợ YOLOv8 đầu ra dạng [1, 4 + num_classes, num_anchors]
            var dimensions = outputTensor.Dimensions.ToArray();
            
            if (dimensions.Length == 3 && dimensions[1] < dimensions[2])
            {
                // YOLOv8 Format
                int rows = dimensions[1]; // 4 + num_classes
                int numClasses = rows - 4;
                int numAnchors = dimensions[2];
                float[] data = outputTensor.ToArray();

                var boxes = new List<Rect>();
                var confidences = new List<float>();
                var classIds = new List<int>();

                // Tỷ lệ scale tọa độ về kích thước ảnh gốc
                float scaleX = (float)src.Width / inputWidth;
                float scaleY = (float)src.Height / inputHeight;

                for (int i = 0; i < numAnchors; i++)
                {
                    // Lấy điểm tin cậy cao nhất của các lớp
                    float maxClassScore = 0f;
                    int maxClassId = -1;

                    for (int c = 0; c < numClasses; c++)
                    {
                        float classScore = data[(4 + c) * numAnchors + i];
                        if (classScore > maxClassScore)
                        {
                            maxClassScore = classScore;
                            maxClassId = c;
                        }
                    }

                    if (maxClassScore >= confidenceThreshold)
                    {
                        // YOLOv8 lưu center_x, center_y, width, height
                        float cx = data[0 * numAnchors + i];
                        float cy = data[1 * numAnchors + i];
                        float w = data[2 * numAnchors + i];
                        float h = data[3 * numAnchors + i];

                        // Đổi sang x_min, y_min, w, h
                        float x = cx - w / 2f;
                        float y = cy - h / 2f;

                        // Scale về kích thước ảnh gốc và làm tròn số nguyên
                        boxes.Add(new Rect((int)(x * scaleX), (int)(y * scaleY), (int)(w * scaleX), (int)(h * scaleY)));
                        confidences.Add(maxClassScore);
                        classIds.Add(maxClassId);
                    }
                }

                // Chạy thuật toán lọc trùng NMS
                CvDnn.NMSBoxes(boxes, confidences, confidenceThreshold, nmsThreshold, out int[] indices);

                foreach (int idx in indices)
                {
                    var rect = boxes[idx];
                    results.Add(new DetectedObject
                    {
                        ClassId = classIds[idx],
                        ClassName = classIds[idx] < _classNames.Count ? _classNames[classIds[idx]] : $"class_{classIds[idx]}",
                        Confidence = confidences[idx],
                        X = (float)rect.X,
                        Y = (float)rect.Y,
                        Width = (float)rect.Width,
                        Height = (float)rect.Height
                    });
                }
            }
            else if (dimensions.Length == 3)
            {
                // YOLOv5/v7 Format: [1, anchors, 5 + num_classes] hoặc tương tự
                int anchors = dimensions[1];
                int cols = dimensions[2];
                int numClasses = cols - 5;
                float[] data = outputTensor.ToArray();

                var boxes = new List<Rect>();
                var confidences = new List<float>();
                var classIds = new List<int>();

                float scaleX = (float)src.Width / inputWidth;
                float scaleY = (float)src.Height / inputHeight;

                for (int i = 0; i < anchors; i++)
                {
                    int offset = i * cols;
                    float objectness = data[offset + 4];

                    if (objectness >= confidenceThreshold)
                    {
                        float maxClassScore = 0f;
                        int maxClassId = -1;

                        for (int c = 0; c < numClasses; c++)
                        {
                            float classScore = data[offset + 5 + c];
                            if (classScore > maxClassScore)
                            {
                                maxClassScore = classScore;
                                maxClassId = c;
                            }
                        }

                        float confidence = objectness * maxClassScore;
                        if (confidence >= confidenceThreshold)
                        {
                            float cx = data[offset + 0];
                            float cy = data[offset + 1];
                            float w = data[offset + 2];
                            float h = data[offset + 3];

                            float x = cx - w / 2f;
                            float y = cy - h / 2f;

                            boxes.Add(new Rect((int)(x * scaleX), (int)(y * scaleY), (int)(w * scaleX), (int)(h * scaleY)));
                            confidences.Add(confidence);
                            classIds.Add(maxClassId);
                        }
                    }
                }

                CvDnn.NMSBoxes(boxes, confidences, confidenceThreshold, nmsThreshold, out int[] indices);

                foreach (int idx in indices)
                {
                    var rect = boxes[idx];
                    results.Add(new DetectedObject
                    {
                        ClassId = classIds[idx],
                        ClassName = classIds[idx] < _classNames.Count ? _classNames[classIds[idx]] : $"class_{classIds[idx]}",
                        Confidence = confidences[idx],
                        X = (float)rect.X,
                        Y = (float)rect.Y,
                        Width = (float)rect.Width,
                        Height = (float)rect.Height
                    });
                }
            }

            return results;
        }

        private List<DetectedObject> GenerateMockDetections(int width, int height, int? targetClassCode = null)
        {
            var list = new List<DetectedObject>();
            var random = new Random();

            // Sinh ngẫu nhiên từ 1 đến 4 lỗi
            int count = random.Next(1, 5);
            for (int i = 0; i < count; i++)
            {
                int classId = targetClassCode ?? random.Next(0, _classNames.Count);
                if (classId >= _classNames.Count) classId = 0;
                float confidence = (float)(random.NextDouble() * 0.4 + 0.55); // 0.55 - 0.95

                // Tạo bounding box ngẫu nhiên trong vùng ảnh
                float w = random.Next(40, 150);
                float h = random.Next(40, 150);
                float x = random.Next(50, width - (int)w - 50);
                float y = random.Next(50, height - (int)h - 50);

                list.Add(new DetectedObject
                {
                    ClassId = classId,
                    ClassName = classId < _classNames.Count ? _classNames[classId] : $"class_{classId}",
                    Confidence = confidence,
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h
                });
            }

            return list;
        }

        private void DrawDetections(Mat src, List<DetectedObject> detectedObjects)
        {
            var colors = new Scalar[]
            {
                new Scalar(0, 0, 255),   // Đỏ
                new Scalar(0, 255, 255), // Vàng
                new Scalar(255, 0, 0),   // Xanh dương
                new Scalar(0, 255, 0)    // Xanh lá
            };

            int count = 1;
            foreach (var obj in detectedObjects)
            {
                var color = colors[obj.ClassId % colors.Length];
                var rect = new Rect((int)obj.X, (int)obj.Y, (int)obj.Width, (int)obj.Height);
                
                // Vẽ hình chữ nhật
                Cv2.Rectangle(src, rect, color, 3);

                // Viết số thứ tự đếm thay vì nhãn lớp và độ tin cậy
                string label = count.ToString();
                count++;

                int baseLine;
                var labelSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 1, out baseLine);
                
                var labelRect = new Rect(rect.X, rect.Y - labelSize.Height - 5, labelSize.Width, labelSize.Height + 5);
                if (labelRect.Y < 0) labelRect.Y = 0;

                Cv2.Rectangle(src, labelRect, color, -1); // Vẽ đè hình chữ nhật đặc làm nền chữ
                Cv2.PutText(src, label, new Point(rect.X, labelRect.Y + labelSize.Height), 
                    HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
