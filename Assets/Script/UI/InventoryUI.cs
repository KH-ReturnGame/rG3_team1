using UnityEngine;

// B키로 열고/닫는 인벤토리 창 (OnGUI 기반, Canvas 세팅 불필요).
//  - 나무/주황 톤(스타듀식) · 상단 카테고리 탭 · 단축키 칸(주황 테두리+번호) 강조
//  - 칸 클릭으로 집기, 다른 칸 클릭으로 놓기/교체/스택, 창 밖 클릭으로 버리기, 마우스 호버 툴팁
// 표시만 담당하므로 나중에 UI 에셋으로 갈아끼우기 쉬움(데이터는 Inventory/GameManager).
public class InventoryUI : MonoBehaviour
{
    [Header("열기/닫기")]
    public KeyCode toggleKey = KeyCode.B;

    [Header("격자")]
    public int columns = 8;
    public bool autoSize = true;           // 화면 크기에 맞춰 칸 크기 자동
    public float slotHeightRatio = 0.11f;  // autoSize 시 화면 높이 대비 칸 크기 비율
    public float slotSize = 80f;           // autoSize 끄면 쓰는 고정 칸 크기
    public float padding = 8f;
    private float curSlot;

    [Header("툴팁 / 버리기")]
    public float tooltipWidth = 320f;
    public float dropWorldSize = 0.5f;

    private bool open;
    private ItemData heldItem;
    private int heldCount;
    private Transform playerT;
    private Hotbar hotbar;

    private int selectedCat;   // 0=전체 1=소비 2=재료 3=장비
    private static readonly string[] catNames = { "전체", "소비", "재료", "장비" };

    // ── 색(나무/주황 팔레트) ──
    private static readonly Color cPanel  = new Color(0.14f, 0.11f, 0.08f);   // 어두운 나무(불투명) — 글자/아이템이 잘 보이게
    private static readonly Color cBorder = new Color(0.86f, 0.63f, 0.30f);   // 밝은 금색 테두리
    private static readonly Color cSlot   = new Color(0.31f, 0.25f, 0.18f);   // 슬롯(패널보다 밝게)
    private static readonly Color cSlotBd = new Color(0.58f, 0.45f, 0.30f);
    private static readonly Color cAccent = new Color(1f, 0.64f, 0.14f);      // 밝은 주황(선택 탭·단축키)
    private static readonly Color cTabOff = new Color(0.34f, 0.27f, 0.19f);
    private static readonly Color cClose  = new Color(0.82f, 0.26f, 0.18f);   // 닫기(X) 버튼

    private Texture2D white;
    private GUIStyle countStyle, tipNameStyle, tipDescStyle, tabStyle, tabSelStyle, goldStyle, itemLabelStyle, hotkeyNumStyle, closeStyle;

    void Awake() { SetupItemCollisions(); }

