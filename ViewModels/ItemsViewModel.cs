using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InvoiceApp.ViewModels
{
    public partial class ItemsViewModel : ObservableObject
    {
        private readonly SavedItemsService _service = new();

        public ObservableCollection<SavedItem> Items { get; } = new();

        [ObservableProperty]
        private int count;

        public ItemsViewModel()
        {
            LoadData();
        }

        private async void LoadData()
        {
            var list = await _service.LoadAsync();
            Items.Clear();
            foreach (var item in list)
            {
                item.PropertyChanged += Item_PropertyChanged;
                Items.Add(item);
            }
            UpdateCount();
        }

        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _ = SaveAsync();
        }

        private void UpdateCount() => Count = Items.Count;

        [RelayCommand]
        private void Add()
        {
            var newItem = new SavedItem { Name = "Nová položka", UnitPrice = 100, Unit = "ks" };
            newItem.PropertyChanged += Item_PropertyChanged;
            Items.Add(newItem);
            UpdateCount();
            _ = SaveAsync();
        }

        [RelayCommand]
        private void DeleteSelected(object? param)
        {
            if (param is SavedItem item)
            {
                if (MessageBox.Show($"Opravdu smazat položku '{item.Name}'?", "Smazat", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    Items.Remove(item);
                    UpdateCount();
                    _ = SaveAsync();
                }
            }
        }

        private async Task SaveAsync()
        {
            await _service.SaveAsync(Items.ToList());
        }
    }
}
