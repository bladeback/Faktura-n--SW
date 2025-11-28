using CommunityToolkit.Mvvm.ComponentModel;
using InvoiceApp.Models;
using InvoiceApp.Services;
using System;
using System.Linq;

namespace InvoiceApp.ViewModels
{
    public enum ChartPeriod { Months, Quarters, Years }

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

        [ObservableProperty] private ChartPeriod selectedPeriod = ChartPeriod.Months;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void SetPeriod(ChartPeriod period)
        {
            SelectedPeriod = period;
        }

        partial void OnSelectedYearChanged(int value)
        {
            LoadStats(false); 
        }

        partial void OnSelectedPeriodChanged(ChartPeriod value)
        {
            LoadStats(false);
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
                
                var years = _allDocs.Select(d => d.IssueDate.Year).Distinct().OrderByDescending(y => y).ToList();
                if (!years.Contains(DateTime.Now.Year)) years.Insert(0, DateTime.Now.Year);
                
                AvailableYears.Clear();
                foreach (var y in years) AvailableYears.Add(y);
                
                if (!AvailableYears.Contains(SelectedYear)) SelectedYear = DateTime.Now.Year;
            }

            var year = SelectedYear;

            var invoices = _allDocs.Where(d => d.Type == DocType.Invoice).ToList();
            var orders = _allDocs.Where(d => d.Type == DocType.Order).ToList();

            TotalRevenueYear = invoices
                .Where(i => i.IssueDate.Year == year)
                .Sum(i => i.Total);

            if (year == DateTime.Now.Year)
            {
                TotalRevenueMonth = invoices
                    .Where(i => i.IssueDate.Year == year && i.IssueDate.Month == DateTime.Now.Month)
                    .Sum(i => i.Total);
            }
            else
            {
                TotalRevenueMonth = 0; 
            }

            TotalUnpaid = invoices
                .Where(i => !i.IsPaid)
                .Sum(i => i.Total);

            CountInvoicesYear = invoices.Count(i => i.IssueDate.Year == year);
            CountOrdersYear = orders.Count(o => o.IssueDate.Year == year);

            UpdateChart(invoices);
        }

        private void UpdateChart(System.Collections.Generic.List<Invoice> invoices)
        {
            var stats = new System.Collections.ObjectModel.ObservableCollection<ChartItem>();
            decimal maxVal = 1;

            if (SelectedPeriod == ChartPeriod.Months)
            {
                var monthNames = new[] { "Led", "Úno", "Bře", "Dub", "Kvě", "Čer", "Čvc", "Srp", "Zář", "Říj", "Lis", "Pro" };
                var monthlyValues = new decimal[12];
                for (int m = 1; m <= 12; m++)
                {
                    monthlyValues[m - 1] = invoices
                        .Where(i => i.IssueDate.Year == SelectedYear && i.IssueDate.Month == m)
                        .Sum(i => i.Total);
                }

                maxVal = monthlyValues.Max();
                if (maxVal == 0) maxVal = 1;

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
            }
            else if (SelectedPeriod == ChartPeriod.Quarters)
            {
                var qNames = new[] { "Q1", "Q2", "Q3", "Q4" };
                var qValues = new decimal[4];
                
                for (int q = 1; q <= 4; q++)
                {
                    int startMonth = (q - 1) * 3 + 1;
                    int endMonth = startMonth + 2;
                    
                    qValues[q - 1] = invoices
                        .Where(i => i.IssueDate.Year == SelectedYear && i.IssueDate.Month >= startMonth && i.IssueDate.Month <= endMonth)
                        .Sum(i => i.Total);
                }

                maxVal = qValues.Max();
                if (maxVal == 0) maxVal = 1;

                for (int i = 0; i < 4; i++)
                {
                    double height = (double)(qValues[i] / maxVal) * 150;
                    if (height < 2 && qValues[i] > 0) height = 2;

                    stats.Add(new ChartItem 
                    { 
                        Label = qNames[i], 
                        Value = qValues[i], 
                        Height = height,
                        Color = qValues[i] > 0 ? "#009A8D" : "#E2E8F0"
                    });
                }
            }
            else if (SelectedPeriod == ChartPeriod.Years)
            {
                // Show last 5 years including selected
                var startYear = SelectedYear - 4;
                var yearsRange = Enumerable.Range(startYear, 5).ToList();
                var yValues = new decimal[5];

                for (int i = 0; i < 5; i++)
                {
                    int y = yearsRange[i];
                    yValues[i] = invoices
                        .Where(inv => inv.IssueDate.Year == y)
                        .Sum(inv => inv.Total);
                }

                maxVal = yValues.Max();
                if (maxVal == 0) maxVal = 1;

                for (int i = 0; i < 5; i++)
                {
                    double height = (double)(yValues[i] / maxVal) * 150;
                    if (height < 2 && yValues[i] > 0) height = 2;

                    stats.Add(new ChartItem 
                    { 
                        Label = yearsRange[i].ToString(), 
                        Value = yValues[i], 
                        Height = height,
                        Color = yValues[i] > 0 ? "#009A8D" : "#E2E8F0"
                    });
                }
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
