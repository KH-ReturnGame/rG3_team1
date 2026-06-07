using System.Collections.Generic;
using UnityEngine;

// 플레이어 인벤토리(싱글톤, 씬 넘어가도 유지). 슬롯 N개 + 스택 쌓기.
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    // 인벤토리/메뉴 UI가 열려있는지(플레이어 조작 잠금용).
    // UI를 교체해도 새 UI에서 이 플래그만 켜고/끄면 플레이어 코드는 그대로 동작.
    public static bool IsUIOpen;

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
}
