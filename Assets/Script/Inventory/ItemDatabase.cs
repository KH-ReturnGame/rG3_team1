using System.Collections.Generic;
using UnityEngine;

// 모든 ItemData(어떤 Resources 폴더든)를 id로 찾아주는 사전. 세이브/로드 때 아이템 복원에 사용.
public static class ItemDatabase
{
    private static Dictionary<string, ItemData> map;

    private static void EnsureLoaded()
    {
        if (map != null) return;
        map = new Dictionary<string, ItemData>();
        foreach (var it in Resources.LoadAll<ItemData>(""))   // Resources 폴더 안의 모든 ItemData
        {
            string key = Key(it);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = it;
        }
    }

    // 아이템의 식별 키(id 비어있으면 에셋 파일 이름)
    public static string Key(ItemData item)
        => item == null ? "" : (string.IsNullOrEmpty(item.id) ? item.name : item.id);

    public static ItemData Get(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        return map.TryGetValue(id, out var v) ? v : null;
    }

    // 도감용 전체 목록(등급 → 이름순). 결과는 캐시.
    private static List<ItemData> all;
    public static List<ItemData> All()
    {
        EnsureLoaded();
        if (all == null)
        {
            all = new List<ItemData>(map.Values);
            all.Sort((a, b) => { int r = a.rarity.CompareTo(b.rarity); return r != 0 ? r : string.Compare(a.itemName, b.itemName, System.StringComparison.Ordinal); });
        }
        return all;
    }
}
