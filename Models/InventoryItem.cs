namespace InventoryPlus.Models;

public class InventoryItem
{
    public string Id          { get; set; } = Guid.NewGuid().ToString("N");
    public string Name        { get; set; } = string.Empty;
    public decimal BoughtPrice  { get; set; }
    public decimal SellingPrice { get; set; }
    public int    Quantity    { get; set; }
    public string Category    { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Filename only, e.g. "widget_a.jpg". File lives in Data/Images/.</summary>
    public string ImageFile   { get; set; } = string.Empty;
}
