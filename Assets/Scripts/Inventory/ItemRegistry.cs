using System.Collections.Generic;
using UnityEngine;

public class ItemRegistry : MonoBehaviour
{
    public static ItemRegistry Instance { get; private set; }

    readonly Dictionary<string, ItemDefinition> _items = new();

    void Awake()
    {
        Instance = this;
        var defs = Resources.LoadAll<ItemDefinition>("Items");
        foreach (var def in defs)
            Register(def);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Register(ItemDefinition def)
    {
        if (def != null && !string.IsNullOrEmpty(def.itemId))
            _items[def.itemId] = def;
    }

    public ItemDefinition Get(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : _items.GetValueOrDefault(itemId);
}
