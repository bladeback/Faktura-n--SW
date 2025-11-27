using InvoiceApp.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class ConfigService
    {
        private const string FileName = "config.json";
        private readonly string _filePath;

        public ConfigService()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(dir, FileName);
        }

        public async Task<AppConfig> LoadAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null) return config;
                }
            }
            catch { /* ignore */ }

            return new AppConfig(); // Return default
        }

        public async Task SaveAsync(AppConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch { /* ignore */ }
        }
    }
}
