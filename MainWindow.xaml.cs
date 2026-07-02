using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using InventoryPlus.Models;
using InventoryPlus.Services;

namespace InventoryPlus;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<InventoryItem> _items = [];
    private List<InventoryItem> _allItems = [];

    public MainWindow()
    {
        InitializeComponent();
        LoadItems();
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void LoadItems()
    {
        _allItems = InventoryService.LoadAll();
        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        _items.Clear();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : _allItems.Where(i =>
                i.Name.Contains(query,        StringComparison.OrdinalIgnoreCase) ||
                i.Category.Contains(query,    StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
            _items.Add(item);

        ItemList.ItemsSource = _items;
        ClearDetail();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    // UI source: SearchBox
    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter(SearchBox.Text);

    // ── Detail Panel ──────────────────────────────────────────────────────────

    // UI source: ItemList
    private void ItemList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ItemList.SelectedItem is InventoryItem item)
            ShowDetail(item);
        else
            ClearDetail();
    }

    private void ShowDetail(InventoryItem item)
    {
        EmptyDetail.Visibility = Visibility.Collapsed;
        ItemDetail.Visibility  = Visibility.Visible;
        SetStepperEnabled(true);

        DetailName.Text     = item.Name;
        DetailCategory.Text = item.Category;
        DetailQty.Text      = item.Quantity.ToString();
        DetailDesc.Text     = item.Description;

        var imgPath = InventoryService.GetImagePath(item.ImageFile);
        if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource      = new Uri(imgPath, UriKind.Absolute);
            bmp.CacheOption    = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            DetailImage.Source = bmp;
        }
        else
        {
            DetailImage.Source = null;
        }
    }

    private void ClearDetail()
    {
        EmptyDetail.Visibility = Visibility.Visible;
        ItemDetail.Visibility  = Visibility.Collapsed;
        ItemList.SelectedItem  = null;
        SetStepperEnabled(false);
    }

    // ── Stock stepper ───────────────────────────────────────────────────────────

    private void SetStepperEnabled(bool enabled)
    {
        // Guard: called from ClearDetail during construction, before controls exist.
        if (IncreaseBtn is null || DecreaseBtn is null) return;
        IncreaseBtn.IsEnabled = enabled;
        DecreaseBtn.IsEnabled = enabled;
    }

    // UI source: StockAmountBox
    private void StockAmountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");

    private bool TryGetStepAmount(out int amount)
    {
        amount = 0;
        if (int.TryParse(StockAmountBox.Text, out var n) && n is >= 1 and <= 9999)
        {
            amount = n;
            return true;
        }

        MessageBox.Show("Enter an amount between 1 and 9999.", "Invalid amount",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
        StockAmountBox.Focus();
        StockAmountBox.SelectAll();
        return false;
    }

    // UI source: IncreaseBtn
    private void IncreaseStock_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not InventoryItem item) return;
        if (!TryGetStepAmount(out var amount)) return;

        item.Quantity += amount;
        PersistStockChange(item);
    }

    // UI source: DecreaseBtn
    private void DecreaseStock_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not InventoryItem item) return;
        if (!TryGetStepAmount(out var amount)) return;

        if (amount > item.Quantity)
        {
            MessageBox.Show(
                $"Can't remove {amount}. \"{item.Name}\" only has {item.Quantity} in stock.",
                "Not enough stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        item.Quantity -= amount;
        PersistStockChange(item);
    }

    /// <summary>Saves to disk and refreshes the list row + detail panel in place.</summary>
    private void PersistStockChange(InventoryItem item)
    {
        InventoryService.SaveAll(_allItems);
        ItemList.Items.Refresh();
        DetailQty.Text = item.Quantity.ToString();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    // UI source: AddItemButton
    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEditWindow();
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            InventoryService.Upsert(dlg.Result, _allItems);
            ApplyFilter(SearchBox.Text);
        }
    }

    // UI source: EditItemButton
    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not InventoryItem selected) return;

        var dlg = new AddEditWindow(selected);
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            InventoryService.Upsert(dlg.Result, _allItems);
            ApplyFilter(SearchBox.Text);
        }
    }

    // UI source: DeleteItemButton
    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not InventoryItem selected) return;

        var confirm = MessageBox.Show(
            $"Delete \"{selected.Name}\"?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            InventoryService.Delete(selected.Id, _allItems);
            ApplyFilter(SearchBox.Text);
        }
    }
}
