using Newtonsoft.Json;

namespace TaiwuEncyclopedia.Core.Probe.Dto;

public sealed class InventorySnapshot
{
    [JsonProperty("物品")] public InventoryItemRaw[] Items { get; set; } = System.Array.Empty<InventoryItemRaw>();
    [JsonProperty("资源")] public int[] Resources { get; set; } = new int[8];
    [JsonProperty("装备")] public EquipmentRaw[] Equipment { get; set; } = System.Array.Empty<EquipmentRaw>();
    [JsonProperty("错误")] public string[] Errors { get; set; } = System.Array.Empty<string>();
}

public sealed class InventoryItemRaw
{
    [JsonProperty("名称")] public string Name { get; set; } = "";
    [JsonProperty("模板ID")] public short TemplateId { get; set; }
    [JsonProperty("物品类型")] public int ItemType { get; set; }
    [JsonProperty("品级")] public int Grade { get; set; }
    [JsonProperty("数量")] public int Amount { get; set; }
    [JsonProperty("改造状态")] public int ModificationState { get; set; }
    [JsonProperty("可交易")] public bool AllowTrade { get; set; }
    [JsonProperty("特殊")] public bool IsSpecial { get; set; }
}

public sealed class EquipmentRaw
{
    [JsonProperty("名称")] public string Name { get; set; } = "";
    [JsonProperty("模板ID")] public short TemplateId { get; set; }
    [JsonProperty("品级")] public int Grade { get; set; }
    [JsonProperty("槽位")] public int Slot { get; set; }
}
