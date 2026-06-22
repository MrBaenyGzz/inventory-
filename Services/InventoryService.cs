using System.IO;
using InventoryPlus.Models;

namespace InventoryPlus.Services;

/// <summary>
/// Reads and writes inventory data to a pipe-delimited plain-text file.
/// Format (one item per line):
///   Id|Name|Quantity|Category|Description|ImageFile
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

            items.Add(new InventoryItem
            {
                Id          = parts[0],
                Name        = Unescape(parts[1]),
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

    // ── Escaping ─────────────────────────────────────────────────────────────

    private static string Escape(string value)   => value.Replace("\\", "\\\\").Replace("|", "\\p").Replace("\n", "\\n");
    private static string Unescape(string value) => value.Replace("\\n", "\n").Replace("\\p", "|").Replace("\\\\", "\\");
}
