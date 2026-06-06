namespace Basalt.Server.Player;

using Basalt.Protocol.Nbt;

/// <summary>
/// Selective NBT merges for cross-world player transfers.
/// </summary>
public static class PlayerNbtMerge
{
    public const string InventoryTraitId = "inventory";

    public static void ApplyInventory(CompoundTag target, CompoundTag source)
    {
        CopyRootTag(source, target, "Inventory");
        CopyRootTag(source, target, "SelectedInventorySlot");
        ReplaceTraitById(target, source, InventoryTraitId);
        ReplaceTraitById(target, source, "Basalt.Server.Entity.Traits.EntityEquipmentTrait");
    }

    static void CopyRootTag(CompoundTag source, CompoundTag target, string name)
    {
        if (source.Get<BaseTag>(name) is BaseTag tag)
        {
            target.Set(name, tag);
        }
    }

    static void ReplaceTraitById(CompoundTag target, CompoundTag source, string traitId)
    {
        ListTag? sourceTraits = source.Get<ListTag>("traits");
        if (sourceTraits is null)
        {
            return;
        }

        CompoundTag? sourceEntry = FindTraitEntry(sourceTraits, traitId);
        if (sourceEntry is null)
        {
            return;
        }

        ListTag targetTraits = target.Get<ListTag>("traits") ?? new ListTag { Name = "traits" };
        RemoveTraitEntry(targetTraits, traitId);
        targetTraits.Values.Add(sourceEntry);
        target.Set("traits", targetTraits);
    }

    static CompoundTag? FindTraitEntry(ListTag traits, string traitId)
    {
        for (int i = 0; i < traits.Values.Count; i++)
        {
            if (traits.Values[i] is not CompoundTag entry)
            {
                continue;
            }

            if (string.Equals(entry.Get<StringTag>("id")?.Value, traitId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    static void RemoveTraitEntry(ListTag traits, string traitId)
    {
        for (int i = traits.Values.Count - 1; i >= 0; i--)
        {
            if (traits.Values[i] is not CompoundTag entry)
            {
                continue;
            }

            if (string.Equals(entry.Get<StringTag>("id")?.Value, traitId, StringComparison.Ordinal))
            {
                traits.Values.RemoveAt(i);
            }
        }
    }
}
