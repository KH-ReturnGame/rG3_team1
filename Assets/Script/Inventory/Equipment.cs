using System.Collections.Generic;
using UnityEngine;

// 장신구 착용 시스템(싱글톤·자동부팅·씬 넘어가도 유지).
// 착용한 장신구의 스탯 보너스를 GameManager(체력·기력·공격) / PlayerController(점프·기력회복)에 합산 적용.
public class Equipment : MonoBehaviour
{
    public static Equipment Instance { get; private set; }
    public const int SlotCount = 3;
    public ItemData[] slots = new ItemData[SlotCount];

    public int MaxJumpBonus { get; private set; }       // PlayerController가 읽음

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("Equipment").AddComponent<Equipment>(); }

    // 첫 빈 칸에 장착. 성공 시 true.
    public bool Equip(ItemData item)
    {
        if (item == null || item.kind != ItemData.ItemKind.Equipment) return false;
        for (int i = 0; i < SlotCount; i++)
            if (slots[i] == null) { slots[i] = item; Recompute(); return true; }
        return false;
    }

    // i칸 해제 후 그 아이템 반환(없으면 null)
    public ItemData Unequip(int i)
    {
        if (i < 0 || i >= SlotCount) return null;
        var it = slots[i];
        slots[i] = null;
        Recompute();
        return it;
    }

    public void Recompute()
    {
        int jump = 0, heart = 0;
        float atk = 0f;
        for (int i = 0; i < SlotCount; i++)
        {
            var it = slots[i];
            if (it == null) continue;
            jump += it.maxJumpBonus; heart += it.maxHeartBonus; atk += it.attackBonus;
        }
        MaxJumpBonus = jump;
        if (GameManager.Instance != null) GameManager.Instance.SetEquipBonuses(heart, atk);
        if (PlayerController.Instance != null) PlayerController.Instance.ApplyEquipment();
    }

    // ── 세이브/로드 ──
    public List<string> SaveIds()
    {
        var list = new List<string>();
        for (int i = 0; i < SlotCount; i++) list.Add(slots[i] != null ? ItemDatabase.Key(slots[i]) : "");
        return list;
    }

    public void LoadIds(List<string> ids)
    {
        for (int i = 0; i < SlotCount; i++) slots[i] = null;
        if (ids != null)
            for (int i = 0; i < SlotCount && i < ids.Count; i++)
                if (!string.IsNullOrEmpty(ids[i])) slots[i] = ItemDatabase.Get(ids[i]);
        Recompute();
    }
}
