using InvoiceApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

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
                    MessageBox.Show($"Chybí datový soubor '{FileName}'. Ujistěte se, že je v projektu a má ve vlastnostech nastaveno 'Copy if newer'.", "Chyba souboru", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new List<Bank>();
                }

                // Přečteme celý obsah souboru
                string jsonContent = File.ReadAllText(filePath);
                // Převedeme JSON na seznam objektů Bank
                var banks = JsonSerializer.Deserialize<List<Bank>>(jsonContent);

                _cachedBanks = banks ?? new List<Bank>();
                return _cachedBanks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nepodařilo se načíst nebo zpracovat seznam bank ze souboru '{FileName}'.\nChyba: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Bank>();
            }
        }
    }
}