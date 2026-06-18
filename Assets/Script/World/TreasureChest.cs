using System.Collections.Generic;
using UnityEngine;

// 보물상자: F로 열어 보상(아이템/골드)을 받는다. 탐험의 핵심 발견 대상.
//  - PlayerInteractor가 반경 안의 IInteractable을 F로 실행(Collider2D 필요 → 트리거 박스 사용)
//  - TreasureDetector(Z)가 All 리스트에서 가장 가까운 '안 연' 상자를 찾아 방향을 알려줌
//  - 연 상자는 세션 동안 닫힌 채 유지(씬 재진입에도). 영구 저장은 추후 SaveData 연동.
[RequireComponent(typeof(Collider2D))]
public class TreasureChest : MonoBehaviour, IInteractable
{
    public static readonly List<TreasureChest> All = new List<TreasureChest>();
    private static readonly HashSet<string> openedKeys = new HashSet<string>();   // 연 상자 기억(세션)

    [Header("식별 (비우면 씬+위치로 자동 생성)")]
    public string chestId = "";

    [Header("보상 — 아이템 (드래그로 추가, 칸마다 확률·개수)")]
    public LootDrop[] loot;           // 이 상자에서 나올 아이템들(각 item/chance/minCount/maxCount). 적 드롭과 동일 방식.
    public int lootGold = 0;          // 가치(골드 환산) → 동화/은화/금화로 환산해 드랍(0이면 화폐 없음)

    [Header("화폐 아이템 id (Resources/Items)")]
    public string copperCoinId = "copper_coin";   // 동화
    public string silverCoinId = "silver_coin";   // 은화
    public string goldCoinId   = "gold_coin";     // 금화

    [Header("드랍 연출")]
    public float dropSize = 0.6f;    // 떨군 아이템/코인 월드 크기
    public float dropSpacing = 0.6f; // 공중에 한 줄로 띄울 때 간격

    [Header("연출 (선택)")]
    public Sprite openedSprite;      // 열렸을 때 스프라이트(있으면 교체, 없으면 어둡게)
    public string prompt = "F: 상자 열기";

    private bool isOpen;
    private SpriteRenderer sr;
    private Collider2D col;

    private string Key => string.IsNullOrEmpty(chestId)
        ? gameObject.scene.name + ":" + transform.position.x.ToString("0.0") + "," + transform.position.y.ToString("0.0")
        : chestId;

    public string Prompt => isOpen ? "" : prompt;
    public bool IsOpened => isOpen;
    public Vector3 Position => transform.position;

    void Awake() { sr = GetComponent<SpriteRenderer>(); col = GetComponent<Collider2D>(); }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (openedKeys.Contains(Key)) ApplyOpenedVisual();   // 이미 연 상자면 닫힌 상태 복원
    }
    void OnDisable() { All.Remove(this); }

    public void Interact()
    {
        if (isOpen) return;
        openedKeys.Add(Key);

        // 떨굴 목록 구성: loot[] 아이템(확률) + 골드 가치를 환산한 동화/은화/금화
        var drops = new List<KeyValuePair<ItemData, int>>();
        if (loot != null)
            foreach (var d in loot)
            {
                if (d == null || d.item == null || Random.value > d.chance) continue;
                int cnt = Random.Range(d.minCount, Mathf.Max(d.minCount, d.maxCount) + 1);
                if (cnt > 0) drops.Add(new KeyValuePair<ItemData, int>(d.item, cnt));
            }
        if (lootGold > 0) AddCoinDrops(drops, lootGold);

        // 상자 위 공중에 한 줄로 둥둥 띄움(튀어나오지 않음, F로 줍기)
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        int n = drops.Count;
        for (int i = 0; i < n; i++)
        {
            float ox = (i - (n - 1) * 0.5f) * dropSpacing;
            Vector3 pos = origin + new Vector3(ox, 0f, 0f);
            ItemPickup.SpawnWorld(drops[i].Key, drops[i].Value, pos, dropSize, true);   // hover=true → 둥둥
        }

        Toast.Show("보물 상자를 열었다!", 2f);
        ApplyOpenedVisual();
    }

    // 골드 가치를 금화→은화→동화 순서로 환산해 드랍 목록에 추가(각 화폐 baseValue 기준)
    private void AddCoinDrops(List<KeyValuePair<ItemData, int>> drops, int value)
    {
        ItemData gold = ItemDatabase.Get(goldCoinId);
        ItemData silver = ItemDatabase.Get(silverCoinId);
        ItemData copper = ItemDatabase.Get(copperCoinId);
        int rem = value;
        if (gold != null) { int v = Mathf.Max(1, gold.baseValue); int c = rem / v; if (c > 0) { drops.Add(new KeyValuePair<ItemData, int>(gold, c)); rem -= c * v; } }
        if (silver != null) { int v = Mathf.Max(1, silver.baseValue); int c = rem / v; if (c > 0) { drops.Add(new KeyValuePair<ItemData, int>(silver, c)); rem -= c * v; } }
        if (copper != null && rem > 0) { int v = Mathf.Max(1, copper.baseValue); int c = Mathf.RoundToInt(rem / (float)v); if (c > 0) drops.Add(new KeyValuePair<ItemData, int>(copper, c)); }
    }

    private void ApplyOpenedVisual()
    {
        isOpen = true;
        if (sr != null)
        {
            if (openedSprite != null) sr.sprite = openedSprite;
            else { Color c = sr.color; sr.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f); }   // 어둡게 = 열림 표시
        }
        if (col != null) col.enabled = false;   // 더는 상호작용/감지 대상 아님
    }
}
