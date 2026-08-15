using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Munyu.Models
{
    public class MunyuItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _type = "file"; // "file" | "url" | "text" | "binder"
        private string _content = string.Empty;
        private List<MunyuItem>? _children;
        private DateTime _createdAt = DateTime.UtcNow;
        private bool _isExpanded = false;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("type")]
        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        [JsonPropertyName("content")]
        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(ToolTipText)); }
        }

        [JsonPropertyName("children")]
        public List<MunyuItem>? Children
        {
            get => _children;
            set { _children = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(ChildCount)); }
        }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("isExpanded")]
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        // Non-serialized UI Helper properties
        [JsonIgnore]
        public ImageSource? IconSource { get; set; }

        private bool _isSelected;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public int ChildCount => Children?.Count ?? 0;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (Type == "binder")
                {
                    return $"Binder ({ChildCount})";
                }
                if (Type == "file")
                {
                    if (string.IsNullOrWhiteSpace(Content)) return "File";
                    try
                    {
                        return Path.GetFileName(Content);
                    }
                    catch
                    {
                        return Content;
                    }
                }
                if (Type == "url")
                {
                    return Content;
                }
                if (Type == "text")
                {
                    string singleLine = Content.Replace("\r", "").Replace("\n", " ");
                    return singleLine.Length > 20 ? singleLine.Substring(0, 20) + "..." : singleLine;
                }
                return Content;
            }
        }

        [JsonIgnore]
        public string ToolTipText
        {
            get
            {
                if (Type == "binder")
                {
                    return $"Binder ({ChildCount} items)";
                }
                return Content;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
