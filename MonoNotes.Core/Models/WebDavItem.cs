using System;

namespace MonoNotes.Core.Models
{
    public class WebDavItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public DateTimeOffset LastModified { get; set; }
    }
}