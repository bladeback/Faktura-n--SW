using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InvoiceApp.Services
{
    /// <summary>
    /// Číslování: yyyy + pořadí 000001–999999 (10 číslic).
    /// – oddělené čítače pro FA a OBJ,
    /// – perzistence v rootu aplikace (portable),
    /// – dvoufázově: Reserve…() jen rezervuje číslo, Commit…() potvrdí (trvalé navýšení).
    /// </summary>
    public class InvoiceNumberService
    {
        private const string FileName = "invoice_counter.json";

        private readonly string _filePath;
        private Counters _counters = new();
        private readonly object _sync = new();

        // Drží „rezervované“ číslo, dokud neproběhne Commit
        private string? _reservedFa;
        private string? _reservedObj;

        public static readonly InvoiceNumberService Instance = new();

        private InvoiceNumberService()
        {
            // Portable: Ukládáme vedle .exe
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            _filePath = Path.Combine(dir, FileName);
            _counters = Load();
        }

        // --------- VEŘEJNÉ API ----------

        // Upraveno pro podporu konkrétního roku (volitelně)
        public string ReserveInvoiceNumber(int? year = null)
        {
            lock (_sync)
            {
                var targetYear = year ?? DateTime.Now.Year;
                _reservedFa = BuildNext("FA", targetYear);
                return _reservedFa;
            }
        }

        public void CommitInvoice()
        {
            lock (_sync)
            {
                if (_reservedFa == null) return;
                Commit("FA", _reservedFa);
                _reservedFa = null;
            }
        }

        public string ReserveOrderNumber(int? year = null)
        {
            lock (_sync)
            {
                var targetYear = year ?? DateTime.Now.Year;
                _reservedObj = BuildNext("OBJ", targetYear);
                return _reservedObj;
            }
        }

        public void CommitOrder()
        {
            lock (_sync)
            {
                if (_reservedObj == null) return;
                Commit("OBJ", _reservedObj);
                _reservedObj = null;
            }
        }

        // ZPĚTNÁ KOMPATIBILITA
        public string NextInvoiceNumber() => ReserveInvoiceNumber();
        public string NextOrderNumber() => ReserveOrderNumber();

        // --------- METODY PRO NASTAVENÍ (Settings) ----------

        public int GetNextInvoiceNumber(string year)
        {
            lock (_sync)
            {
                if (_counters.Years.TryGetValue(year, out var y))
                    return y.FaNext;
                return 1;
            }
        }

        public void SetNextInvoiceNumber(string year, int nextVal)
        {
            lock (_sync)
            {
                if (!_counters.Years.TryGetValue(year, out var y))
                {
                    y = new YearCounters();
                    _counters.Years[year] = y;
                }
                y.FaNext = nextVal;
                Save(_counters);
            }
        }

        public int GetNextOrderNumber(string year)
        {
            lock (_sync)
            {
                if (_counters.Years.TryGetValue(year, out var y))
                    return y.ObjNext;
                return 1;
            }
        }

        public void SetNextOrderNumber(string year, int nextVal)
        {
            lock (_sync)
            {
                if (!_counters.Years.TryGetValue(year, out var y))
                {
                    y = new YearCounters();
                    _counters.Years[year] = y;
                }
                y.ObjNext = nextVal;
                Save(_counters);
            }
        }

        // --------- INTERNÍ LOGIKA ----------

        private string BuildNext(string kind, int yearVal) 
        {
            var year = yearVal.ToString();

            if (!_counters.Years.TryGetValue(year, out var y))
            {
                y = new YearCounters();
                _counters.Years[year] = y;
            }

            long next = kind == "FA" ? y.FaNext : y.ObjNext;
            if (next < 1) next = 1;
            if (next > 9999) throw new InvalidOperationException($"Dosažen limit 9999 pro {kind} v roce {year}.");

            return $"{year}{next:0000}";
        }

        private void Commit(string kind, string number)
        {
            var year = number.Substring(0, 4);

            if (!_counters.Years.TryGetValue(year, out var y))
            {
                y = new YearCounters();
                _counters.Years[year] = y;
            }

            var seqPart = number.Substring(4); // 6 číslic
            if (!int.TryParse(seqPart, out var used)) used = 0;

            if (kind == "FA")
            {
                if (used >= y.FaNext) y.FaNext = used + 1;
            }
            else
            {
                if (used >= y.ObjNext) y.ObjNext = used + 1;
            }

            Save(_counters);
        }

        // --------- PERSISTENCE ----------

        private Counters Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<Counters>(json);
                    if (data != null) return data;
                }
            }
            catch { /* ignore */ }

            // Pokud soubor neexistuje (první spuštění v novém umístění),
            // inicializujeme výchozí hodnoty pro rok 2025 dle přání uživatele.
            var defaults = new Counters();
            defaults.Years["2025"] = new YearCounters
            {
                FaNext = 20,  // Další bude FA-20250020
                ObjNext = 6   // Další bude OBJ-20250006
            };
            
            // Uložíme, aby se soubor vytvořil
            Save(defaults);

            return defaults;
        }

        private void Save(Counters data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { /* ignore */ }
        }

        // --------- DTO ----------

        private class Counters
        {
            public Dictionary<string, YearCounters> Years { get; set; } = new(); 
        }

        private class YearCounters
        {
            public int FaNext { get; set; } = 1;
            public int ObjNext { get; set; } = 1;
        }
    }
}
