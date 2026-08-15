using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Munyu.Models;

namespace Munyu.Services
{
    public static class DataManager
    {
        private const string JsonFileName = "munyu_data.json";

        private static string GetJsonFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, JsonFileName);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public static List<MunyuItem> LoadData()
        {
            try
            {
                string filePath = GetJsonFilePath();
                if (File.Exists(filePath))
                {
                    string jsonStr = File.ReadAllText(filePath);
                    var items = JsonSerializer.Deserialize<List<MunyuItem>>(jsonStr, _jsonOptions);
                    if (items != null)
                    {
                        // Attach icons to loaded items
                        AttachIcons(items);
                        return items;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading munyu_data.json: {ex.Message}");
            }

            return new List<MunyuItem>();
        }

        public static void SaveData(IEnumerable<MunyuItem> items)
        {
            try
            {
                string filePath = GetJsonFilePath();
                string jsonStr = JsonSerializer.Serialize(items, _jsonOptions);
                File.WriteAllText(filePath, jsonStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving munyu_data.json: {ex.Message}");
            }
        }

        private static void AttachIcons(IEnumerable<MunyuItem> items)
        {
            foreach (var item in items)
            {
                item.IconSource = IconService.GetIconForItem(item.Type, item.Content);
                if (item.Children != null && item.Children.Count > 0)
                {
                    AttachIcons(item.Children);
                }
            }
        }
    }
}
