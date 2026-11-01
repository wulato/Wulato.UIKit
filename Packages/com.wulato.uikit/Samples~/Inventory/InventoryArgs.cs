namespace Wulato.UIKit.Samples.Inventory
{
    public enum InventoryTab { Items, Equipment }
    public readonly struct InventoryArgs
    {
        public string PlayerId { get; }
        public InventoryTab Tab { get; }
        public InventoryArgs(string playerId,InventoryTab tab){PlayerId=playerId;Tab=tab;}
    }
}
