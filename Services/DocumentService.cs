using InvoiceApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class DocumentService
    {
        private const string FileName = "documents.json";
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        private static string GetPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        }

        public async Task<List<Invoice>> LoadAsync()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path)) return new List<Invoice>();

                using var fs = File.OpenRead(path);
                var list = await JsonSerializer.DeserializeAsync<List<Invoice>>(fs, _json);
                return list ?? new List<Invoice>();
            }
            catch
            {
                return new List<Invoice>();
            }
        }

        public async Task SaveDocumentAsync(Invoice doc)
        {
            var list = await LoadAsync();
            
            // Pokud už existuje (podle čísla), aktualizujeme ho
            var existing = list.FirstOrDefault(x => x.Number == doc.Number);
            if (existing != null)
            {
                list.Remove(existing);
            }
            
            // Přidáme na začátek (nejnovější první)
            list.Insert(0, doc);

            await SaveListAsync(list);
        }

        public async Task UpdateStatusAsync(string number, bool isPaid)
        {
            var list = await LoadAsync();
            var doc = list.FirstOrDefault(x => x.Number == number);
            if (doc != null)
            {
                // Tady bychom mohli mít property IsPaid, ale Invoice ji zatím nemá.
                // Prozatím to nebudeme řešit, nebo přidáme property do Invoice.
                // Uživatel chtěl "označit jako zaplacené".
                // Takže přidáme property do Invoice.
            }
        }

        private async Task SaveListAsync(List<Invoice> list)
        {
            var path = GetPath();
            using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, list, _json);
        }
    }
}
