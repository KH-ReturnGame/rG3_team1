using System.Collections.Generic;
using UnityEngine;

// 플레이어 인벤토리(싱글톤, 씬 넘어가도 유지). 슬롯 N개 + 스택 쌓기.
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    // 각 UI가 '자기 플래그'만 켜고/끈다(서로 덮어쓰지 않게). 하나라도 열려 있으면 IsUIOpen = true.
    // 플레이어 조작/공격/상호작용 잠금에 사용. 읽는 쪽은 IsUIOpen 그대로 사용.
    public static bool InvUIOpen, ShopUIOpen, CraftUIOpen, QuestUIOpen, HandbookUIOpen;
    public static bool IsUIOpen => InvUIOpen || ShopUIOpen || CraftUIOpen || QuestUIOpen || HandbookUIOpen;

    [System.Serializable]
    public class Slot
    {
        public ItemData item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
        public void Clear() { item = null; count = 0; }
    }

    [Header("슬롯")]
    public int slotCount = 24;
    public List<Slot> slots = new List<Slot>();

    public event System.Action OnChanged;   // 내용이 바뀌면 UI가 구독해서 갱신

    public void RaiseChanged() => OnChanged?.Invoke();   // 슬롯을 직접 바꾼 뒤 호출(UI 갱신 알림)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 슬롯 개수 맞추기
        if (slots == null) slots = new List<Slot>();
        while (slots.Count < slotCount) slots.Add(new Slot());
    }

    // 아이템 추가(같은 종류 스택 먼저 채우고, 남으면 빈 칸). 못 넣은 개수를 반환(0이면 전부 들어감).
    public int Add(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return 0;

        // 1) 기존 스택에 채우기
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
        // 2) 빈 칸에 넣기
        foreach (var s in slots)
        {
            if (amount <= 0) break;
            if (s.IsEmpty)
            {
                int put = Mathf.Min(item.maxStack, amount);
                s.item = item;
                s.count = put;
                amount -= put;
            }
        }

        OnChanged?.Invoke();
        return amount;   // 인벤이 꽉 차면 남은 수량이 0보다 큼
    }

    // 아이템 제거(나중에 제작/소모에 사용). 충분히 있으면 true.
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

        OnChanged?.Invoke();
        return true;
    }

    public int CountOf(ItemData item)
    {
        int total = 0;
        foreach (var s in slots)
            if (!s.IsEmpty && s.item == item) total += s.count;
        return total;
    }

    // 특정 슬롯에서 개수 차감(단축키 사용 등). 충분하면 true.
    public bool ConsumeAt(int index, int amount = 1)
    {
        if (index < 0 || index >= slots.Count) return false;
        var s = slots[index];
        if (s.IsEmpty || s.count < amount) return false;
        s.count -= amount;
        if (s.count <= 0) s.Clear();
        OnChanged?.Invoke();
        return true;
    }

    // 특정 슬롯의 아이템(비었으면 null).
    public ItemData ItemAt(int index)
        => (index >= 0 && index < slots.Count && !slots[index].IsEmpty) ? slots[index].item : null;

    // 세이브 데이터로 인벤토리 복원
    public void LoadFromSaved(List<SavedItem> saved)
    {
        foreach (var s in slots) s.Clear();
        if (saved != null)
            foreach (var si in saved)
            {
                ItemData item = ItemDatabase.Get(si.id);
                if (item != null) Add(item, si.count);
            }
        OnChanged?.Invoke();
    }
}
