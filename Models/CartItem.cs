namespace InventoryPlus.Models;

public class CartItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitSellingPrice { get; set; }

    public decimal Subtotal => Quantity * UnitSellingPrice;
}
