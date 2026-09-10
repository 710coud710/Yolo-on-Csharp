using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using Client.Models;

namespace Client.Services
{
    public enum YoloModelType
    {
        Auto,
        Detect,
        Obb
    }

    public class LocalDetectionResponse
    {
        public DetectionResult Result { get; set; } = new();
        public byte[]? AnnotatedImageBytes { get; set; }
        public byte[]? UiAnnotatedImageBytes { get; set; }
        public BitmapSource? UiAnnotatedBitmapSource { get; set; }
    }

    public interface IYoloDetector : IDisposable
    {
        void LoadModel(string modelPath);
        YoloModelType CurrentModelType { get; }
        LocalDetectionResponse Detect(
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
            double tilingOverlap = 0.2);
        
        LocalDetectionResponse Detect(
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
            double tilingOverlap = 0.2);

        List<string> GetClassNames();
        string CurrentModelPath { get; }
        bool IsModelLoaded { get; }
    }
}
