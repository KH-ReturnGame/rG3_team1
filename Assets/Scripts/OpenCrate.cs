using System.Collections.Generic;
using UnityEngine;

// 크레이트(상자): F로 열면 보상이 공중에 둥둥 떠서 드랍 → 플레이어가 F로 줍는다.
// 보물상자(TreasureChest)와 동일한 사이버펑크 경제: 골드 코인 연출 없음.
//  - 골드 가치는 동화/은화/금화 화폐 아이템으로 환산해 드랍(상점에 팔아 환금)
//  - 전리품(loot)도 공중 부유로 드랍
public class OpenCrate : MonoBehaviour, IInteractable
{
    [Header("Crate Rewards")]
    public int CrateMultiplier = 1;
    public int goldMin = 10;      // 가치(골드 환산) 범위 → 동화/은화/금화로 환산해 드랍
    public int goldMax = 50;
    public LootDrop[] loot;        // 확률로 떨어지는 전리품
    public float dropSize = 0.6f;  // 떨군 아이템/코인 월드 크기
    public float dropSpacing = 0.6f;

    public string Prompt => "F: 상자 열기";

    public void Interact()
    {
        GrantRewards();
        gameObject.SetActive(false);
    }

    private void GrantRewards()
    {
        var drops = new List<KeyValuePair<ItemData, int>>();

        if (loot != null)
            foreach (var d in loot)
            {
                if (d == null || d.item == null || Random.value > d.chance) continue;
                int n = Random.Range(d.minCount, d.maxCount + 1);
                if (n > 0) drops.Add(new KeyValuePair<ItemData, int>(d.item, n));
            }

        int value = Random.Range(goldMin, goldMax + 1) * Mathf.Max(1, CrateMultiplier);
        if (value > 0) AddCoinDrops(drops, value);

        // 상자 위 공중에 한 줄로 둥둥(튀어나오지 않음, F로 줍기)
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        int cnt = drops.Count;
        for (int i = 0; i < cnt; i++)
        {
            float ox = (i - (cnt - 1) * 0.5f) * dropSpacing;
            ItemPickup.SpawnWorld(drops[i].Key, drops[i].Value, origin + new Vector3(ox, 0f, 0f), dropSize, true);
        }
    }

    // 골드 가치를 금화→은화→동화 순서로 환산(각 화폐 baseValue 기준)
    private void AddCoinDrops(List<KeyValuePair<ItemData, int>> drops, int value)
    {
        ItemData gold = ItemDatabase.Get("gold_coin");
        ItemData silver = ItemDatabase.Get("silver_coin");
        ItemData copper = ItemDatabase.Get("copper_coin");
        int rem = value;
        if (gold != null) { int v = Mathf.Max(1, gold.baseValue); int c = rem / v; if (c > 0) { drops.Add(new KeyValuePair<ItemData, int>(gold, c)); rem -= c * v; } }
        if (silver != null) { int v = Mathf.Max(1, silver.baseValue); int c = rem / v; if (c > 0) { drops.Add(new KeyValuePair<ItemData, int>(silver, c)); rem -= c * v; } }
        if (copper != null && rem > 0) { int v = Mathf.Max(1, copper.baseValue); int c = Mathf.RoundToInt(rem / (float)v); if (c > 0) drops.Add(new KeyValuePair<ItemData, int>(copper, c)); }
    }
}
