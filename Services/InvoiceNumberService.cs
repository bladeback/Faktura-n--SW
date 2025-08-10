using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InvoiceApp.Services
{
    /// <summary>
    /// Číslování: yyyy + pořadí 000001–999999 (10 číslic).
    /// – oddělené čítače pro FA a OBJ,
    /// – perzistence v %AppData%\InvoiceApp\invoice_counter.json,
    /// – dvoufázově: Reserve…() jen rezervuje číslo, Commit…() potvrdí (trvalé navýšení).
    /// </summary>
    public class InvoiceNumberService
    {
        private const string AppFolderName = "InvoiceApp";
        private const string FileName = "invoice_counter.json";

        private readonly string _filePath;
        private Counters _counters = new();
        private readonly object _sync = new();

        // Drží „rezervované“ číslo, dokud neproběhne Commit
        private string? _reservedFa;
        private string? _reservedObj;

        public InvoiceNumberService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, AppFolderName);
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, FileName);
            _counters = Load();
        }

        // --------- VEŘEJNÉ API ----------

        // Vrátí rezervované číslo FA (bez prefixu). Netrvale – dokud neproběhne CommitInvoice().
        public string ReserveInvoiceNumber()
        {
            lock (_sync)
            {
                _reservedFa = BuildNext("FA");
                return _reservedFa;
            }
        }

        // Trvale potvrdí (uloží) poslední rezervované číslo FA.
        public void CommitInvoice()
        {
            lock (_sync)
            {
                if (_reservedFa == null) return;
                Commit("FA", _reservedFa);
                _reservedFa = null;
            }
        }

        // Vrátí rezervované číslo OBJ (bez prefixu). Netrvale – dokud neproběhne CommitOrder().
        public string ReserveOrderNumber()
        {
            lock (_sync)
            {
                _reservedObj = BuildNext("OBJ");
                return _reservedObj;
            }
        }

        // Trvale potvrdí (uloží) poslední rezervované číslo OBJ.
        public void CommitOrder()
        {
            lock (_sync)
            {
                if (_reservedObj == null) return;
                Commit("OBJ", _reservedObj);
                _reservedObj = null;
            }
        }

        // ZPĚTNÁ KOMPATIBILITA: pokud někde stále voláš „Next…Number()“, deleguji na Reserve…()
        public string NextInvoiceNumber() => ReserveInvoiceNumber();
        public string NextOrderNumber() => ReserveOrderNumber();

        // --------- INTERNÍ LOGIKA ----------

        private string BuildNext(string kind) // kind = "FA" / "OBJ"
        {
            var year = DateTime.Now.ToString("yyyy");

            if (!_counters.Years.TryGetValue(year, out var y))
            {
                y = new YearCounters();
                _counters.Years[year] = y;
            }

            long next = kind == "FA" ? y.FaNext : y.ObjNext;
            if (next < 1) next = 1;                 // začínáme od 000001
            if (next > 999999) throw new InvalidOperationException($"Dosažen limit 999999 pro {kind} v roce {year}.");

            // Vracíme neprefixované číslo yyyy + 6 číslic
            return $"{year}{next:0000}";
        }

        private void Commit(string kind, string number)
        {
            // number je ve formátu yyyy###### (10 číslic)
            var year = number.Substring(0, 4);

            if (!_counters.Years.TryGetValue(year, out var y))
            {
                y = new YearCounters();
                _counters.Years[year] = y;
            }

            // z čísla si vytáhneme užité pořadí a posuneme next na další
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
            return new Counters();
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
            public Dictionary<string, YearCounters> Years { get; set; } = new(); // key = "2025"
        }

        private class YearCounters
        {
            public int FaNext { get; set; } = 1;   // další pořadí pro FA v roce
            public int ObjNext { get; set; } = 1;  // další pořadí pro OBJ v roce
        }
    }
}
