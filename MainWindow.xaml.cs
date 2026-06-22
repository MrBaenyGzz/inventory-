using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter(SearchBox.Text);

    // ── Detail Panel ──────────────────────────────────────────────────────────

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
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEditWindow();
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            InventoryService.Upsert(dlg.Result, _allItems);
            ApplyFilter(SearchBox.Text);
        }
    }

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
