using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.WpfExtensions;
using Client.Models;

namespace Client.Services
{
    public class YoloDetector : IYoloDetector
    {
        private InferenceSession? _session;
        private List<string> _classNames = new();
        private string _currentModelPath = string.Empty;
        private bool _isModelLoaded = false;
        private YoloModelType _currentModelType = YoloModelType.Detect;
        private readonly object _lock = new();

        public string CurrentModelPath => _currentModelPath;
        public bool IsModelLoaded => _isModelLoaded;
        public YoloModelType CurrentModelType => _currentModelType;

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
                    try
                    {
                        // Thử kích hoạt GPU qua DirectML (tự động fallback về CPU nếu không tương thích)
                        options.AppendExecutionProvider_DML(0);
                        Console.WriteLine("ONNX Runtime: Successfully initialized DirectML (GPU).");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ONNX Runtime: DirectML (GPU) not available. Falling back to CPU. Error: {ex.Message}");
                    }

                    _session = new InferenceSession(modelPath, options);
                    _currentModelPath = modelPath;

                    // Đọc metadata "task" để nhận biết loại model (detect hoặc obb)
                    _currentModelType = YoloModelType.Detect;
                    if (_session.ModelMetadata.CustomMetadataMap.TryGetValue("task", out var taskMetadata))
                    {
                        if (taskMetadata.Contains("obb", StringComparison.OrdinalIgnoreCase))
                        {
                            _currentModelType = YoloModelType.Obb;
                        }
                    }

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

                    // Nếu metadata chưa có task, kiểm tra qua Output Tensor shape
                    if (_currentModelType == YoloModelType.Detect)
                    {
                        try
                        {
                            var firstOutput = _session.OutputMetadata.First();
                            var dims = firstOutput.Value.Dimensions;
                            if (dims != null && dims.Length == 3)
                            {
                                int channels = dims[1];
                                if (channels > 0 && channels == 5 + _classNames.Count)
                                {
                                    _currentModelType = YoloModelType.Obb;
                                }
                            }
                        }
                        catch
                        {
                            // Bỏ qua lỗi đọc output metadata nếu có
                        }
                    }

