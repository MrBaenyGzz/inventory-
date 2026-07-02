using System.IO;
using System.Globalization;
using ClosedXML.Excel;
using InventoryPlus.Models;

namespace InventoryPlus.Services;

/// <summary>
/// Reads and writes inventory data to a pipe-delimited plain-text file.
/// Format (one item per line):
///   Id|Name|BoughtPrice|SellingPrice|Quantity|Category|Description|ImageFile
/// Images are stored as .jpg files under Data/Images/.
/// </summary>
public static class InventoryService
{
    private static readonly string DataDir   = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string DataFile  = Path.Combine(DataDir, "inventory.txt");
    public  static readonly string ImagesDir = Path.Combine(DataDir, "Images");

    private const char Sep = '|';

    static InventoryService()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ImagesDir);
        if (!File.Exists(DataFile))
            File.WriteAllText(DataFile, string.Empty);
    }

    // ── Read ────────────────────────────────────────────────────────────────

    public static List<InventoryItem> LoadAll()
    {
        var items = new List<InventoryItem>();

        foreach (var raw in File.ReadAllLines(DataFile))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(Sep);
            if (parts.Length < 6) continue;

            // Backward compatibility:
            // Old format (6 fields): Id|Name|Quantity|Category|Description|ImageFile
            // New format (8 fields): Id|Name|BoughtPrice|SellingPrice|Quantity|Category|Description|ImageFile
            if (parts.Length >= 8)
            {
                items.Add(new InventoryItem
                {
                    Id           = parts[0],
                    Name         = Unescape(parts[1]),
                    BoughtPrice  = ParseDecimal(parts[2]),
                    SellingPrice = ParseDecimal(parts[3]),
                    Quantity     = int.TryParse(parts[4], out var q2) ? q2 : 0,
                    Category     = Unescape(parts[5]),
                    Description  = Unescape(parts[6]),
                    ImageFile    = parts[7]
                });

                continue;
            }

            items.Add(new InventoryItem
            {
                Id          = parts[0],
                Name        = Unescape(parts[1]),
                BoughtPrice  = 0m,
                SellingPrice = 0m,
                Quantity    = int.TryParse(parts[2], out var q) ? q : 0,
                Category    = Unescape(parts[3]),
                Description = Unescape(parts[4]),
                ImageFile   = parts[5]
            });
        }

        return items;
    }

    // ── Write ───────────────────────────────────────────────────────────────

    public static void SaveAll(IEnumerable<InventoryItem> items)
    {
        var lines = items.Select(i =>
            string.Join(Sep,
                i.Id,
                Escape(i.Name),
                i.BoughtPrice.ToString(CultureInfo.InvariantCulture),
                i.SellingPrice.ToString(CultureInfo.InvariantCulture),
                i.Quantity,
                Escape(i.Category),
                Escape(i.Description),
                i.ImageFile));

        File.WriteAllLines(DataFile, lines);
    }

    public static void Upsert(InventoryItem item, IList<InventoryItem> collection)
    {
        var idx = collection.IndexOf(collection.FirstOrDefault(x => x.Id == item.Id)!);
        if (idx >= 0)
            collection[idx] = item;
        else
            collection.Add(item);

        SaveAll(collection);
    }

    public static void Delete(string id, IList<InventoryItem> collection)
    {
        var target = collection.FirstOrDefault(x => x.Id == id);
        if (target is null) return;

        // Remove associated image if present
        if (!string.IsNullOrEmpty(target.ImageFile))
        {
            var imgPath = Path.Combine(ImagesDir, target.ImageFile);
            if (File.Exists(imgPath))
                File.Delete(imgPath);
        }

        collection.Remove(target);
        SaveAll(collection);
    }

    // ── Image helpers ────────────────────────────────────────────────────────

    /// <summary>Copies a .jpg source into Data/Images/ and returns the filename.</summary>
    public static string ImportImage(string sourcePath, string itemId)
    {
        var ext      = Path.GetExtension(sourcePath).ToLowerInvariant();
        var fileName = $"{itemId}{ext}";
        var destPath = Path.Combine(ImagesDir, fileName);
        File.Copy(sourcePath, destPath, overwrite: true);
        return fileName;
    }

    public static string GetImagePath(string imageFile)
        => string.IsNullOrEmpty(imageFile) ? string.Empty : Path.Combine(ImagesDir, imageFile);

    // ── Export ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports all inventory items to an Excel workbook (.xlsx).
    /// Images are intentionally excluded; one column is created per item data field.
    /// </summary>
    public static void ExportToExcel(IEnumerable<InventoryItem> items, string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Inventario");

        // Header row
        sheet.Cell(1, 1).Value = "BoughtPrice";
        sheet.Cell(1, 2).Value = "SellingPrice";
        sheet.Cell(1, 3).Value = "Name";
        sheet.Cell(1, 4).Value = "Quantity";
        sheet.Cell(1, 5).Value = "Category";
        sheet.Cell(1, 6).Value = "Description";

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.BoughtPrice;
            sheet.Cell(row, 2).Value = item.SellingPrice;
            sheet.Cell(row, 3).Value = item.Name;
            sheet.Cell(row, 4).Value = item.Quantity;
            sheet.Cell(row, 5).Value = item.Category;
            sheet.Cell(row, 6).Value = item.Description;
            row++;
        }

        var usedRange = sheet.RangeUsed();
        if (usedRange is not null)
        {
            usedRange.SetAutoFilter();
            var header = sheet.Row(1);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        sheet.Columns().AdjustToContents();

        var outDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(outDir))
            Directory.CreateDirectory(outDir);

        workbook.SaveAs(filePath);
    }

    // ── Escaping ─────────────────────────────────────────────────────────────

    private static decimal ParseDecimal(string input)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return value;

        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return value;

        return 0m;
    }

    private static string Escape(string value)   => value.Replace("\\", "\\\\").Replace("|", "\\p").Replace("\n", "\\n");
    private static string Unescape(string value) => value.Replace("\\n", "\n").Replace("\\p", "|").Replace("\\\\", "\\");
}