    // Item 레이어는 맵(Ground)하고만 충돌(밀림 방지)
    private void SetupItemCollisions()
    {
        int item = LayerMask.NameToLayer("Item");
        if (item < 0) return;
        for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(item, i, true);
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0) Physics2D.IgnoreLayerCollision(item, ground, false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            if (!open) ReturnHeld();
        }
        Inventory.IsUIOpen = open;
    }

    void OnDisable() { Inventory.IsUIOpen = false; }

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;
        EnsureStyles();
        curSlot = autoSize ? Screen.height * slotHeightRatio : slotSize;

        var slots = Inventory.Instance.slots;
        int cols = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(slots.Count / (float)cols);

        float tabH = Mathf.Clamp(curSlot * 0.42f, 30f, 48f);
        float gridH = rows * (curSlot + padding) + padding;
        float equipH = 20f + curSlot + padding;            // 장신구 라벨 + 착용칸
        float w = cols * (curSlot + padding) + padding;
        float h = tabH + gridH + equipH;
        float x0 = (Screen.width - w) * 0.5f;
        float y0 = (Screen.height - h) * 0.5f;
        Rect windowRect = new Rect(x0, y0, w, h);
        float gridTop = y0 + tabH;
        float equipLabelY = gridTop + gridH;
        float equipSlotY = equipLabelY + 20f;

        Vector2 mouse = Event.current.mousePosition;

        // --- 클릭 처리 (탭 먼저, 그다음 칸/버리기) ---
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (CloseBtnRect(x0, y0, w, tabH).Contains(mouse)) { Close(); Event.current.Use(); return; }
            int tab = TabIndexAt(mouse, x0, y0, w, tabH);
            if (tab >= 0) selectedCat = tab;
            else
            {
                int idx = SlotIndexAt(mouse, x0, gridTop, cols, slots.Count);
                int eidx = EquipSlotIndexAt(mouse, x0, equipSlotY);
                if (idx >= 0) HandleSlotClick(slots, idx);
                else if (eidx >= 0) HandleEquipClick(eidx);
                else if (!windowRect.Contains(mouse) && heldItem != null) DropHeld();
            }
            Event.current.Use();
        }

        // --- 패널 ---
        Fill(windowRect, cPanel);
        Border(windowRect, 4f, cBorder);

        // --- 탭 + 골드 + 닫기(X) ---
        DrawTabs(x0, y0, w, tabH);
        int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        GUI.Label(new Rect(x0, y0, w - (tabH - 10f) - 16f, tabH), $"{gold} G", goldStyle);
        Rect cb = CloseBtnRect(x0, y0, w, tabH);
        Fill(cb, cClose);
        Border(cb, 2f, cBorder);
        GUI.Label(cb, "X", closeStyle);

        // --- 슬롯 ---
        int hoverIdx = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            Rect r = SlotRect(i, x0, gridTop, cols);
            Fill(r, cSlot);
            bool isHot = IsHotkeySlot(i, out int hkNum);
            Border(r, isHot ? 3f : 2f, isHot ? cAccent : cSlotBd);

            var s = slots[i];
            if (s != null && !s.IsEmpty)
            {
                DrawItem(r, s.item, s.count, !MatchesCat(s.item));   // 카테고리 불일치는 흐리게
                if (heldItem == null && r.Contains(mouse)) hoverIdx = i;
            }
            if (isHot) GUI.Label(new Rect(r.x + 5, r.y + 2, 22, 18), hkNum.ToString(), hotkeyNumStyle);
        }

        // --- 장신구 착용칸 (격자 아래) ---
        GUI.Label(new Rect(x0 + padding, equipLabelY + 1f, w - padding * 2f, 18f), "장신구 착용", tabStyle);
        var eq = Equipment.Instance;
        int hoverEquip = -1;
        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            Rect r = EquipSlotRect(i, x0, equipSlotY);
            Fill(r, cSlot);
            Border(r, 2f, cAccent);                        // 장신구칸은 주황 테두리로 구분
            var it = eq != null ? eq.slots[i] : null;
            if (it != null)
            {
                DrawItem(r, it, 1, false);
                if (heldItem == null && r.Contains(mouse)) hoverEquip = i;
            }
        }

        if (hoverIdx >= 0) DrawTooltip(slots[hoverIdx].item, mouse);
        else if (hoverEquip >= 0 && eq != null && eq.slots[hoverEquip] != null) DrawTooltip(eq.slots[hoverEquip], mouse);

        if (heldItem != null)
        {
            float hs = curSlot * 0.8f;
            DrawItem(new Rect(mouse.x - hs * 0.5f, mouse.y - hs * 0.5f, hs, hs), heldItem, heldCount, false);
        }
    }

    // ── 카테고리 ──
    private bool MatchesCat(ItemData item)
    {
        if (selectedCat == 0 || item == null) return true;
        switch (selectedCat)
        {
            case 1: return item.kind == ItemData.ItemKind.Consumable;
            case 2: return item.kind == ItemData.ItemKind.Material;
            case 3: return item.kind == ItemData.ItemKind.Equipment;
        }
        return true;
    }

    private void DrawTabs(float x0, float y0, float w, float tabH)
    {
        float tabW = TabWidth(w);
        for (int c = 0; c < catNames.Length; c++)
        {
            Rect t = TabRect(c, x0, y0, tabW, tabH);
            bool sel = selectedCat == c;
            Fill(t, sel ? cAccent : cTabOff);
            Border(t, 2f, cBorder);
            GUI.Label(t, catNames[c], sel ? tabSelStyle : tabStyle);
        }
    }

    private float TabWidth(float w) => Mathf.Min(120f, (w - padding * 2f) / (catNames.Length + 1.6f));
    private Rect TabRect(int c, float x0, float y0, float tabW, float tabH)
        => new Rect(x0 + padding + c * (tabW + 4f), y0 + 4f, tabW, tabH - 8f);
    private int TabIndexAt(Vector2 m, float x0, float y0, float w, float tabH)
    {
        float tabW = TabWidth(w);
        for (int c = 0; c < catNames.Length; c++)
            if (TabRect(c, x0, y0, tabW, tabH).Contains(m)) return c;
        return -1;
    }

    // 오른쪽 위 닫기(X) 버튼 영역
    private Rect CloseBtnRect(float x0, float y0, float w, float tabH)
    {
        float s = tabH - 10f;
        return new Rect(x0 + w - s - 6f, y0 + (tabH - s) * 0.5f, s, s);
    }

    // ── 단축키 칸 판정(맨 아랫줄 왼쪽 N칸) ──
    private Hotbar Bar()
    {
        if (hotbar == null) hotbar = FindAnyObjectByType<Hotbar>();
        return hotbar;
    }
    private bool IsHotkeySlot(int index, out int hotkeyNum)
    {
        hotkeyNum = 0;
        var b = Bar();
        if (b == null) return false;
        int start = Mathf.Max(0, Inventory.Instance.slotCount - b.hotbarColumns);
        if (index >= start && index < start + b.hotkeySlots) { hotkeyNum = index - start + 1; return true; }
        return false;
    }

    // ── 클릭: 집기/놓기/교체/스택 ──
    private void HandleSlotClick(System.Collections.Generic.List<Inventory.Slot> slots, int i)
    {
        var s = slots[i];
        if (heldItem == null)
        {
            if (!s.IsEmpty) { heldItem = s.item; heldCount = s.count; s.Clear(); }
        }
        else
        {
            if (s.IsEmpty) { s.item = heldItem; s.count = heldCount; heldItem = null; heldCount = 0; }
            else if (s.item == heldItem)
            {
                int put = Mathf.Min(s.item.maxStack - s.count, heldCount);
                s.count += put; heldCount -= put;
                if (heldCount <= 0) heldItem = null;
            }
            else { var ti = s.item; var tc = s.count; s.item = heldItem; s.count = heldCount; heldItem = ti; heldCount = tc; }
        }
        Inventory.Instance.RaiseChanged();
    }

    private void DropHeld()
    {
        if (heldItem != null)
        {
            Vector3 basePos = (Player() != null ? Player().position : Vector3.zero);
            SpawnWorldItem(heldItem, heldCount, basePos + Vector3.up * 0.3f);
        }
        heldItem = null; heldCount = 0;
        Inventory.Instance.RaiseChanged();
    }

    private void SpawnWorldItem(ItemData item, int count, Vector3 pos)
        => ItemPickup.SpawnWorld(item, count, pos, dropWorldSize);

    private Transform Player()
    {
        if (playerT == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) playerT = pc.transform;
        }
        return playerT;
    }

    private void ReturnHeld()
    {
        if (heldItem != null && Inventory.Instance != null) Inventory.Instance.Add(heldItem, heldCount);
        heldItem = null; heldCount = 0;
    }

    // B 없이도 닫기(X 버튼) — 들고 있던 아이템은 되돌림
    private void Close()
    {
        open = false;
        ReturnHeld();
        Inventory.IsUIOpen = false;
    }

    private Rect SlotRect(int i, float x0, float gridTop, int cols)
    {
        int r = i / cols, c = i % cols;
        return new Rect(x0 + padding + c * (curSlot + padding),
                        gridTop + padding + r * (curSlot + padding),
                        curSlot, curSlot);
    }

    private int SlotIndexAt(Vector2 m, float x0, float gridTop, int cols, int count)
    {
        for (int i = 0; i < count; i++)
            if (SlotRect(i, x0, gridTop, cols).Contains(m)) return i;
        return -1;
    }

    // ── 장신구 착용칸 ──
    private Rect EquipSlotRect(int i, float x0, float equipSlotY)
        => new Rect(x0 + padding + i * (curSlot + padding), equipSlotY, curSlot, curSlot);

    private int EquipSlotIndexAt(Vector2 m, float x0, float equipSlotY)
    {
        for (int i = 0; i < Equipment.SlotCount; i++)
            if (EquipSlotRect(i, x0, equipSlotY).Contains(m)) return i;
        return -1;
    }

    // 장신구칸 클릭: 손에 든 장신구 장착 / 착용 중인 것 집기(해제) / 교체
    private void HandleEquipClick(int i)
    {
        var eq = Equipment.Instance;
        if (eq == null) return;
        var cur = eq.slots[i];
        if (heldItem == null)
        {
            if (cur != null) { eq.Unequip(i); heldItem = cur; heldCount = 1; }
        }
        else
        {
            if (heldItem.kind != ItemData.ItemKind.Equipment) return;   // 장신구만 장착 가능
            eq.slots[i] = heldItem;
            eq.Recompute();
            if (cur != null) { heldItem = cur; heldCount = 1; }          // 기존 장신구는 손에(교체)
            else { heldItem = null; heldCount = 0; }
        }
        Inventory.Instance.RaiseChanged();
    }

    private void DrawItem(Rect r, ItemData item, int count, bool dim)
    {
        Rect inner = new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12);
        var prev = GUI.color;
        if (dim) GUI.color = new Color(1f, 1f, 1f, 0.30f);
        if (item.icon != null) GUI.DrawTexture(inner, item.icon.texture, ScaleMode.ScaleToFit);
        else GUI.Label(inner, item.itemName, itemLabelStyle);
        GUI.color = prev;
        if (count > 1) GUI.Label(new Rect(r.x, r.y, r.width - 6, r.height - 4), count.ToString(), countStyle);
    }

    private void DrawTooltip(ItemData item, Vector2 mouse)
    {
        float tw = Mathf.Max(120f, tooltipWidth);
        tipNameStyle.normal.textColor = item.RarityColor();   // 희귀도 색으로 이름 표시
        string name = item.itemName;
        string desc = item.description;
        float nameH = tipNameStyle.CalcHeight(new GUIContent(name), tw - 16);
        float descH = string.IsNullOrEmpty(desc) ? 0f : tipDescStyle.CalcHeight(new GUIContent(desc), tw - 16);
        float th = nameH + descH + 18;

        float tx = mouse.x + 18, ty = mouse.y + 18;
        if (tx + tw > Screen.width) tx = Screen.width - tw - 4;
        if (ty + th > Screen.height) ty = Screen.height - th - 4;

        Rect tr = new Rect(tx, ty, tw, th);
        Fill(tr, cPanel);
        Border(tr, 2f, cBorder);
        GUI.Label(new Rect(tx + 8, ty + 5, tw - 16, nameH), name, tipNameStyle);
        if (descH > 0) GUI.Label(new Rect(tx + 8, ty + 5 + nameH, tw - 16, descH), desc, tipDescStyle);
    }

    // ── 그리기 헬퍼(흰 1x1 텍스처를 색으로 칠함) ──
    private void Fill(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }
    private void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (countStyle != null) return;

        Color cream = new Color(0.97f, 0.93f, 0.83f);
        Color gold  = new Color(1f, 0.85f, 0.42f);
        countStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = 18 };
        countStyle.normal.textColor = Color.white;
        tipNameStyle   = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 24 };
        tipNameStyle.normal.textColor = gold;
        tipDescStyle   = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        tipDescStyle.normal.textColor = cream;
        tabStyle       = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        tabStyle.normal.textColor = new Color(0.82f, 0.74f, 0.60f);
        tabSelStyle    = new GUIStyle(tabStyle);
        tabSelStyle.normal.textColor = Color.white;
        goldStyle      = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16, fontStyle = FontStyle.Bold };
        goldStyle.normal.textColor = gold;
        itemLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };
        itemLabelStyle.normal.textColor = cream;
        hotkeyNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        hotkeyNumStyle.normal.textColor = Color.white;
        closeStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        closeStyle.normal.textColor = Color.white;
    }
}
