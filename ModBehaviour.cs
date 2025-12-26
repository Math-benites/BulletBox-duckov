using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Duckov.Modding;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;

namespace BulletBox
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
    private const int ID_INJECTIONCASE = 882;
    private const int ID_AMMOBOX = 500882;

    private const string ITEM_NAME = "Item_BulletBox";
    private const string ITEM_DESC = "Item_BulletBox_Desc";
    private const float FIXED_TOTAL_WEIGHT = 1f;
    private const float WEIGHT_REDUCTION_PERCENT = 0.80f;
    private const string ID_MERCHANT_WEAPON = "Merchant_Weapon";
    private const string ICON_NAME = "box.png";

    private static readonly Dictionary<int, Item> itemCache = new Dictionary<int, Item>();
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] AmmoTagHints = { "bullet", "ammo" };
    private const bool DEBUG_ALLOW_ANY_ITEM = false;
    private const bool DEBUG_WEIGHT_DUMP = false;
    private static readonly Dictionary<Item, WeightSnapshot> itemWeightCache = new Dictionary<Item, WeightSnapshot>();
    private static readonly Dictionary<Slot, Item> slotContentCache = new Dictionary<Slot, Item>();

    private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private Coroutine weightFixRoutine;

    protected override void OnAfterSetup()
    {
        Localization.Initialize();
        Debug.Log("[BulletBox] Mod iniciado.");
        if (TryAddAmmoBoxItem())
        {
            AddItemToMerchantEntry();
            if (weightFixRoutine == null)
                weightFixRoutine = StartCoroutine(EnforceFixedWeight());
        }
    }

    protected override void OnBeforeDeactivate()
    {
        if (weightFixRoutine != null)
        {
            StopCoroutine(weightFixRoutine);
            weightFixRoutine = null;
        }
        RestoreAllItemWeights();
        slotContentCache.Clear();
        var merchant = StockShopDatabase.Instance.GetMerchantProfile(ID_MERCHANT_WEAPON);
        var entries = GetEntriesList(merchant);
        if (entries != null)
        {
            object toRemove = null;
            foreach (var entry in entries)
            {
                if (entry != null && GetIntMember(entry, "typeID") == ID_AMMOBOX)
                {
                    toRemove = entry;
                    break;
                }
            }

            if (toRemove != null)
                entries.Remove(toRemove);
        }

        if (itemCache.TryGetValue(ID_AMMOBOX, out var item) && item != null)
        {
            ItemAssetsCollection.RemoveDynamicEntry(item);
            itemCache.Remove(ID_AMMOBOX);
        }
    }

    private bool TryAddAmmoBoxItem()
    {
        bool isNew = false;
        if (!itemCache.TryGetValue(ID_AMMOBOX, out var newItem) || newItem == null)
        {
            if (!TryCopyItemWithNewID(ID_INJECTIONCASE, ID_AMMOBOX, out newItem))
            {
                Debug.LogError($"Unable to copy item id #{ID_INJECTIONCASE}.");
                return false;
            }

            isNew = true;
        }

        newItem.DisplayNameRaw = ITEM_NAME;
        ((UnityEngine.Object)newItem).name = ITEM_NAME;
        ItemPropertyAccessor.TrySet(newItem, "DescriptionRaw", ITEM_DESC);
        ItemPropertyAccessor.TrySet(newItem, "FromInfoKey", true);
        ItemPropertyAccessor.TrySet(newItem, "NeedInspection", false);
        ItemPropertyAccessor.TrySet(newItem, "Inspected", true);

        newItem.Value = 20000;
        UpdateFixedWeight(newItem);

        newItem.Tags.Clear();
        newItem.Tags.Add(TagUtilities.TagFromString("Bullet"));
        newItem.Tags.Add(TagUtilities.TagFromString("Continer"));

        newItem.Slots.Clear();
        foreach (int i in Enumerable.Range(1, 12))
        {
            var slot = new Slot("Ammo" + i);
            if (!DEBUG_ALLOW_ANY_ITEM)
            {
                slot.requireTags.Add(TagUtilities.TagFromString("Bullet"));
                slot.excludeTags.Add(TagUtilities.TagFromString("Continer"));
            }
            slot.onSlotContentChanged += OnAmmoSlotContentChanged;
            newItem.Slots.Add(slot);
        }
        newItem.Slots.OnSlotContentChanged += OnSlotCollectionChanged;

        if (!TryLoadImageAsSprite(ICON_NAME, out var sprite))
        {
            Debug.LogError($"Unable to load image from {ICON_NAME}.");
        }
        else
        {
            newItem.Icon = sprite;
            ItemPropertyAccessor.TrySet(newItem, "icon", sprite);
        }

        if (isNew)
            itemCache[ID_AMMOBOX] = newItem;
        return true;
    }

    private void AddItemToMerchantEntry()
    {
        var merchant = StockShopDatabase.Instance.GetMerchantProfile(ID_MERCHANT_WEAPON);
        var entries = GetEntriesList(merchant);
        if (entries == null)
            return;

        var entryType = GetEntryElementType(entries);
        if (entryType == null)
            return;

        var entry = Activator.CreateInstance(entryType);
        if (entry == null)
            return;

        TrySetMember(entry, "typeID", ID_AMMOBOX);
        TrySetMember(entry, "maxStock", 1);
        TrySetMember(entry, "possibility", 1f);
        TrySetMember(entry, "priceFactor", 1f);
        TrySetMember(entry, "forceUnlock", true);

        entries.Add(entry);
    }

    private bool TryCopyItemWithNewID(int itemID, int newID, out Item newItem)
    {
        newItem = null;

        var prefab = ItemAssetsCollection.GetPrefab(itemID);
        prefab.Initialize();

        var go = UnityEngine.Object.Instantiate(((Component)prefab).gameObject);
        go.name = prefab.name + "_Copied";
        UnityEngine.Object.DontDestroyOnLoad(go);

        newItem = go.GetComponent<Item>();
        if (newItem == null)
        {
            UnityEngine.Object.Destroy(go);
            Debug.LogError("Failed to get Item component from instantiated prefab.");
            return false;
        }

        bool setOk =
            ItemPropertyAccessor.TrySet(newItem, "TypeID", newID) ||
            ItemPropertyAccessor.TrySet(newItem, "typeID", newID) ||
            ItemPropertyAccessor.TrySet(newItem, "_typeID", newID);

        if (!setOk)
        {
            UnityEngine.Object.Destroy(go);
            Debug.LogError($"Failed to set ID #{newID}.");
            return false;
        }

        if (!ItemAssetsCollection.AddDynamicEntry(newItem))
        {
            UnityEngine.Object.Destroy(go);
            Debug.LogError($"Failed to add item with ID #{ID_AMMOBOX}.");
            return false;
        }

        return true;
    }

    private bool TryLoadImageAsSprite(string imageName, out Sprite sprite)
    {
        sprite = null;

        string loc = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(loc))
            loc = AppDomain.CurrentDomain.BaseDirectory;

        string fullPath = Path.Combine(Path.GetDirectoryName(loc) ?? "", imageName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Image does not exist: {fullPath}");
            return false;
        }

        if (spriteCache.TryGetValue(fullPath, out var cached) && cached != null)
        {
            sprite = cached;
            return true;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        var tex = new Texture2D(2, 2, (TextureFormat)4, false);

        if (!ImageConversion.LoadImage(tex, bytes, false))
        {
            UnityEngine.Object.Destroy(tex);
            return false;
        }

        tex.name = imageName;
        tex.wrapMode = (TextureWrapMode)1;

        sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );
        sprite.name = imageName;

        spriteCache[fullPath] = sprite;
        return true;
    }

    private static void OnAmmoSlotContentChanged(Slot slot)
    {
        if (slot == null)
            return;

        Item previous = null;
        slotContentCache.TryGetValue(slot, out previous);

        var content = slot.Content;
        if (previous != null && previous != content)
            RestoreItemWeight(previous);

        if (content == null)
        {
            slotContentCache.Remove(slot);
            UpdateFixedWeight(slot.Master);
            return;
        }

        slotContentCache[slot] = content;

        if (DEBUG_ALLOW_ANY_ITEM)
        {
            LogItemTags(content);
            ApplyReducedWeight(content);
            UpdateFixedWeight(slot.Master);
            return;
        }

        if (!IsAmmoItem(content))
        {
            RestoreItemWeight(content);
            slot.Unplug();
            slotContentCache.Remove(slot);
            UpdateFixedWeight(slot.Master);
            return;
        }

        if (DEBUG_WEIGHT_DUMP)
            DumpWeightDebug(content, "before_zero");

        ApplyReducedWeight(content);
        UpdateFixedWeight(slot.Master);

        if (DEBUG_WEIGHT_DUMP)
            DumpWeightDebug(content, "after_zero");
    }

    private static bool IsAmmoItem(Item item)
    {
        if (item == null || item.Tags == null)
            return false;

        foreach (var tag in item.Tags)
        {
            if (tag == null)
                continue;

            var name = tag.name ?? tag.DisplayName;
            if (string.IsNullOrEmpty(name))
                continue;

            var lower = name.ToLowerInvariant();
            foreach (var hint in AmmoTagHints)
            {
                if (lower.Contains(hint))
                    return true;
            }
        }

        return false;
    }

    private static void UpdateFixedWeight(Item item)
    {
        if (item == null)
            return;

        ItemPropertyAccessor.TrySet(item, "weight", FIXED_TOTAL_WEIGHT);
        ItemPropertyAccessor.TrySet(item, "SelfWeight", FIXED_TOTAL_WEIGHT);
        ItemPropertyAccessor.TrySet(item, "UnitSelfWeight", FIXED_TOTAL_WEIGHT);
        ItemPropertyAccessor.TrySet(item, "TotalWeight", FIXED_TOTAL_WEIGHT);
        ItemPropertyAccessor.TrySet(item, "_cachedTotalWeight", FIXED_TOTAL_WEIGHT);
        SetStatsWeight(item, FIXED_TOTAL_WEIGHT);
        TryRecalculateWeight(item);
    }

    private static void ApplyReducedWeight(Item item)
    {
        if (item == null)
            return;

        if (!itemWeightCache.ContainsKey(item))
        {
            var newSnapshot = new WeightSnapshot
            {
                Weight = GetFloatMember(item, "weight"),
                SelfWeight = GetFloatMember(item, "SelfWeight"),
                UnitSelfWeight = GetFloatMember(item, "UnitSelfWeight"),
                TotalWeight = GetFloatMember(item, "TotalWeight"),
                CachedTotalWeight = GetFloatMember(item, "_cachedTotalWeight"),
                StatsWeight = GetStatsWeight(item),
                HasStatsWeight = TryHasStatsWeight(item),
                StatWeights = GetAllWeightStats(item)
            };
            itemWeightCache[item] = newSnapshot;
        }

        var snapshot = itemWeightCache[item];

        var multiplier = 1f - WEIGHT_REDUCTION_PERCENT;
        SetField(item, "weight", snapshot.Weight * multiplier);
        SetField(item, "SelfWeight", snapshot.SelfWeight * multiplier);
        SetField(item, "UnitSelfWeight", snapshot.UnitSelfWeight * multiplier);
        SetField(item, "TotalWeight", snapshot.TotalWeight * multiplier);
        SetField(item, "_cachedTotalWeight", snapshot.CachedTotalWeight * multiplier);
        if (snapshot.HasStatsWeight)
            SetStatsWeight(item, snapshot.StatsWeight * multiplier);
        if (snapshot.StatWeights != null && snapshot.StatWeights.Count > 0)
            SetAllWeightStats(item, multiplier, snapshot.StatWeights);
        TryRecalculateWeight(item);
    }

    private static void RestoreItemWeight(Item item)
    {
        if (item == null)
            return;

        if (DEBUG_WEIGHT_DUMP)
            DumpWeightDebug(item, "before_restore");

        if (!itemWeightCache.TryGetValue(item, out var snapshot))
            return;

        if (!float.IsNaN(snapshot.Weight))
            SetField(item, "weight", snapshot.Weight);
        if (!float.IsNaN(snapshot.SelfWeight))
            SetField(item, "SelfWeight", snapshot.SelfWeight);
        if (!float.IsNaN(snapshot.UnitSelfWeight))
            SetField(item, "UnitSelfWeight", snapshot.UnitSelfWeight);
        if (!float.IsNaN(snapshot.TotalWeight))
            SetField(item, "TotalWeight", snapshot.TotalWeight);
        if (!float.IsNaN(snapshot.CachedTotalWeight))
            SetField(item, "_cachedTotalWeight", snapshot.CachedTotalWeight);

        if (snapshot.HasStatsWeight)
            SetStatsWeight(item, snapshot.StatsWeight);

        if (snapshot.StatWeights != null && snapshot.StatWeights.Count > 0)
            RestoreAllWeightStats(item, snapshot.StatWeights);

        TryRecalculateWeight(item);
        if (DEBUG_WEIGHT_DUMP)
            DumpWeightDebug(item, "after_restore");
        itemWeightCache.Remove(item);
    }

    private static void RestoreAllItemWeights()
    {
        if (itemWeightCache.Count == 0)
            return;

        var items = itemWeightCache.Keys.ToArray();
        foreach (var item in items)
            RestoreItemWeight(item);
    }

    private IEnumerator EnforceFixedWeight()
    {
        while (true)
        {
            if (itemCache.TryGetValue(ID_AMMOBOX, out var item) && item != null)
                UpdateFixedWeight(item);

            yield return new WaitForSeconds(0.5f);
        }
    }

    private static void OnSlotCollectionChanged(Slot slot)
    {
        if (slot == null)
            return;

        var content = slot.Content;
        if (content == null)
            return;

        Debug.Log("[BulletBox][SlotChange] " + slot.Key);
        LogItemTags(content);
    }


    private static void LogItemTags(Item item)
    {
        if (item == null || item.Tags == null)
            return;

        var names = new List<string>();
        foreach (var tag in item.Tags)
        {
            if (tag == null)
                continue;

            var name = tag.name ?? tag.DisplayName;
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        if (names.Count == 0)
            return;

        Debug.Log("[BulletBox][TagDump] " + ((UnityEngine.Object)item).name + " => " + string.Join(", ", names));
    }

    private static IList GetEntriesList(object merchant)
    {
        if (merchant == null)
            return null;

        var entries = GetMemberValue(merchant, "entries");
        return entries as IList;
    }

    private static Type GetEntryElementType(IList entries)
    {
        if (entries == null)
            return null;

        var listType = entries.GetType();
        if (listType.IsGenericType)
        {
            var args = listType.GetGenericArguments();
            if (args.Length == 1)
                return args[0];
        }

        return listType.GetElementType();
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        var t = target.GetType();
        var prop = t.GetProperty(memberName, BF);
        if (prop != null)
            return prop.GetValue(target);

        var field = t.GetField(memberName, BF);
        if (field != null)
            return field.GetValue(target);

        return null;
    }

    private static float GetFloatMember(object target, string memberName)
    {
        var value = GetMemberValue(target, memberName);
        if (value == null)
            return float.NaN;

        if (value is float f)
            return f;
        if (value is double d)
            return (float)d;
        if (value is int i)
            return i;
        if (value is long l)
            return l;

        if (float.TryParse(value.ToString(), out var parsed))
            return parsed;

        return float.NaN;
    }

    private static void SetField(Item item, string memberName, float value)
    {
        if (item == null)
            return;

        if (!ItemPropertyAccessor.TrySet(item, memberName, value))
            TrySetMember(item, memberName, value);
    }

    private static object GetStatsObject(Item item)
    {
        if (item == null)
            return null;

        var stats = GetMemberValue(item, "stats") ?? GetMemberValue(item, "Stats") ?? GetMemberValue(item, "_stats");
        return stats;
    }

    private static Dictionary<string, float> GetAllWeightStats(Item item)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return null;

        var keys = GetStatKeys(stats);
        if (keys == null)
            return null;

        var getFloat = GetStatsGetFloat(stats);
        if (getFloat == null)
            return null;

        var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!key.ToLowerInvariant().Contains("weight"))
                continue;

            var value = InvokeGetFloat(getFloat, stats, key);
            if (!float.IsNaN(value))
                dict[key] = value;
        }

        return dict.Count > 0 ? dict : null;
    }

    private static void SetAllWeightStats(Item item, float value)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return;

        var keys = GetStatKeys(stats);
        if (keys == null)
            return;

        var setFloat = GetStatsSetFloat(stats);
        if (setFloat == null)
            return;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!key.ToLowerInvariant().Contains("weight"))
                continue;

            try { setFloat.Invoke(stats, new object[] { key, value }); } catch { }
        }
    }

    private static void SetAllWeightStats(Item item, float multiplier, Dictionary<string, float> snapshot)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return;

        var setFloat = GetStatsSetFloat(stats);
        if (setFloat == null)
            return;

        foreach (var kvp in snapshot)
        {
            try { setFloat.Invoke(stats, new object[] { kvp.Key, kvp.Value * multiplier }); } catch { }
        }
    }

    private static void RestoreAllWeightStats(Item item, Dictionary<string, float> snapshot)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return;

        var setFloat = GetStatsSetFloat(stats);
        if (setFloat == null)
            return;

        foreach (var kvp in snapshot)
        {
            try { setFloat.Invoke(stats, new object[] { kvp.Key, kvp.Value }); } catch { }
        }
    }

    private static void DumpWeightDebug(Item item, string label)
    {
        if (item == null)
            return;

        var display = ((UnityEngine.Object)item).name;
        var weight = GetFloatMember(item, "weight");
        var selfWeight = GetFloatMember(item, "SelfWeight");
        var unitSelfWeight = GetFloatMember(item, "UnitSelfWeight");
        var totalWeight = GetFloatMember(item, "TotalWeight");
        var cachedTotal = GetFloatMember(item, "_cachedTotalWeight");
        var statsWeight = GetStatsWeight(item);

        Debug.Log($"[BulletBox][WeightDump:{label}] {display} weight={weight} self={selfWeight} unitSelf={unitSelfWeight} total={totalWeight} cachedTotal={cachedTotal} statsWeight={statsWeight}");

        var stats = GetStatsObject(item);
        if (stats == null)
            return;

        var keys = GetStatKeys(stats);
        if (keys == null)
            return;

        var getFloat = GetStatsGetFloat(stats);
        if (getFloat == null)
            return;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!key.ToLowerInvariant().Contains("weight"))
                continue;

            var value = InvokeGetFloat(getFloat, stats, key);
            Debug.Log($"[BulletBox][WeightDump:{label}] stat {key}={value}");
        }
    }

    private static IEnumerable<string> GetStatKeys(object stats)
    {
        if (stats == null)
            return null;

        var t = stats.GetType();
        var prop =
            t.GetProperty("referenceKeys", BF) ??
            t.GetProperty("avaliableKeys", BF) ??
            t.GetProperty("availableKeys", BF) ??
            t.GetProperty("statKeys", BF) ??
            t.GetProperty("StatKeys", BF);

        if (prop != null)
            return prop.GetValue(stats) as IEnumerable<string>;

        var method = t.GetMethod("GetStatKeys", BF);
        if (method != null)
        {
            var value = method.Invoke(stats, null);
            return value as IEnumerable<string>;
        }

        return null;
    }

    private static MethodInfo GetStatsGetFloat(object stats)
    {
        var t = stats.GetType();
        return t.GetMethod("GetFloat", BF, null, new[] { typeof(string) }, null);
    }

    private static MethodInfo GetStatsSetFloat(object stats)
    {
        var t = stats.GetType();
        return t.GetMethod("SetFloat", BF, null, new[] { typeof(string), typeof(float) }, null);
    }

    private static float GetStatsWeight(Item item)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return float.NaN;

        var getFloat = GetStatsGetFloat(stats);
        if (getFloat == null)
            return float.NaN;

        float value;
        value = InvokeGetFloat(getFloat, stats, "Weight");
        if (!float.IsNaN(value))
            return value;

        value = InvokeGetFloat(getFloat, stats, "weight");
        if (!float.IsNaN(value))
            return value;

        return float.NaN;
    }

    private static bool TryHasStatsWeight(Item item)
    {
        var value = GetStatsWeight(item);
        return !float.IsNaN(value);
    }

    private static float InvokeGetFloat(MethodInfo method, object target, string key)
    {
        try
        {
            var result = method.Invoke(target, new object[] { key });
            if (result == null)
                return float.NaN;
            return Convert.ToSingle(result);
        }
        catch
        {
            return float.NaN;
        }
    }

    private static void SetStatsWeight(Item item, float value)
    {
        var stats = GetStatsObject(item);
        if (stats == null)
            return;

        var setFloat = GetStatsSetFloat(stats);
        if (setFloat == null)
            return;

        try { setFloat.Invoke(stats, new object[] { "Weight", value }); } catch { }
        try { setFloat.Invoke(stats, new object[] { "weight", value }); } catch { }
    }

    private static void TryRecalculateWeight(Item item)
    {
        if (item == null)
            return;

        var t = item.GetType();
        var recalcWeight = t.GetMethod("RecalculateWeight", BF);
        if (recalcWeight != null)
        {
            try { recalcWeight.Invoke(item, null); } catch { }
        }

        var recalcTotal = t.GetMethod("RecalculateTotalWeight", BF);
        if (recalcTotal != null)
        {
            try { recalcTotal.Invoke(item, null); } catch { }
        }
    }

    private static bool TrySetMember(object target, string memberName, object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return false;

        var t = target.GetType();
        var prop = t.GetProperty(memberName, BF);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return true;
        }

        var field = t.GetField(memberName, BF);
        if (field != null)
        {
            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static int GetIntMember(object target, string memberName)
    {
        var value = GetMemberValue(target, memberName);
        if (value == null)
            return 0;

        if (value is int i)
            return i;

        return Convert.ToInt32(value);
    }

    private sealed class WeightSnapshot
    {
        public float Weight;
        public float SelfWeight;
        public float UnitSelfWeight;
        public float TotalWeight;
        public float CachedTotalWeight;
        public float StatsWeight;
        public bool HasStatsWeight;
        public Dictionary<string, float> StatWeights;
    }
    }
}
