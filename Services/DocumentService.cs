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
        private static readonly System.Threading.SemaphoreSlim _lock = new(1, 1);

        private static string GetPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        }

        public async Task<List<Invoice>> LoadAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await LoadInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<List<Invoice>> LoadInternalAsync()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var path = GetPath();
                    if (!File.Exists(path)) return new List<Invoice>();

                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var list = await JsonSerializer.DeserializeAsync<List<Invoice>>(fs, _json);
                    return list ?? new List<Invoice>();
                }
                catch (IOException)
                {
                    await Task.Delay(100);
                }
                catch
                {
                    return new List<Invoice>();
                }
            }
            return new List<Invoice>();
        }

        public async Task SaveDocumentAsync(Invoice doc)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await LoadInternalAsync();
                
                var existing = list.FirstOrDefault(x => x.Number == doc.Number);
                if (existing != null)
                {
                    list.Remove(existing);
                }
                
                list.Insert(0, doc);

                await SaveListInternalAsync(list);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UpdateStatusAsync(string number, bool isPaid)
        {
            await _lock.WaitAsync();
            try
            {
                var list = await LoadInternalAsync();
                var doc = list.FirstOrDefault(x => x.Number == number);
                if (doc != null)
                {
                    doc.IsPaid = isPaid;
                    await SaveListInternalAsync(list);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task SaveListInternalAsync(List<Invoice> list)
        {
            var path = GetPath();
            using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, list, _json);
        }
    }
}
