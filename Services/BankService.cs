using InvoiceApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InvoiceApp.Services
{
    public class BankService
    {
        private static List<Bank>? _cachedBanks;
        private const string FileName = "banks.json";

        public List<Bank> GetBanks()
        {
            // Pokud už máme banky v paměti z dřívějška, hned je vrátíme
            if (_cachedBanks != null)
            {
                return _cachedBanks;
            }

            try
            {
                // Najdeme cestu k našemu JSON souboru
                string filePath = Path.Combine(AppContext.BaseDirectory, FileName);

                if (!File.Exists(filePath))
                {
                    // Ve service vrstvě neukazujeme MessageBox – vrátíme prázdný seznam a necháme UI rozhodnout
                    _cachedBanks = new List<Bank>();
                    return _cachedBanks;
                }

                // Přečteme celý obsah souboru
                string jsonContent = File.ReadAllText(filePath);
                // Převedeme JSON na seznam objektů Bank
                var banks = JsonSerializer.Deserialize<List<Bank>>(jsonContent);

                _cachedBanks = banks ?? new List<Bank>();
                return _cachedBanks;
            }
            catch
            {
                // Bez UI side-effectů; případné logování řeš jinde
                _cachedBanks = new List<Bank>();
                return _cachedBanks;
            }
        }
    }
}