                    _isModelLoaded = true;
                    Console.WriteLine($"ONNX Model loaded successfully from {modelPath}. Type: {_currentModelType}, Detected {_classNames.Count} classes.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading ONNX model: {ex.Message}. Falling back to Mock Mode.");
                    _session = null;
                    _currentModelPath = "MockMode";
                    _currentModelType = YoloModelType.Detect;
                    _classNames = new List<string> { "screw", "nut", "washer", "bolt" };
                }
            }
        }

        public LocalDetectionResponse Detect(
            byte[] imageBytes, 
            float confidenceThreshold = 0.25f, 
            float nmsThreshold = 0.45f, 
            int? targetClassCode = null, 
            double roiX = 0, 
            double roiY = 0, 
            double roiWidth = 100, 
            double roiHeight = 100,
            bool useLetterbox = true,
            bool enableTiling = false,
            double tilingOverlap = 0.2)
        {
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

                return DetectInternal(
                    src, 
                    confidenceThreshold, 
                    nmsThreshold, 
                    targetClassCode, 
                    roiX, 
                    roiY, 
                    roiWidth, 
                    roiHeight, 
                    useLetterbox, 
                    enableTiling, 
                    tilingOverlap, 
                    generateBitmapSource: false);
            }
            catch (Exception ex)
            {
                response.Result.IsSuccess = false;
                response.Result.ErrorMessage = ex.Message;
                return response;
            }
        }

        public LocalDetectionResponse Detect(
            BitmapSource bitmapSource, 
            float confidenceThreshold = 0.25f, 
            float nmsThreshold = 0.45f, 
            int? targetClassCode = null, 
            double roiX = 0, 
            double roiY = 0, 
            double roiWidth = 100, 
            double roiHeight = 100,
            bool useLetterbox = true,
            bool enableTiling = false,
            double tilingOverlap = 0.2)
        {
            var response = new LocalDetectionResponse();

            if (bitmapSource == null)
            {
                response.Result.IsSuccess = false;
                response.Result.ErrorMessage = "Empty bitmap source data";
                return response;
            }

            try
            {
                // Chuyển đổi trực tiếp BitmapSource sang OpenCV Mat
                using var src = BitmapSourceConverter.ToMat(bitmapSource);
                if (src.Empty())
                {
                    response.Result.IsSuccess = false;
                    response.Result.ErrorMessage = "Failed to convert BitmapSource to Mat";
                    return response;
                }

                // Bảo đảm hệ màu BGR 3 kênh để đưa vào mô hình YOLO
                using var bgrMat = new Mat();
                if (src.Channels() == 4)
                {
                    Cv2.CvtColor(src, bgrMat, ColorConversionCodes.BGRA2BGR);
                }
                else if (src.Channels() == 1)
                {
                    Cv2.CvtColor(src, bgrMat, ColorConversionCodes.GRAY2BGR);
                }
                else
                {
                    src.CopyTo(bgrMat);
                }

                return DetectInternal(
                    bgrMat, 
                    confidenceThreshold, 
                    nmsThreshold, 
                    targetClassCode, 
                    roiX, 
                    roiY, 
                    roiWidth, 
                    roiHeight, 
                    useLetterbox, 
                    enableTiling, 
                    tilingOverlap, 
                    generateBitmapSource: true);
            }
            catch (Exception ex)
            {
                response.Result.IsSuccess = false;
                response.Result.ErrorMessage = ex.Message;
                return response;
            }
        }

        private LocalDetectionResponse DetectInternal(
            Mat src, 
            float confidenceThreshold, 
            float nmsThreshold, 
            int? targetClassCode, 
            double roiX, 
            double roiY, 
            double roiWidth, 
            double roiHeight,
            bool useLetterbox,
            bool enableTiling,
            double tilingOverlap,
            bool generateBitmapSource)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = new LocalDetectionResponse();

            try
            {
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
                            detectedObjects = RunDetection(roiMat, confidenceThreshold, nmsThreshold, useLetterbox, enableTiling, tilingOverlap);
                        }
                    }

                    // Dịch chuyển toạ độ các object được detect về toạ độ của ảnh gốc
                    foreach (var obj in detectedObjects)
                    {
                        obj.X += rx;
                        obj.Y += ry;
                        if (obj.IsObb)
                        {
                            obj.CenterX += rx;
                            obj.CenterY += ry;
                            if (obj.Points != null)
                            {
                                foreach (var pt in obj.Points)
                                {
                                    pt.X += rx;
                                    pt.Y += ry;
                                }
                            }
                        }
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
                            detectedObjects = RunDetection(src, confidenceThreshold, nmsThreshold, useLetterbox, enableTiling, tilingOverlap);
                        }
                    }
                }

                if (targetClassCode.HasValue)
                {
                    detectedObjects = detectedObjects.Where(o => o.ClassId == targetClassCode.Value).ToList();
                }

                // 1. Vẽ Bounding Boxes của objects lên ảnh
                DrawDetections(src, detectedObjects);

                // 2. Encode sang byte[] JPEG cho lưu trữ (không có khung ROI) - Chỉ làm khi không chạy chế độ tối ưu cho stream
                if (!generateBitmapSource)
                {
                    Cv2.ImEncode(".jpg", src, out byte[] outputBytes);
                    response.AnnotatedImageBytes = outputBytes;
                }

                // 3. Vẽ khung ROI (nếu được kích hoạt) lên cùng ảnh để hiển thị lên UI
                if (isRoiEnabled)
                {
                    var roiRect = new Rect(rx, ry, rw, rh);
                    Cv2.Rectangle(src, roiRect, new Scalar(0, 165, 255), 2, LineTypes.Link8); // Màu Orange
                    Cv2.PutText(src, "ROI", new Point(rx + 5, ry + 20), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 165, 255), 2);
                }

                if (generateBitmapSource)
                {
                    // Chuyển đổi trực tiếp Mat -> BitmapSource (không nén JPEG)
                    var uiBitmap = BitmapSourceConverter.ToBitmapSource(src);
                    uiBitmap.Freeze();
                    response.UiAnnotatedBitmapSource = uiBitmap;
                }
                else
                {
                    // 4. Encode sang byte[] JPEG cho giao diện thông thường
                    Cv2.ImEncode(".jpg", src, out byte[] uiOutputBytes);
                    response.UiAnnotatedImageBytes = uiOutputBytes;
                }

                stopwatch.Stop();

                response.Result.IsSuccess = true;
                response.Result.Count = detectedObjects.Count;
                response.Result.DetectedObjects = detectedObjects;
                response.Result.Timestamp = DateTime.Now;
                response.Result.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                response.Result.ModelPath = _currentModelPath;
                response.Result.ModelType = _currentModelType == YoloModelType.Obb ? "OBB" : "Detect";
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

        private List<DetectedObject> RunDetection(Mat img, float confidenceThreshold, float nmsThreshold, bool useLetterbox, bool enableTiling, double tilingOverlap)
        {
            if (enableTiling)
            {
                return RunTilingInference(img, confidenceThreshold, nmsThreshold, useLetterbox, tilingOverlap);
            }
            else
            {
                return RunOnnxInference(img, confidenceThreshold, nmsThreshold, useLetterbox);
            }
        }

        private List<DetectedObject> RunTilingInference(Mat src, float confidenceThreshold, float nmsThreshold, bool useLetterbox, double tilingOverlap)
        {
            int inputWidth = 1280;
            int inputHeight = 1280;

            if (_session != null)
            {
                string firstInputName = _session.InputMetadata.Keys.First();
                var inputMeta = _session.InputMetadata[firstInputName];
                var dims = inputMeta.Dimensions;
                if (dims != null && dims.Length >= 4)
                {
                    int h = dims[2];
                    int w = dims[3];
                    if (h > 0) inputHeight = h;
                    if (w > 0) inputWidth = w;
                }
            }

            // Nếu kích thước ảnh nhỏ hơn kích thước tile, chạy inference bình thường
            if (src.Width <= inputWidth && src.Height <= inputHeight)
            {
                return RunOnnxInference(src, confidenceThreshold, nmsThreshold, useLetterbox);
            }

            // Tính bước nhảy (stride) dựa trên overlap
            int overlapW = (int)(inputWidth * tilingOverlap);
            int overlapH = (int)(inputHeight * tilingOverlap);
            int strideW = Math.Max(1, inputWidth - overlapW);
            int strideH = Math.Max(1, inputHeight - overlapH);

            var xCoords = new List<int>();
            int cx = 0;
            while (cx + inputWidth < src.Width)
            {
                xCoords.Add(cx);
                cx += strideW;
            }
            xCoords.Add(src.Width - inputWidth);
            xCoords = xCoords.Where(x => x >= 0).Distinct().ToList();

            var yCoords = new List<int>();
            int cy = 0;
            while (cy + inputHeight < src.Height)
            {
                yCoords.Add(cy);
                cy += strideH;
            }
            yCoords.Add(src.Height - inputHeight);
            yCoords = yCoords.Where(y => y >= 0).Distinct().ToList();

            var allObjects = new List<DetectedObject>();

            foreach (int ty in yCoords)
            {
                foreach (int tx in xCoords)
                {
                    var rect = new Rect(tx, ty, inputWidth, inputHeight);
                    using var tileMat = new Mat(src, rect);

                    // Chạy suy luận trên tile
                    var tileDetections = RunOnnxInference(tileMat, confidenceThreshold, nmsThreshold, useLetterbox);

                    // Dịch tọa độ về ảnh gốc
                    foreach (var obj in tileDetections)
                    {
                        obj.X += tx;
                        obj.Y += ty;
                        if (obj.IsObb)
                        {
                            obj.CenterX += tx;
                            obj.CenterY += ty;
                            if (obj.Points != null)
                            {
                                foreach (var pt in obj.Points)
                                {
                                    pt.X += tx;
                                    pt.Y += ty;
                                }
                            }
                        }
                        allObjects.Add(obj);
                    }
                }
            }

            if (allObjects.Count == 0)
            {
                return allObjects;
            }

            // Áp dụng NMS để lọc trùng lặp giữa các tile
            if (allObjects.Any(o => o.IsObb))
            {
                return ApplyRotatedNms(allObjects, nmsThreshold);
            }
            else
            {
                var boxes = allObjects.Select(o => new Rect((int)o.X, (int)o.Y, (int)o.Width, (int)o.Height)).ToList();
                var confidences = allObjects.Select(o => o.Confidence).ToList();
                CvDnn.NMSBoxes(boxes, confidences, confidenceThreshold, nmsThreshold, out int[] indices);

                var results = new List<DetectedObject>();
                foreach (int idx in indices)
                {
                    results.Add(allObjects[idx]);
                }

                return results;
            }
        }

        private List<DetectedObject> RunOnnxInference(Mat src, float confidenceThreshold, float nmsThreshold, bool useLetterbox)
        {
            var results = new List<DetectedObject>();

            // 1. Tiền xử lý ảnh (Resize/Letterbox sang kích thước model yêu cầu, mặc định 1280)
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
            int padW = 0;
            int padH = 0;
            double scale = 1.0;

            if (useLetterbox)
            {
                double r = Math.Min((double)inputWidth / src.Width, (double)inputHeight / src.Height);
                int newW = (int)Math.Round(src.Width * r);
                int newH = (int)Math.Round(src.Height * r);
                padW = (inputWidth - newW) / 2;
                padH = (inputHeight - newH) / 2;

                using var tempResized = new Mat();
                Cv2.Resize(src, tempResized, new Size(newW, newH));
                Cv2.CopyMakeBorder(tempResized, resized, padH, inputHeight - newH - padH, padW, inputWidth - newW - padW, BorderTypes.Constant, new Scalar(114, 114, 114));
                scale = r;
            }
            else
            {
                Cv2.Resize(src, resized, new Size(inputWidth, inputHeight));
            }

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
            var inputTensor = new DenseTensor<float>(inputData, new int[] { 1, 3, inputHeight, inputWidth });
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
                // YOLOv8 / YOLO11 Format [1, channels, num_anchors]
                int rows = dimensions[1];
                bool isObb = _currentModelType == YoloModelType.Obb || (rows == 5 + _classNames.Count);

                if (isObb)
                {
                    results = ParseObbOutput(outputTensor, src, inputWidth, inputHeight, confidenceThreshold, nmsThreshold, useLetterbox, scale, padW, padH);
                }
                else
                {
                    results = ParseDetectV8Output(outputTensor, src, inputWidth, inputHeight, confidenceThreshold, nmsThreshold, useLetterbox, scale, padW, padH);
                }
            }
            else if (dimensions.Length == 3)
            {
                // YOLOv5/v7 Format: [1, anchors, 5 + num_classes]
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

                            float lx = cx - w / 2f;
                            float ty = cy - h / 2f;

                            float x, y, boxW, boxH;
                            if (useLetterbox)
                            {
                                x = (lx - padW) / (float)scale;
                                y = (ty - padH) / (float)scale;
                                boxW = w / (float)scale;
                                boxH = h / (float)scale;
                            }
                            else
                            {
                                x = lx * scaleX;
                                y = ty * scaleY;
                                boxW = w * scaleX;
                                boxH = h * scaleY;
                            }

                            int ix = Math.Clamp((int)x, 0, src.Width - 1);
                            int iy = Math.Clamp((int)y, 0, src.Height - 1);
                            int iw = Math.Clamp((int)boxW, 1, src.Width - ix);
                            int ih = Math.Clamp((int)boxH, 1, src.Height - iy);

                            boxes.Add(new Rect(ix, iy, iw, ih));
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
                        Height = (float)rect.Height,
                        IsObb = false
                    });
                }
            }

            return results;
        }

        private List<DetectedObject> ParseDetectV8Output(
            Tensor<float> outputTensor,
            Mat src,
            int inputWidth,
            int inputHeight,
            float confidenceThreshold,
            float nmsThreshold,
            bool useLetterbox,
            double scale,
            int padW,
            int padH)
        {
            var results = new List<DetectedObject>();
            var dimensions = outputTensor.Dimensions;
            int rows = dimensions[1]; // 4 + num_classes
            int numClasses = rows - 4;
            int numAnchors = dimensions[2];
            float[] data = outputTensor.ToArray();

            var boxes = new List<Rect>();
            var confidences = new List<float>();
            var classIds = new List<int>();

            float scaleX = (float)src.Width / inputWidth;
            float scaleY = (float)src.Height / inputHeight;

            for (int i = 0; i < numAnchors; i++)
            {
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
                    float cx = data[0 * numAnchors + i];
                    float cy = data[1 * numAnchors + i];
                    float w = data[2 * numAnchors + i];
                    float h = data[3 * numAnchors + i];

                    float lx = cx - w / 2f;
                    float ty = cy - h / 2f;

                    float x, y, boxW, boxH;
                    if (useLetterbox)
                    {
                        x = (lx - padW) / (float)scale;
                        y = (ty - padH) / (float)scale;
                        boxW = w / (float)scale;
                        boxH = h / (float)scale;
                    }
                    else
                    {
                        x = lx * scaleX;
                        y = ty * scaleY;
                        boxW = w * scaleX;
                        boxH = h * scaleY;
                    }

                    int ix = Math.Clamp((int)x, 0, src.Width - 1);
                    int iy = Math.Clamp((int)y, 0, src.Height - 1);
                    int iw = Math.Clamp((int)boxW, 1, src.Width - ix);
                    int ih = Math.Clamp((int)boxH, 1, src.Height - iy);

                    boxes.Add(new Rect(ix, iy, iw, ih));
                    confidences.Add(maxClassScore);
                    classIds.Add(maxClassId);
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
                    Height = (float)rect.Height,
                    IsObb = false
                });
            }

            return results;
        }

        private List<DetectedObject> ParseObbOutput(
            Tensor<float> outputTensor,
            Mat src,
            int inputWidth,
            int inputHeight,
            float confidenceThreshold,
            float nmsThreshold,
            bool useLetterbox,
            double scale,
            int padW,
            int padH)
        {
            var dimensions = outputTensor.Dimensions;
            int rows = dimensions[1]; // 5 + num_classes (cx, cy, w, h, cls_0..cls_N, theta)
            int numClasses = rows - 5;
            int numAnchors = dimensions[2];
            float[] data = outputTensor.ToArray();

            var candidateObjects = new List<DetectedObject>();

            float scaleX = (float)src.Width / inputWidth;
            float scaleY = (float)src.Height / inputHeight;

            for (int i = 0; i < numAnchors; i++)
            {
                float maxClassScore = 0f;
                int maxClassId = -1;

                // Các kênh class scores nằm từ index 4 đến 4 + numClasses - 1
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
                    float cx = data[0 * numAnchors + i];
                    float cy = data[1 * numAnchors + i];
                    float w = data[2 * numAnchors + i];
                    float h = data[3 * numAnchors + i];
                    float angleRad = data[(4 + numClasses) * numAnchors + i]; // Góc radian

                    float origCx, origCy, origW, origH;
                    if (useLetterbox)
                    {
                        origCx = (cx - padW) / (float)scale;
                        origCy = (cy - padH) / (float)scale;
                        origW = w / (float)scale;
                        origH = h / (float)scale;
                    }
                    else
                    {
                        origCx = cx * scaleX;
                        origCy = cy * scaleY;
                        origW = w * scaleX;
                        origH = h * scaleY;
                    }

                    // Tính toán 4 đỉnh xoay theo công thức chuẩn Ultralytics OBB
                    float cos = (float)Math.Cos(angleRad);
                    float sin = (float)Math.Sin(angleRad);
                    float dx = cos * (origW / 2f);
                    float dy = sin * (origW / 2f);
                    float ex = -sin * (origH / 2f);
                    float ey = cos * (origH / 2f);

                    var p0 = new DetectedPoint(origCx - dx - ex, origCy - dy - ey);
                    var p1 = new DetectedPoint(origCx + dx - ex, origCy + dy - ey);
                    var p2 = new DetectedPoint(origCx + dx + ex, origCy + dy + ey);
                    var p3 = new DetectedPoint(origCx - dx + ex, origCy - dy + ey);

                    // Tính AABB bao ngoài
                    float minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
                    float minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
                    float maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
                    float maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

                    int ix = Math.Clamp((int)minX, 0, src.Width - 1);
                    int iy = Math.Clamp((int)minY, 0, src.Height - 1);
                    int iw = Math.Clamp((int)(maxX - minX), 1, src.Width - ix);
                    int ih = Math.Clamp((int)(maxY - minY), 1, src.Height - iy);

                    float angleDeg = (float)(angleRad * 180.0 / Math.PI);

                    candidateObjects.Add(new DetectedObject
                    {
                        ClassId = maxClassId,
                        ClassName = maxClassId >= 0 && maxClassId < _classNames.Count ? _classNames[maxClassId] : $"class_{maxClassId}",
                        Confidence = maxClassScore,
                        X = ix,
                        Y = iy,
                        Width = iw,
                        Height = ih,
                        IsObb = true,
                        Angle = angleRad,
                        AngleDegree = angleDeg,
                        CenterX = origCx,
                        CenterY = origCy,
                        Points = new List<DetectedPoint> { p0, p1, p2, p3 }
                    });
                }
            }

            return ApplyRotatedNms(candidateObjects, nmsThreshold);
        }

        private List<DetectedObject> ApplyRotatedNms(List<DetectedObject> candidates, float nmsThreshold)
        {
            if (candidates.Count <= 1)
                return candidates;

            var sorted = candidates.OrderByDescending(o => o.Confidence).ToList();
            var results = new List<DetectedObject>();
            var suppressed = new bool[sorted.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (suppressed[i])
                    continue;

                var current = sorted[i];
                results.Add(current);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (suppressed[j])
                        continue;

                    var other = sorted[j];

                    // Class-aware NMS: cùng lớp mới triệt tiêu lẫn nhau
                    if (current.ClassId != other.ClassId)
                        continue;

                    float iou = ComputeRotatedIoU(current, other);
                    if (iou >= nmsThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }

            return results;
        }

        private float ComputeRotatedIoU(DetectedObject boxA, DetectedObject boxB)
        {
            try
            {
                if (boxA.Points == null || boxA.Points.Count < 4 || boxB.Points == null || boxB.Points.Count < 4)
                {
                    return ComputeAabbIoU(boxA, boxB);
                }

                float wA = (float)Math.Sqrt(Math.Pow(boxA.Points[1].X - boxA.Points[0].X, 2) + Math.Pow(boxA.Points[1].Y - boxA.Points[0].Y, 2));
                float hA = (float)Math.Sqrt(Math.Pow(boxA.Points[2].X - boxA.Points[1].X, 2) + Math.Pow(boxA.Points[2].Y - boxA.Points[1].Y, 2));
                if (wA <= 0f || hA <= 0f) return ComputeAabbIoU(boxA, boxB);

                float wB = (float)Math.Sqrt(Math.Pow(boxB.Points[1].X - boxB.Points[0].X, 2) + Math.Pow(boxB.Points[1].Y - boxB.Points[0].Y, 2));
                float hB = (float)Math.Sqrt(Math.Pow(boxB.Points[2].X - boxB.Points[1].X, 2) + Math.Pow(boxB.Points[2].Y - boxB.Points[1].Y, 2));
                if (wB <= 0f || hB <= 0f) return ComputeAabbIoU(boxA, boxB);

                var rectA = new RotatedRect(new Point2f(boxA.CenterX, boxA.CenterY), new Size2f(wA, hA), boxA.AngleDegree);
                var rectB = new RotatedRect(new Point2f(boxB.CenterX, boxB.CenterY), new Size2f(wB, hB), boxB.AngleDegree);

                var intersectType = Cv2.RotatedRectangleIntersection(rectA, rectB, out Point2f[] vertices);
                if ((int)intersectType != 0 && vertices != null && vertices.Length >= 3)
                {
                    double interArea = Cv2.ContourArea(vertices);
                    double areaA = wA * hA;
                    double areaB = wB * hB;
                    double unionArea = areaA + areaB - interArea;

                    if (unionArea > 0)
                    {
                        return (float)(interArea / unionArea);
                    }
                }
            }
            catch
            {
                return ComputeAabbIoU(boxA, boxB);
            }

            return 0f;
        }

        private float ComputeAabbIoU(DetectedObject boxA, DetectedObject boxB)
        {
            float x1 = Math.Max(boxA.X, boxB.X);
            float y1 = Math.Max(boxA.Y, boxB.Y);
            float x2 = Math.Min(boxA.X + boxA.Width, boxB.X + boxB.Width);
            float y2 = Math.Min(boxA.Y + boxA.Height, boxB.Y + boxB.Height);

            float w = Math.Max(0f, x2 - x1);
            float h = Math.Max(0f, y2 - y1);
            float interArea = w * h;

            float areaA = boxA.Width * boxA.Height;
            float areaB = boxB.Width * boxB.Height;
            float unionArea = areaA + areaB - interArea;

            return unionArea > 0 ? (interArea / unionArea) : 0f;
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

                float w = random.Next(40, 150);
                float h = random.Next(40, 150);
                float x = random.Next(50, Math.Max(60, width - (int)w - 50));
                float y = random.Next(50, Math.Max(60, height - (int)h - 50));

                if (_currentModelType == YoloModelType.Obb)
                {
                    float cx = x + w / 2f;
                    float cy = y + h / 2f;
                    float angleRad = (float)(random.NextDouble() * Math.PI - Math.PI / 4.0); // [-pi/4, 3pi/4]
                    float cos = (float)Math.Cos(angleRad);
                    float sin = (float)Math.Sin(angleRad);
                    float dx = cos * (w / 2f);
                    float dy = sin * (w / 2f);
                    float ex = -sin * (h / 2f);
                    float ey = cos * (h / 2f);

                    var p0 = new DetectedPoint(cx - dx - ex, cy - dy - ey);
                    var p1 = new DetectedPoint(cx + dx - ex, cy + dy - ey);
                    var p2 = new DetectedPoint(cx + dx + ex, cy + dy + ey);
                    var p3 = new DetectedPoint(cx - dx + ex, cy - dy + ey);

                    list.Add(new DetectedObject
                    {
                        ClassId = classId,
                        ClassName = classId < _classNames.Count ? _classNames[classId] : $"class_{classId}",
                        Confidence = confidence,
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h,
                        IsObb = true,
                        Angle = angleRad,
                        AngleDegree = (float)(angleRad * 180.0 / Math.PI),
                        CenterX = cx,
                        CenterY = cy,
                        Points = new List<DetectedPoint> { p0, p1, p2, p3 }
                    });
                }
                else
                {
                    list.Add(new DetectedObject
                    {
                        ClassId = classId,
                        ClassName = classId < _classNames.Count ? _classNames[classId] : $"class_{classId}",
                        Confidence = confidence,
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h,
                        IsObb = false
                    });
                }
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
                string label = count.ToString();
                count++;

                int baseLine;
                var labelSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 1, out baseLine);

                if (obj.IsObb && obj.Points != null && obj.Points.Count == 4)
                {
                    // 1. Vẽ đa giác OBB bằng 4 đỉnh xoay
                    var pts = obj.Points.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
                    Cv2.Polylines(src, new[] { pts }, isClosed: true, color: color, thickness: 3, lineType: LineTypes.AntiAlias);

                    // Vẽ chấm tròn nhỏ tại tâm
                    Cv2.Circle(src, new Point((int)Math.Round(obj.CenterX), (int)Math.Round(obj.CenterY)), 4, color, -1);

                    // 2. Đặt nhãn số thứ tự ở vị trí đỉnh trên cùng (Y nhỏ nhất)
                    var topPoint = pts.OrderBy(p => p.Y).First();
                    int labelX = Math.Clamp(topPoint.X - labelSize.Width / 2, 0, Math.Max(0, src.Width - labelSize.Width - 1));
                    int labelY = topPoint.Y - 5;
                    if (labelY - labelSize.Height < 0) labelY = labelSize.Height + 5;

                    var labelRect = new Rect(labelX, labelY - labelSize.Height - 5, labelSize.Width, labelSize.Height + 5);
                    Cv2.Rectangle(src, labelRect, color, -1);
                    Cv2.PutText(src, label, new Point(labelX, labelY),
                        HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
                }
                else
                {
                    var rect = new Rect((int)obj.X, (int)obj.Y, (int)obj.Width, (int)obj.Height);

                    // Vẽ hình chữ nhật
                    Cv2.Rectangle(src, rect, color, 3);

                    var labelRect = new Rect(rect.X, rect.Y - labelSize.Height - 5, labelSize.Width, labelSize.Height + 5);
                    if (labelRect.Y < 0) labelRect.Y = 0;

                    Cv2.Rectangle(src, labelRect, color, -1); // Vẽ đè hình chữ nhật đặc làm nền chữ
                    Cv2.PutText(src, label, new Point(rect.X, labelRect.Y + labelSize.Height), 
                        HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
                }
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
