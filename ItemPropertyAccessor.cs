using System;
using System.Globalization;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

internal static class ItemPropertyAccessor
{
    private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type T = typeof(Item);

    public static bool TrySet(Item item, string memberName, object value)
    {
        if ((UnityEngine.Object)(object)item == null || string.IsNullOrWhiteSpace(memberName))
            return false;

        var prop = T.GetProperty(memberName, BF);
        if (prop != null)
        {
            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter == null) return false;

            object v = value;
            if (v != null && !prop.PropertyType.IsInstanceOfType(v))
            {
                try { v = Convert.ChangeType(v, prop.PropertyType, CultureInfo.InvariantCulture); }
                catch { return false; }
            }

            prop.SetValue(item, v);
            return true;
        }

        var field = T.GetField(memberName, BF);
        if (field != null)
        {
            object v = value;
            if (v != null && !field.FieldType.IsInstanceOfType(v))
            {
                try { v = Convert.ChangeType(v, field.FieldType, CultureInfo.InvariantCulture); }
                catch { return false; }
            }

            field.SetValue(item, v);
            return true;
        }

        return false;
    }
}
