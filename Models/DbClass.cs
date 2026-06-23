using System;

namespace Client.Models
{
    public class DbClass
    {
        public int Id { get; set; }
        public int ClassCode { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
