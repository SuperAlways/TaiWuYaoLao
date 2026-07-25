namespace TaiwuEncyclopedia.Core.Probe.Dto;

public sealed class InventorySnapshot
{
    public InventoryItemRaw[] Items { get; set; } = System.Array.Empty<InventoryItemRaw>();
    public int[] Resources { get; set; } = new int[8];
    public EquipmentRaw[] Equipment { get; set; } = System.Array.Empty<EquipmentRaw>();
    public string[] Errors { get; set; } = System.Array.Empty<string>();
}

public sealed class InventoryItemRaw
{
    public string Name { get; set; } = "";
    public short TemplateId { get; set; }
    public int ItemType { get; set; }
    public int Grade { get; set; }
    public int Amount { get; set; }
    public int ModificationState { get; set; }
    public bool AllowTrade { get; set; }
    public bool IsSpecial { get; set; }
}

public sealed class EquipmentRaw
{
    public string Name { get; set; } = "";
    public short TemplateId { get; set; }
    public int Grade { get; set; }
    public int Slot { get; set; }
}