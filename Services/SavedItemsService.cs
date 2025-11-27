using InvoiceApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class SavedItemsService
    {
        private const string FileName = "saved_items.json";

        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string GetPath()
        {
            var folder = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(folder, FileName);
        }

        public async Task<List<SavedItem>> LoadAsync()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                    return new List<SavedItem>();

                using var fs = File.OpenRead(path);
                var list = await JsonSerializer.DeserializeAsync<List<SavedItem>>(fs, _json);
                return list ?? new List<SavedItem>();
            }
            catch
            {
                return new List<SavedItem>();
            }
        }

        public async Task SaveAsync(List<SavedItem> items)
        {
            var path = GetPath();
            using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, items, _json);
        }
    }
}
