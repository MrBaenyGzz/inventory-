using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using InventoryPlus.Models;
using InventoryPlus.Services;

namespace InventoryPlus;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<InventoryItem> _items = [];
    private readonly ObservableCollection<CartItem> _cart = [];
    private List<InventoryItem> _allItems = [];

    public MainWindow()
    {
        InitializeComponent();
        CartList.ItemsSource = _cart;
        UpdateCartSummary();
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
                i.BoughtPrice.ToString("0.##").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.SellingPrice.ToString("0.##").Contains(query, StringComparison.OrdinalIgnoreCase) ||
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
        DetailBoughtPrice.Text = item.BoughtPrice.ToString("0.00");
        DetailSellingPrice.Text = item.SellingPrice.ToString("0.00");
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
        if (IncreaseBtn is null || DecreaseBtn is null || AddToCartButton is null) return;
        IncreaseBtn.IsEnabled = enabled;
        DecreaseBtn.IsEnabled = enabled;
        AddToCartButton.IsEnabled = enabled;
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

    // ── Cart / Sale ──────────────────────────────────────────────────────────

    // UI source: AddToCartButton
    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (ItemList.SelectedItem is not InventoryItem selected)
        {
            MessageBox.Show("Select an item first.", "Cart", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryGetStepAmount(out var amount)) return;

        var inCart = _cart.FirstOrDefault(x => x.ItemId == selected.Id);
        var currentInCartQty = inCart?.Quantity ?? 0;
        var requestedTotal = currentInCartQty + amount;

        if (requestedTotal > selected.Quantity)
        {
            MessageBox.Show(
                $"Cannot add {amount}. \"{selected.Name}\" has {selected.Quantity} in stock and {currentInCartQty} already in cart.",
                "Not enough stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (inCart is null)
        {
            _cart.Add(new CartItem
            {
                ItemId = selected.Id,
                Name = selected.Name,
                Quantity = amount,
                UnitSellingPrice = selected.SellingPrice
            });
        }
        else
        {
            inCart.Quantity += amount;
            CartList.Items.Refresh();
        }

        UpdateCartSummary();
    }

    // UI source: CartList remove buttons
    private void CartRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string itemId) return;

        var target = _cart.FirstOrDefault(x => x.ItemId == itemId);
        if (target is null) return;

        _cart.Remove(target);
        UpdateCartSummary();
    }

    // UI source: ClearCartButton
    private void ClearCart_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0) return;
        _cart.Clear();
        UpdateCartSummary();
    }

    // UI source: ConfirmSaleButton
    private void ConfirmSale_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("The cart is empty.", "Cart", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var cartItem in _cart)
        {
            var inv = _allItems.FirstOrDefault(x => x.Id == cartItem.ItemId);
            if (inv is null)
            {
                MessageBox.Show($"Item not found in inventory: {cartItem.Name}", "Checkout error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cartItem.Quantity > inv.Quantity)
            {
                MessageBox.Show(
                    $"Not enough stock for \"{inv.Name}\". Requested: {cartItem.Quantity}, Available: {inv.Quantity}.",
                    "Checkout error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var total = _cart.Sum(x => x.Subtotal);
        var confirm = MessageBox.Show(
            $"Confirm charge for {total:0.00}?",
            "Confirm sale",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        foreach (var cartItem in _cart)
        {
            var inv = _allItems.First(x => x.Id == cartItem.ItemId);
            inv.Quantity -= cartItem.Quantity;
        }

        InventoryService.SaveAll(_allItems);

        if (ItemList.SelectedItem is InventoryItem selected)
            ShowDetail(selected);

        ItemList.Items.Refresh();
        _cart.Clear();
        UpdateCartSummary();

        MessageBox.Show("Sale completed and stock updated.", "Sale",
                        MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateCartSummary()
    {
        if (CartTotalText is null) return;
        var total = _cart.Sum(x => x.Subtotal);
        CartTotalText.Text = $"Total: {total:0.00}";
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

    // UI source: ExportExcelButton
    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_allItems.Count == 0)
        {
            MessageBox.Show("No hay items para exportar.", "Exportar Excel",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar inventario",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"inventario_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            InventoryService.ExportToExcel(_allItems, dialog.FileName);
            MessageBox.Show($"Inventario exportado correctamente:\n{dialog.FileName}",
                            "Exportar Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar el inventario.\n\n{ex.Message}",
                            "Error de exportacion", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var inCart = _cart.FirstOrDefault(x => x.ItemId == selected.Id);
            if (inCart is not null)
            {
                _cart.Remove(inCart);
                UpdateCartSummary();
            }

            InventoryService.Delete(selected.Id, _allItems);
            ApplyFilter(SearchBox.Text);
        }
    }
}
