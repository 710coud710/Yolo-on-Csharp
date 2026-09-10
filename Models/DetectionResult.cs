using System;
using System.Collections.Generic;

namespace Client.Models
{
    public class DetectionResult
    {
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ImagePath { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public List<DetectedObject> DetectedObjects { get; set; } = new();
        public double ProcessingTimeMs { get; set; }
        public string ModelPath { get; set; } = string.Empty;
        public string ModelType { get; set; } = "Detect"; // "Detect" | "OBB"
    }

    public class DetectedPoint
    {
        public float X { get; set; }
        public float Y { get; set; }

        public DetectedPoint() { }
        public DetectedPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public class DetectedObject
    {
        public string ClassName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public float Confidence { get; set; }

        // Bounding Box dạng thẳng (AABB - Axis-Aligned Bounding Box)
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        // Các thuộc tính mở rộng cho OBB (Oriented Bounding Box)
        public bool IsObb { get; set; } = false;
        public float Angle { get; set; } = 0f; // Góc xoay theo radian
        public float AngleDegree { get; set; } = 0f; // Góc xoay theo độ
        public float CenterX { get; set; } = 0f;
        public float CenterY { get; set; } = 0f;
        public List<DetectedPoint>? Points { get; set; } // 4 đỉnh theo thứ tự xoay
    }
}
