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
    public class Worn
    {
        public ItemData item;
        public int x, y;
        public int rot;        // R 회전 단계(0~3). 홀수면 발자국 가로/세로 스왑
        public int W => item == null ? 1 : ((rot & 1) == 1 ? item.GridH : item.GridW);
        public int H => item == null ? 1 : ((rot & 1) == 1 ? item.GridW : item.GridH);
    }
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
    public bool CanPlace(ItemData item, int x, int y, Worn ignore = null) => CanPlace(item, x, y, 0, ignore);
    public bool CanPlace(ItemData item, int x, int y, int rot, Worn ignore = null)
    {
        if (item == null || item.kind != ItemData.ItemKind.Equipment) return false;
        bool swap = (rot & 1) == 1;
        int w = swap ? item.GridH : item.GridW, h = swap ? item.GridW : item.GridH;
        if (x < 0 || y < 0 || x + w > GridW || y + h > GridH) return false;
        foreach (var s in worn)
        {
            if (s == null || s == ignore || s.item == null) continue;
            if (x < s.x + s.W && s.x < x + w && y < s.y + s.H && s.y < y + h) return false;
        }
        return true;
    }

    public Worn EntryAt(int cx, int cy)
    {
        foreach (var s in worn)
        {
            if (s == null || s.item == null) continue;
            if (cx >= s.x && cx < s.x + s.W && cy >= s.y && cy < s.y + s.H) return s;
        }
        return null;
    }

    public bool FindFree(ItemData item, out int x, out int y, out int rot)
    {
        for (int yy = 0; yy <= GridH - item.GridH; yy++)
            for (int xx = 0; xx <= GridW - item.GridW; xx++)
                if (CanPlace(item, xx, yy)) { x = xx; y = yy; rot = 0; return true; }
        if (item.GridW != item.GridH)
            for (int yy = 0; yy <= GridH - item.GridW; yy++)
                for (int xx = 0; xx <= GridW - item.GridH; xx++)
                    if (CanPlace(item, xx, yy, 1)) { x = xx; y = yy; rot = 1; return true; }
        x = y = 0; rot = 0; return false;
    }

    public Worn Place(ItemData item, int x, int y, int rot = 0)
    {
        if (!CanPlace(item, x, y, rot)) return null;
        var s = new Worn { item = item, x = x, y = y, rot = rot & 3 };
        worn.Add(s);
        Recompute();
        return s;
    }

    public void Remove(Worn s)
    {
        if (s != null && worn.Remove(s)) Recompute();
    }

    // 첫 빈 자리에 장착(자동 — 필요하면 돌려서라도). 성공 시 true.
    public bool Equip(ItemData item)
    {
        if (item == null || item.kind != ItemData.ItemKind.Equipment) return false;
        if (!FindFree(item, out int x, out int y, out int rot)) return false;
        return Place(item, x, y, rot) != null;
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

    // ── 세이브/로드 ── 형식 "id@x,y" 또는 회전 시 "id@x,y,r1~r3". (구)형식 = "id" 또는 ",r" → 호환.
    public List<string> SaveIds()
    {
        var list = new List<string>();
        foreach (var s in worn)
            if (s != null && s.item != null) list.Add(ItemDatabase.Key(s.item) + "@" + s.x + "," + s.y + (s.rot != 0 ? ",r" + s.rot : ""));
        return list;
    }

    public void LoadIds(List<string> ids)
    {
        worn.Clear();
        if (ids != null)
            foreach (var raw in ids)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string id = raw; int px = -1, py = -1; int rot = 0;
                int at = raw.IndexOf('@');
                if (at >= 0)
                {
                    id = raw.Substring(0, at);
                    var xy = raw.Substring(at + 1).Split(',');
                    if (xy.Length >= 2) { int.TryParse(xy[0], out px); int.TryParse(xy[1], out py); }
                    if (xy.Length >= 3 && xy[2].StartsWith("r"))
                    {
                        if (xy[2] == "r") rot = 1;                                  // 구형식 ",r" = 90도
                        else int.TryParse(xy[2].Substring(1), out rot);
                        rot &= 3;
                    }
                }
                var item = ItemDatabase.Get(id);
                if (item == null) continue;
                if (px >= 0 && CanPlace(item, px, py, rot)) worn.Add(new Worn { item = item, x = px, y = py, rot = rot });
                else Equip(item);   // 좌표 없음/충돌 → 자동 배치
            }
        Recompute();
    }
}
