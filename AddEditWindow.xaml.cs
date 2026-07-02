using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using InventoryPlus.Models;
using InventoryPlus.Services;
using Microsoft.Win32;

namespace InventoryPlus;

public partial class AddEditWindow : Window
{
    private readonly InventoryItem _item;
    private string? _pendingImageSource; // path chosen but not yet saved

    public InventoryItem? Result { get; private set; }

    // ── Constructors ──────────────────────────────────────────────────────────

    public AddEditWindow() : this(null) { }

    public AddEditWindow(InventoryItem? existing)
    {
        InitializeComponent();

        _item = existing is not null
            ? new InventoryItem
              {
                  Id          = existing.Id,
                  Name        = existing.Name,
                  BoughtPrice = existing.BoughtPrice,
                  SellingPrice = existing.SellingPrice,
                  Quantity    = existing.Quantity,
                  Category    = existing.Category,
                  Description = existing.Description,
                  ImageFile   = existing.ImageFile
              }
            : new InventoryItem();

        if (existing is not null)
        {
            DialogTitle.Text  = "Edit Item";
            NameBox.Text      = existing.Name;
            BoughtPriceBox.Text = existing.BoughtPrice.ToString("0.##", CultureInfo.CurrentCulture);
            SellingPriceBox.Text = existing.SellingPrice.ToString("0.##", CultureInfo.CurrentCulture);
            QtyBox.Text       = existing.Quantity.ToString();
            CategoryBox.Text  = existing.Category;
            DescBox.Text      = existing.Description;

            var imgPath = InventoryService.GetImagePath(existing.ImageFile);
            if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
                LoadPreview(imgPath);
        }
    }

    // ── Image picker ──────────────────────────────────────────────────────────

    // UI source: ImageBorder (opens picker) and updates PreviewImage/ImagePlaceholder
    private void PickImage_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select an image",
            Filter = "JPEG images|*.jpg;*.jpeg"
        };

        if (dlg.ShowDialog() != true) return;

        _pendingImageSource = dlg.FileName;
        LoadPreview(dlg.FileName);
    }

    private void LoadPreview(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource   = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();

        PreviewImage.Source       = bmp;
        ImagePlaceholder.Visibility = Visibility.Collapsed;
    }

    // ── Validation helpers ────────────────────────────────────────────────────

    // UI source: QtyBox
    private void QtyBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");

    // UI source: BoughtPriceBox / SellingPriceBox
    private void PriceBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !Regex.IsMatch(e.Text, @"^[\d.,]+$");

    // ── Save / Cancel ─────────────────────────────────────────────────────────

    // UI source: SaveButton
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (!int.TryParse(QtyBox.Text, out var qty) || qty < 0)
        {
            MessageBox.Show("Quantity must be a non-negative number.", "Validation",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            QtyBox.Focus();
            return;
        }

        if (!TryParseMoney(BoughtPriceBox.Text, out var boughtPrice) || boughtPrice < 0)
        {
            MessageBox.Show("Bought price must be a non-negative number.", "Validation",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            BoughtPriceBox.Focus();
            BoughtPriceBox.SelectAll();
            return;
        }

        if (!TryParseMoney(SellingPriceBox.Text, out var sellingPrice) || sellingPrice < 0)
        {
            MessageBox.Show("Selling price must be a non-negative number.", "Validation",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            SellingPriceBox.Focus();
            SellingPriceBox.SelectAll();
            return;
        }

        _item.Name        = name;
        _item.BoughtPrice = boughtPrice;
        _item.SellingPrice = sellingPrice;
        _item.Quantity    = qty;
        _item.Category    = CategoryBox.Text.Trim();
        _item.Description = DescBox.Text.Trim();

        // Copy new image into the data folder
        if (_pendingImageSource is not null)
            _item.ImageFile = InventoryService.ImportImage(_pendingImageSource, _item.Id);

        Result       = _item;
        DialogResult = true;
    }

    // UI source: CancelButton
    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;

    private static bool TryParseMoney(string value, out decimal parsed)
    {
        var normalized = value.Trim();

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            return true;

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }
}
