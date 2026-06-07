using UnityEngine;

// B키로 열고/닫는 인벤토리 창 (OnGUI 기반, Canvas 세팅 불필요).
// - 불투명 배경 + 큰 격자
// - 아이템에 마우스 올리면 이름/설명 툴팁
// - 칸 클릭으로 집기, 다른 칸 클릭으로 놓기/교체/스택, 창 밖 클릭으로 버리기
public class InventoryUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }   // 열려있으면 플레이어 조작 잠금용

    [Header("열기/닫기")]
    public KeyCode toggleKey = KeyCode.B;

    [Header("격자")]
    public int columns = 8;
    public float slotSize = 80f;
    public float padding = 8f;

    private bool open;
    private ItemData heldItem;   // 마우스로 잡은 아이템
    private int heldCount;

    private Texture2D bgTex;
    private GUIStyle titleStyle, countStyle, tipNameStyle, tipDescStyle;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            if (!open) ReturnHeld();   // 닫을 때 들고 있던 아이템 되돌리기
        }
        IsOpen = open;
    }

    void OnDisable() { IsOpen = false; }

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;

        EnsureStyles();

        var slots = Inventory.Instance.slots;
        int cols = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(slots.Count / (float)cols);

        float titleH = 34f;
        float w = cols * (slotSize + padding) + padding;
        float h = rows * (slotSize + padding) + padding + titleH;
        float x0 = (Screen.width - w) * 0.5f;
        float y0 = (Screen.height - h) * 0.5f;
        Rect windowRect = new Rect(x0, y0, w, h);
        float gridTop = y0 + titleH;

        Vector2 mouse = Event.current.mousePosition;

        // --- 클릭 처리 ---
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            int idx = SlotIndexAt(mouse, x0, gridTop, cols, slots.Count);
            if (idx >= 0)
                HandleSlotClick(slots, idx);
            else if (!windowRect.Contains(mouse) && heldItem != null)
                DropHeld();                       // 창 밖 클릭 → 버리기
            Event.current.Use();
        }

        // --- 배경(불투명) / 제목 ---
        GUI.DrawTexture(windowRect, bgTex, ScaleMode.StretchToFill);
        GUI.Box(new Rect(x0, y0, w, titleH), "인벤토리   (B: 닫기 · 클릭: 집기/놓기 · 창 밖 클릭: 버리기)", titleStyle);

        // --- 슬롯 ---
        int hoverIdx = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            Rect r = SlotRect(i, x0, gridTop, cols);
            GUI.Box(r, GUIContent.none);

            var s = slots[i];
            if (s != null && !s.IsEmpty)
            {
                DrawItem(r, s.item, s.count);
                if (heldItem == null && r.Contains(mouse)) hoverIdx = i;
            }
        }

        // --- 툴팁 (잡고 있지 않을 때만) ---
        if (hoverIdx >= 0) DrawTooltip(slots[hoverIdx].item, mouse);

        // --- 잡은 아이템을 커서에 따라다니게 ---
        if (heldItem != null)
        {
            float hs = slotSize * 0.8f;
            DrawItem(new Rect(mouse.x - hs * 0.5f, mouse.y - hs * 0.5f, hs, hs), heldItem, heldCount);
        }
    }

    // 칸 클릭: 집기 / 놓기 / 교체 / 스택
    private void HandleSlotClick(System.Collections.Generic.List<Inventory.Slot> slots, int i)
    {
        var s = slots[i];

        if (heldItem == null)
        {
            if (!s.IsEmpty)                       // 집기(스택 통째로)
            {
                heldItem = s.item; heldCount = s.count;
                s.Clear();
            }
        }
        else
        {
            if (s.IsEmpty)                        // 빈 칸 → 놓기
            {
                s.item = heldItem; s.count = heldCount;
                heldItem = null; heldCount = 0;
            }
            else if (s.item == heldItem)         // 같은 아이템 → 스택
            {
                int put = Mathf.Min(s.item.maxStack - s.count, heldCount);
                s.count += put; heldCount -= put;
                if (heldCount <= 0) heldItem = null;
            }
            else                                 // 다른 아이템 → 교체(swap)
            {
                var ti = s.item; var tc = s.count;
                s.item = heldItem; s.count = heldCount;
                heldItem = ti; heldCount = tc;
            }
        }
        Inventory.Instance.RaiseChanged();
    }

    private void DropHeld()
    {
        heldItem = null; heldCount = 0;          // 버리기(삭제)
        Inventory.Instance.RaiseChanged();
    }

    private void ReturnHeld()
    {
        if (heldItem != null && Inventory.Instance != null)
            Inventory.Instance.Add(heldItem, heldCount);
        heldItem = null; heldCount = 0;
    }

    private Rect SlotRect(int i, float x0, float gridTop, int cols)
    {
        int r = i / cols, c = i % cols;
        return new Rect(x0 + padding + c * (slotSize + padding),
                        gridTop + padding + r * (slotSize + padding),
                        slotSize, slotSize);
    }

    private int SlotIndexAt(Vector2 m, float x0, float gridTop, int cols, int count)
    {
        for (int i = 0; i < count; i++)
            if (SlotRect(i, x0, gridTop, cols).Contains(m)) return i;
        return -1;
    }

    private void DrawItem(Rect r, ItemData item, int count)
    {
        Rect inner = new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10);
        if (item.icon != null) GUI.DrawTexture(inner, item.icon.texture, ScaleMode.ScaleToFit);
        else GUI.Label(inner, item.itemName);
        if (count > 1) GUI.Label(new Rect(r.x, r.y, r.width - 5, r.height - 3), count.ToString(), countStyle);
    }

    private void DrawTooltip(ItemData item, Vector2 mouse)
    {
        const float tw = 240f;
        string name = item.itemName;
        string desc = item.description;
        float nameH = tipNameStyle.CalcHeight(new GUIContent(name), tw - 12);
        float descH = string.IsNullOrEmpty(desc) ? 0f : tipDescStyle.CalcHeight(new GUIContent(desc), tw - 12);
        float th = nameH + descH + 14;

        float tx = mouse.x + 18, ty = mouse.y + 18;
        if (tx + tw > Screen.width) tx = Screen.width - tw - 4;
        if (ty + th > Screen.height) ty = Screen.height - th - 4;

        Rect tr = new Rect(tx, ty, tw, th);
        GUI.DrawTexture(tr, bgTex, ScaleMode.StretchToFill);
        GUI.Box(tr, GUIContent.none);
        GUI.Label(new Rect(tx + 6, ty + 5, tw - 12, nameH), name, tipNameStyle);
        if (descH > 0) GUI.Label(new Rect(tx + 6, ty + 5 + nameH, tw - 12, descH), desc, tipDescStyle);
    }

    private void EnsureStyles()
    {
        if (bgTex == null)
        {
            bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.15f, 1f));  // 불투명 배경색
            bgTex.Apply();
        }
        if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        if (countStyle == null) countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
        if (tipNameStyle == null) tipNameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        if (tipDescStyle == null) tipDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
    }
}
