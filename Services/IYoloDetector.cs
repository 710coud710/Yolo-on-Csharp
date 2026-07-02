using System;
using System.Collections.Generic;
using Client.Models;

namespace Client.Services
{
    public class LocalDetectionResponse
    {
        public DetectionResult Result { get; set; } = new();
        public byte[]? AnnotatedImageBytes { get; set; }
        public byte[]? UiAnnotatedImageBytes { get; set; }
    }

    public interface IYoloDetector : IDisposable
    {
        void LoadModel(string modelPath);
        LocalDetectionResponse Detect(byte[] imageBytes, float confidenceThreshold = 0.25f, float nmsThreshold = 0.45f, int? targetClassCode = null, double roiX = 0, double roiY = 0, double roiWidth = 100, double roiHeight = 100);
        List<string> GetClassNames();
        string CurrentModelPath { get; }
        bool IsModelLoaded { get; }
    }
}
