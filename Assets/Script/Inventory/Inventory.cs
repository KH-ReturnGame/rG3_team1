using System.Collections.Generic;
using UnityEngine;

// 플레이어 인벤토리(싱글톤, 씬 넘어가도 유지) — 타르코프식 2D 그리드.
//  · 아이템마다 gridW×gridH 칸을 차지하고, 각 '엔트리'(Slot)는 그리드 왼쪽위 좌표(x,y)를 가진다.
//  · slots = "놓인 아이템 덩어리" 목록(빈 엔트리는 정리됨). 예전처럼 foreach로 읽기/차감 가능(상점·제작·세이브 호환).
//  · Add()는 스택 먼저 채우고 빈 자리를 훑어 자동 배치. 못 넣은 개수 반환.
//  · 확장: 엔지니어가 ExpandColumns(1)로 가로 1열씩 늘림(4단계).
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    // 각 UI가 '자기 플래그'만 켜고/끈다(서로 덮어쓰지 않게). 하나라도 열려 있으면 IsUIOpen = true.
    public static bool InvUIOpen, ShopUIOpen, CraftUIOpen, QuestUIOpen, HandbookUIOpen, DialogueOpen;
    public static bool IsUIOpen => InvUIOpen || ShopUIOpen || CraftUIOpen || QuestUIOpen || HandbookUIOpen || DialogueOpen;

    [System.Serializable]
    public class Slot
    {
        public ItemData item;
        public int count;
        public int x, y;                 // 그리드 왼쪽위 칸(footprint 기준점)
        public bool IsEmpty => item == null || count <= 0;
        public void Clear() { item = null; count = 0; }
    }

    [Header("그리드")]
    public int gridWidth = 6;    // 가로 칸(엔지니어 확장으로 +1씩)
    public int gridHeight = 6;   // 세로 칸
    public List<Slot> slots = new List<Slot>();   // 놓인 엔트리 목록

    public event System.Action OnChanged;   // 내용이 바뀌면 UI가 구독해서 갱신

    public void RaiseChanged() { Prune(); OnChanged?.Invoke(); }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        gridWidth = Mathf.Max(4, gridWidth);
        gridHeight = Mathf.Max(4, gridHeight);

        // 레거시(칸 리스트 시절) 직렬화 잔재 정리: 빈 엔트리 제거 + 내용물은 자동 재배치
        var carry = new List<Slot>();
        foreach (var s in slots) if (s != null && !s.IsEmpty) carry.Add(s);
        slots.Clear();
        foreach (var c in carry) Add(c.item, c.count);
    }

    private void Prune() => slots.RemoveAll(s => s == null || s.IsEmpty);

    // ── 그리드 배치 ──
    // (x,y)에 item의 발자국(w×h)을 놓을 수 있나. ignore = 이 엔트리는 없는 셈(집어든 것/교체 대상).
    public bool CanPlace(ItemData item, int x, int y, Slot ignore = null)
    {
        if (item == null) return false;
        int w = item.GridW, h = item.GridH;
        if (x < 0 || y < 0 || x + w > gridWidth || y + h > gridHeight) return false;
        foreach (var s in slots)
        {
            if (s == null || s == ignore || s.IsEmpty) continue;
            int sw = s.item.GridW, sh = s.item.GridH;
            if (x < s.x + sw && s.x < x + w && y < s.y + sh && s.y < y + h) return false;   // AABB 겹침
        }
        return true;
    }

    // 셀 (cx,cy)를 덮고 있는 엔트리(없으면 null)
    public Slot EntryAt(int cx, int cy)
    {
        foreach (var s in slots)
        {
            if (s == null || s.IsEmpty) continue;
            if (cx >= s.x && cx < s.x + s.item.GridW && cy >= s.y && cy < s.y + s.item.GridH) return s;
        }
        return null;
    }

    // 발자국이 들어갈 첫 빈 자리(행 우선). 성공 시 true.
    public bool FindFree(ItemData item, out int x, out int y)
    {
        for (int yy = 0; yy <= gridHeight - item.GridH; yy++)
            for (int xx = 0; xx <= gridWidth - item.GridW; xx++)
                if (CanPlace(item, xx, yy)) { x = xx; y = yy; return true; }
        x = y = 0; return false;
    }

    // (x,y)에 새 엔트리 생성(배치 가능해야 함). 성공 시 엔트리 반환.
    public Slot Place(ItemData item, int count, int x, int y)
    {
        if (!CanPlace(item, x, y)) return null;
        var s = new Slot { item = item, count = count, x = x, y = y };
        slots.Add(s);
        return s;
    }

    // 엔지니어 확장: 가로 열 +n
    public void ExpandColumns(int n = 1)
    {
        gridWidth += Mathf.Max(0, n);
        RaiseChanged();
    }

    // ── 기존 API(호환 유지) ──
    // 아이템 추가(같은 종류 스택 먼저 채우고, 남으면 빈 자리 자동 배치). 못 넣은 개수를 반환(0이면 전부 들어감).
    public int Add(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return 0;

        // 1) 기존 스택 채우기
        foreach (var s in slots)
        {
            if (amount <= 0) break;
            if (!s.IsEmpty && s.item == item && s.count < item.maxStack)
            {
                int put = Mathf.Min(item.maxStack - s.count, amount);
                s.count += put;
                amount -= put;
            }
        }
        // 2) 빈 자리에 새 엔트리
        while (amount > 0 && FindFree(item, out int fx, out int fy))
        {
            int put = Mathf.Min(item.maxStack, amount);
            Place(item, put, fx, fy);
            amount -= put;
        }

        RaiseChanged();
        return amount;   // 자리가 없으면 남은 수량이 0보다 큼
    }

    // 아이템 제거(제작/소모). 충분히 있으면 true.
    public bool Remove(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (CountOf(item) < amount) return false;

        for (int i = slots.Count - 1; i >= 0 && amount > 0; i--)
        {
            var s = slots[i];
            if (s.IsEmpty || s.item != item) continue;
            int take = Mathf.Min(s.count, amount);
            s.count -= take;
            amount -= take;
            if (s.count <= 0) s.Clear();
        }

        RaiseChanged();
        return true;
    }

    public int CountOf(ItemData item)
    {
        int total = 0;
        foreach (var s in slots)
            if (!s.IsEmpty && s.item == item) total += s.count;
        return total;
    }

    // 특정 엔트리에서 개수 차감(우클릭 사용 등). 충분하면 true.
    public bool ConsumeEntry(Slot s, int amount = 1)
    {
        if (s == null || s.IsEmpty || s.count < amount || !slots.Contains(s)) return false;
        s.count -= amount;
        if (s.count <= 0) s.Clear();
        RaiseChanged();
        return true;
    }

    // 세이브 데이터로 인벤토리 복원 — 저장된 배치 좌표(px,py)를 그대로 살리고, 못 살리면 자동 배치.
    public void LoadFromSaved(List<SavedItem> saved)
    {
        slots.Clear();
        if (saved != null)
        {
            var fallback = new List<SavedItem>();
            foreach (var si in saved)   // 1차: 기록된 좌표에 그대로 복원(스택 분할도 유지)
            {
                ItemData item = ItemDatabase.Get(si.id);
                if (item == null) continue;
                if (si.px >= 0 && si.py >= 0 && si.count <= Mathf.Max(1, item.maxStack) && CanPlace(item, si.px, si.py))
                    Place(item, si.count, si.px, si.py);
                else fallback.Add(si);
            }
            foreach (var si in fallback)   // 2차: 좌표 없거나 충돌(그리드 축소 등) → 자동 배치
            {
                ItemData item = ItemDatabase.Get(si.id);
                if (item != null) Add(item, si.count);
            }
        }
        RaiseChanged();
    }
}
