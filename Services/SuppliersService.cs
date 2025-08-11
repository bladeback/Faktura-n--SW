using InvoiceApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class SuppliersService
    {
        private const string FileName = "suppliers.json";

        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string GetPath()
            => Path.Combine(AppContext.BaseDirectory, FileName);

        public async Task<List<Company>> LoadAsync()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                    return new List<Company>();

                using var fs = File.OpenRead(path);
                var list = await JsonSerializer.DeserializeAsync<List<Company>>(fs, _json);
                return list ?? new List<Company>();
            }
            catch
            {
                return new List<Company>();
            }
        }

        public async Task SaveAsync(List<Company> suppliers)
        {
            var path = GetPath();
            using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, suppliers, _json);
        }
    }
}
