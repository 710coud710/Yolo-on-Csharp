using System;

namespace Client.Models
{
    public class DbItem
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int ClassCode { get; set; }
        public string ClassName { get; set; } = string.Empty;

        // public string DisplayName => $"{ItemCode} - {ItemName} ({ClassName})";
        public string DisplayName => $"{ItemCode}-{ItemName}";
    }
}
