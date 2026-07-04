using System.Collections.Generic;
using UnityEngine;

// 장신구 착용 시스템(싱글톤·자동부팅·씬 넘어가도 유지) — 타르코프식 3×3 그리드.
//  장신구마다 gridW×gridH 발자국을 차지 → 크기 조합(테트리스)으로 몇 개를 낄지 결정된다.
//  착용한 장신구의 스탯 보너스를 GameManager(체력·공격) / PlayerController(점프)에 합산 적용.
public class Equipment : MonoBehaviour
{
    public static Equipment Instance { get; private set; }
    public const int GridW = 3, GridH = 3;

    [System.Serializable]
    public class Worn { public ItemData item; public int x, y; }
    public List<Worn> worn = new List<Worn>();

    public int MaxJumpBonus { get; private set; }       // PlayerController가 읽음

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("Equipment").AddComponent<Equipment>(); }

    // ── 그리드 ──
    public bool CanPlace(ItemData item, int x, int y, Worn ignore = null)
    {
        if (item == null || item.kind != ItemData.ItemKind.Equipment) return false;
        int w = item.GridW, h = item.GridH;
        if (x < 0 || y < 0 || x + w > GridW || y + h > GridH) return false;
        foreach (var s in worn)
        {
            if (s == null || s == ignore || s.item == null) continue;
            if (x < s.x + s.item.GridW && s.x < x + w && y < s.y + s.item.GridH && s.y < y + h) return false;
        }
        return true;
    }

    public Worn EntryAt(int cx, int cy)
    {
        foreach (var s in worn)
        {
            if (s == null || s.item == null) continue;
            if (cx >= s.x && cx < s.x + s.item.GridW && cy >= s.y && cy < s.y + s.item.GridH) return s;
        }
        return null;
    }

    public bool FindFree(ItemData item, out int x, out int y)
    {
        for (int yy = 0; yy <= GridH - item.GridH; yy++)
            for (int xx = 0; xx <= GridW - item.GridW; xx++)
                if (CanPlace(item, xx, yy)) { x = xx; y = yy; return true; }
        x = y = 0; return false;
    }

    public Worn Place(ItemData item, int x, int y)
    {
        if (!CanPlace(item, x, y)) return null;
        var s = new Worn { item = item, x = x, y = y };
        worn.Add(s);
        Recompute();
        return s;
    }

    public void Remove(Worn s)
    {
        if (s != null && worn.Remove(s)) Recompute();
    }

    // 첫 빈 자리에 장착(자동). 성공 시 true.
    public bool Equip(ItemData item)
    {
        if (item == null || item.kind != ItemData.ItemKind.Equipment) return false;
        if (!FindFree(item, out int x, out int y)) return false;
        return Place(item, x, y) != null;
    }

    // precogSlow(예지안) 등 효과 검사용 — 착용 중인 것 순회
    public IEnumerable<ItemData> Items()
    {
        foreach (var s in worn) if (s != null && s.item != null) yield return s.item;
    }

    public void Recompute()
    {
        int jump = 0, heart = 0;
        float atk = 0f;
        foreach (var s in worn)
        {
            if (s == null || s.item == null) continue;
            jump += s.item.maxJumpBonus; heart += s.item.maxHeartBonus; atk += s.item.attackBonus;
        }
        MaxJumpBonus = jump;
        if (GameManager.Instance != null) GameManager.Instance.SetEquipBonuses(heart, atk);
        if (PlayerController.Instance != null) PlayerController.Instance.ApplyEquipment();
    }

    // ── 세이브/로드 ── 형식 "id@x,y". (구)형식 = 그냥 "id" → 자동 배치.
    public List<string> SaveIds()
    {
        var list = new List<string>();
        foreach (var s in worn)
            if (s != null && s.item != null) list.Add(ItemDatabase.Key(s.item) + "@" + s.x + "," + s.y);
        return list;
    }

    public void LoadIds(List<string> ids)
    {
        worn.Clear();
        if (ids != null)
            foreach (var raw in ids)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string id = raw; int px = -1, py = -1;
                int at = raw.IndexOf('@');
                if (at >= 0)
                {
                    id = raw.Substring(0, at);
                    var xy = raw.Substring(at + 1).Split(',');
                    if (xy.Length == 2) { int.TryParse(xy[0], out px); int.TryParse(xy[1], out py); }
                }
                var item = ItemDatabase.Get(id);
                if (item == null) continue;
                if (px >= 0 && CanPlace(item, px, py)) worn.Add(new Worn { item = item, x = px, y = py });
                else Equip(item);   // 좌표 없음/충돌 → 자동 배치
            }
        Recompute();
    }
}
