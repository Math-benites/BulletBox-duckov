using System.Collections.Generic;
using SodaCraft.Localizations;
using UnityEngine;

public static class Localization
{
    private static readonly Dictionary<SystemLanguage, Dictionary<string, string>> Table =
        new Dictionary<SystemLanguage, Dictionary<string, string>>
        {
            [SystemLanguage.English] = new Dictionary<string, string>
            {
                ["Item_BulletBox"] = "Bullet Box",
                ["Item_BulletBox_Desc"] = "A box that stores ammunition. 12 slots."
            },
            [SystemLanguage.Portuguese] = new Dictionary<string, string>
            {
                ["Item_BulletBox"] = "Bullet Box",
                ["Item_BulletBox_Desc"] = "Caixa para armazenar municao. 12 slots."
            }
        };

    private static readonly SystemLanguage Fallback = SystemLanguage.English;

    public static void Initialize()
    {
        LocalizationManager.OnSetLanguage += ApplyLanguage;
        ApplyLanguage(Application.systemLanguage);
    }

    private static void ApplyLanguage(SystemLanguage lang)
    {
        if (!Table.ContainsKey(lang))
            lang = Fallback;

        foreach (KeyValuePair<string, string> item in Table[lang])
            LocalizationManager.SetOverrideText(item.Key, item.Value);
    }
}
