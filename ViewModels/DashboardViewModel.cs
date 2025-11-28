using CommunityToolkit.Mvvm.ComponentModel;
using InvoiceApp.Models;
using InvoiceApp.Services;
using System;
using System.Linq;

namespace InvoiceApp.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DocumentService _service = new();

        [ObservableProperty] private decimal totalRevenueYear;
        [ObservableProperty] private decimal totalRevenueMonth;
        [ObservableProperty] private decimal totalUnpaid;
        [ObservableProperty] private int countInvoicesYear;
        [ObservableProperty] private int countOrdersYear;

        [ObservableProperty] private int selectedYear;
        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<int> availableYears = new();

        partial void OnSelectedYearChanged(int value)
        {
            LoadStats(false); // Reload stats for selected year, false = don't reload docs
        }

        public DashboardViewModel()
        {
            SelectedYear = DateTime.Now.Year;
            LoadStats(true);
        }

        private System.Collections.Generic.List<Invoice> _allDocs = new();

        public async void LoadStats(bool reloadDocs = true)
        {
            if (reloadDocs)
            {
                _allDocs = await _service.LoadAsync();
                
                // Update available years
                var years = _allDocs.Select(d => d.IssueDate.Year).Distinct().OrderByDescending(y => y).ToList();
                if (!years.Contains(DateTime.Now.Year)) years.Insert(0, DateTime.Now.Year);
                
                AvailableYears.Clear();
                foreach (var y in years) AvailableYears.Add(y);
                
                if (!AvailableYears.Contains(SelectedYear)) SelectedYear = DateTime.Now.Year;
            }

            var year = SelectedYear;

            var invoices = _allDocs.Where(d => d.Type == DocType.Invoice).ToList();
            var orders = _allDocs.Where(d => d.Type == DocType.Order).ToList();

            // Celkem za tento rok (faktury)
            TotalRevenueYear = invoices
                .Where(i => i.IssueDate.Year == year)
                .Sum(i => i.Total);

            // Celkem za tento měsíc (faktury) - POZOR: Tady asi chceme měsíce vybraného roku?
            // Pokud uživatel vybere 2024, "tento měsíc" nedává smysl, spíš "měsíční tržby v roce 2024".
            // Ale TotalRevenueMonth je "Fakturováno tento měsíc". 
            // Necháme to tak, že to ukazuje aktuální měsíc vybraného roku? Nebo jen aktuální měsíc aktuálního roku?
            // Uživatel chce filtrovat přehledy. Takže asi vše by se mělo vztahovat k vybranému roku.
            // Ale "tento měsíc" je specifický. 
            // Uděláme to tak, že TotalRevenueMonth bude součet za aktuální kalendářní měsíc, POKUD je vybrán aktuální rok.
            // Pokud je vybrán jiný rok, tak to buď skryjeme, nebo ukážeme 0, nebo průměr?
            // Pro jednoduchost: TotalRevenueMonth bude "Tržby v aktuálním měsíci (pokud je vybrán aktuální rok)"
            // Nebo lépe: Změníme to na "Průměrná měsíční tržba" pro staré roky?
            // Ne, nechme to jednoduché: Pokud vybraný rok == aktuální rok, ukaž aktuální měsíc. Jinak 0 nebo N/A.
            
            if (year == DateTime.Now.Year)
            {
                TotalRevenueMonth = invoices
                    .Where(i => i.IssueDate.Year == year && i.IssueDate.Month == DateTime.Now.Month)
                    .Sum(i => i.Total);
            }
            else
            {
                TotalRevenueMonth = 0; // Or maybe average? Let's stick to 0 for now as "Current Month" implies "Now".
            }

            // Celkem neuhrazeno (všechny roky - to je globální dluh, nemělo by se filtrovat rokem?)
            // Uživatel se ptal na filtrování přehledů.
            // "Neuhrazeno celkem" je obvykle "co mi lidi dluží TEĎ". To nezávisí na roce vystavení (dluh z 2024 platí i v 2025).
            // Takže TotalUnpaid necháme globální.
            TotalUnpaid = invoices
                .Where(i => !i.IsPaid)
                .Sum(i => i.Total);

            // Počty
            CountInvoicesYear = invoices.Count(i => i.IssueDate.Year == year);
            CountOrdersYear = orders.Count(o => o.IssueDate.Year == year);

            // Graf - měsíční tržby
            var monthNames = new[] { "Led", "Úno", "Bře", "Dub", "Kvě", "Čer", "Čvc", "Srp", "Zář", "Říj", "Lis", "Pro" };
            var stats = new System.Collections.ObjectModel.ObservableCollection<ChartItem>();
            
            decimal maxVal = 1;

            // 1. Spočítat hodnoty
            var monthlyValues = new decimal[12];
            for (int m = 1; m <= 12; m++)
            {
                monthlyValues[m - 1] = invoices
                    .Where(i => i.IssueDate.Year == year && i.IssueDate.Month == m)
                    .Sum(i => i.Total);
            }

            maxVal = monthlyValues.Max();
            if (maxVal == 0) maxVal = 1;

            // 2. Vytvořit položky grafu
            for (int i = 0; i < 12; i++)
            {
                double height = (double)(monthlyValues[i] / maxVal) * 150;
                if (height < 2 && monthlyValues[i] > 0) height = 2;

                stats.Add(new ChartItem 
                { 
                    Label = monthNames[i], 
                    Value = monthlyValues[i], 
                    Height = height,
                    Color = monthlyValues[i] > 0 ? "#009A8D" : "#E2E8F0"
                });
            }

            MonthlyStats = stats;
        }

        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<ChartItem> monthlyStats;
    }

    public class ChartItem
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
        public double Height { get; set; }
        public string Color { get; set; } = "#E2E8F0";
        public string Tooltip => $"{Label}: {Value:N0} Kč";
    }
}
