using UnityEngine;

// B키로 열고/닫는 인벤토리 창 (OnGUI 기반, Canvas 세팅 불필요).
//  - 나무/주황 톤(스타듀식) · 상단 카테고리 탭 · 단축키 칸(주황 테두리+번호) 강조
//  - 칸 클릭으로 집기, 다른 칸 클릭으로 놓기/교체/스택, 창 밖 클릭으로 버리기, 마우스 호버 툴팁
// 표시만 담당하므로 나중에 UI 에셋으로 갈아끼우기 쉬움(데이터는 Inventory/GameManager).
public class InventoryUI : MonoBehaviour
{
    [Header("열기/닫기")]
    public KeyCode toggleKey = KeyCode.B;   // 배낭(인벤) 탭 열기
    public KeyCode hoodKey = KeyCode.C;     // 후드(레벨/스탯) 탭 열기
    private int panelTab;                    // 0=배낭, 1=후드
    private int hoodSubTab;                  // 후드: 0=모듈, 1=기프트

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
    private static readonly Color cPanel  = new Color(0.07f, 0.10f, 0.15f);   // 어두운 슬레이트 배경
    private static readonly Color cBorder = new Color(0.30f, 0.80f, 0.95f);   // 시안 테두리
    private static readonly Color cSlot   = new Color(0.13f, 0.18f, 0.25f);   // 슬롯(배경보다 밝게)
    private static readonly Color cSlotBd = new Color(0.26f, 0.42f, 0.54f);
    private static readonly Color cAccent = new Color(0.30f, 0.80f, 0.95f);   // 시안(선택 탭·단축키)
    private static readonly Color cTabOff = new Color(0.14f, 0.18f, 0.24f);
    private static readonly Color cClose  = new Color(0.85f, 0.30f, 0.30f);   // 닫기(X) 버튼

    private Texture2D white;
    private GUIStyle countStyle, tipNameStyle, tipDescStyle, tabStyle, tabSelStyle, goldStyle, itemLabelStyle, hotkeyNumStyle, closeStyle, hoodTitle, hoodStat, hoodCenter;

    public static InventoryUI Instance;
    private bool hoodUpgrade;   // true면 후드에서 업그레이드(+) 가능 — 엔지니어로 열었을 때만. C키로 열면 false(조회 전용)

    void Awake() { Instance = this; SetupItemCollisions(); }

    // 엔지니어 NPC가 호출 — 후드 패널을 '업그레이드 모드'로 연다(+ 버튼 활성).
    public void OpenEngineer() { open = true; panelTab = 1; hoodSubTab = 0; hoodUpgrade = true; }

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
        if (Input.GetKeyDown(toggleKey)) { if (open && panelTab == 0) { open = false; ReturnHeld(); } else { open = true; panelTab = 0; hoodUpgrade = false; TutorialFlow.OnBackpackOpened(); } }
        if (Input.GetKeyDown(hoodKey))   { if (open && panelTab == 1) { open = false; ReturnHeld(); } else { open = true; panelTab = 1; hoodUpgrade = false; } }   // C키 = 조회 전용
        if (open && Input.GetKeyDown(KeyCode.Escape)) { open = false; ReturnHeld(); hoodUpgrade = false; }   // ESC로도 닫기
        Inventory.InvUIOpen = open;
    }

    void OnDisable() { Inventory.InvUIOpen = false; }

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

        // 좌측 세로 탭(후드/배낭)
        float stW = 76f, stH = 88f, stGap = 8f;
        float stX = x0 - stW - 2f;
        float stY = y0 + (h - (stH * 2f + stGap)) * 0.5f;
        Rect hoodRect = new Rect(stX, stY, stW, stH);
        Rect bagRect = new Rect(stX, stY + stH + stGap, stW, stH);

        Vector2 mouse = Event.current.mousePosition;

        // --- 클릭 처리 (탭 먼저, 그다음 칸/버리기) ---
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (hoodRect.Contains(mouse)) { panelTab = 1; Event.current.Use(); }
            else if (bagRect.Contains(mouse)) { panelTab = 0; Event.current.Use(); }
            else if (CloseBtnRect(x0, y0, w, tabH).Contains(mouse)) { Close(); Event.current.Use(); return; }
            else if (panelTab == 0)   // 배낭 탭에서만 인벤 상호작용
            {
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
            // 후드 탭의 클릭(+ 버튼)은 DrawHoodPanel이 처리하므로 여기서 소비하지 않음
        }

        // --- 패널 ---
        Fill(windowRect, cPanel);
        Border(windowRect, 4f, cBorder);

        // --- 좌측 세로 탭(후드/배낭) ---
        DrawSideTab(hoodRect, "후드", panelTab == 1);
        DrawSideTab(bagRect, "배낭", panelTab == 0);

        // --- 골드 + 닫기(X) (항상) ---
        int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        GUI.Label(new Rect(x0, y0, w - (tabH - 10f) - 16f, tabH), $"{gold} G", goldStyle);
        Rect cb = CloseBtnRect(x0, y0, w, tabH);
        Fill(cb, cClose);
        Border(cb, 2f, cBorder);
        GUI.Label(cb, "X", closeStyle);

        if (panelTab == 1)
        {
            DrawHoodPanel(windowRect, tabH);   // 후드: 레벨 & 스탯
        }
        else
        {
            // --- 카테고리 탭 ---
            DrawTabs(x0, y0, w, tabH);

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
        }

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
        hoodUpgrade = false;
        Inventory.InvUIOpen = false;
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

    // 좌측 세로 탭(후드/배낭)
    private void DrawSideTab(Rect r, string label, bool sel)
    {
        Fill(r, sel ? cPanel : cTabOff);
        Border(r, sel ? 3f : 2f, sel ? cAccent : cBorder);
        GUI.Label(r, label, sel ? tabSelStyle : tabStyle);
    }

    // 스탯 설명 툴팁
    private void DrawStatTooltip(string text, Vector2 m)
    {
        float tw = 290f;
        float th = tipDescStyle.CalcHeight(new GUIContent(text), tw - 16f) + 14f;
        float tx = m.x + 16f, ty = m.y + 16f;
        if (tx + tw > Screen.width) tx = Screen.width - tw - 4f;
        if (ty + th > Screen.height) ty = Screen.height - th - 4f;
        Rect tr = new Rect(tx, ty, tw, th);
        Fill(tr, cPanel); Border(tr, 2f, cBorder);
        GUI.Label(new Rect(tx + 8f, ty + 6f, tw - 16f, th - 12f), text, tipDescStyle);
    }

    // 후드 탭: [기프트/모듈] 서브탭 + 레벨(중앙) + (모듈) 캐릭터 자리 + 우측 스탯 강화
    private void DrawHoodPanel(Rect win, float topBarH)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        Vector2 mp = Event.current.mousePosition;
        bool md = Event.current.type == EventType.MouseDown && Event.current.button == 0;
        float pad = 14f, cx = win.x + pad, cy = win.y + topBarH + 8f, cw = win.width - pad * 2f;

        // ── 서브탭(모듈/기프트) ──
        float stw = 92f, sth = 30f;
        Rect tMod = new Rect(cx, cy, stw, sth), tGift = new Rect(cx + stw + 6f, cy, stw, sth);
        DrawSideTab(tMod, "모듈", hoodSubTab == 0);
        DrawSideTab(tGift, "기프트", hoodSubTab == 1);
        if (md && tMod.Contains(mp)) { hoodSubTab = 0; Event.current.Use(); }
        else if (md && tGift.Contains(mp)) { hoodSubTab = 1; Event.current.Use(); }

        // 레벨(중앙) + 개조 포인트(우측)
        GUI.Label(new Rect(cx, cy, cw, sth), "Lv. " + gm.level, hoodCenter);
        GUI.Label(new Rect(cx, cy, cw, sth), "개조 포인트 " + gm.modPoints + (hoodUpgrade ? "" : "   (엔지니어에서 업그레이드)"), goldStyle);

        // XP 바
        float topY = cy + sth + 8f;
        Rect bar = new Rect(cx, topY, cw, 12f);
        Fill(bar, cSlot); Border(bar, 1f, cSlotBd);
        float frac = gm.XpToNext > 0 ? (float)gm.xp / gm.XpToNext : 0f;
        Fill(new Rect(bar.x + 2f, bar.y + 2f, (cw - 4f) * Mathf.Clamp01(frac), 8f), cAccent);
        GUI.Label(bar, "XP " + gm.xp + " / " + gm.XpToNext, tabStyle);
        float bodyY = topY + 18f, bodyH = win.yMax - 14f - bodyY;

        if (hoodSubTab == 1)   // ── 기프트 탭(추후) ──
        {
            Rect g = new Rect(cx, bodyY, cw, bodyH);
            Fill(g, cSlot); Border(g, 2f, cSlotBd);
            GUI.Label(g, "[???]", hoodCenter);
            return;
        }

        // ── 모듈 탭: 왼쪽 캐릭터 자리 + 오른쪽 스탯 ──
        float spriteW = cw * 0.36f;
        Rect spriteBox = new Rect(cx, bodyY, spriteW, bodyH);
        Fill(spriteBox, cSlot); Border(spriteBox, 2f, cSlotBd);
        GUI.Label(spriteBox, "캐릭터\n(레드 후드)\n— 추후 —", tabStyle);

        float sx = cx + spriteW + 12f, sw = cw - spriteW - 12f;
        string[] names = { "체력 모듈", "재생력 모듈", "공격력 모듈", "적응력 모듈", "행운 모듈", "미니맵 모듈", "스캔 모듈", "빨리 뽑기 모듈" };
        string[] descs = {
            "체력 한 칸씩 증가", "체력 자동 재생력 증가",
            "물리 공격력 증가", "마법 공격력 + 기프트 효율 상승", "골드 획득량 + 전리품 획득량 + 채집물 조우 확률 상승",
            "미니맵 표시 (주변 상자·출구·적·채집물). [M]으로 켜고 끔",
            "일반 지도 열람 — 지형·다음 포탈만 표시(플레이어 위치는 안 보임). 핸드북(G) 지도 탭",
            "핫바(단축키) 슬롯 +1 — 더 많은 포션·아이템을 숫자키로 즉시 사용"
        };
        int[] levels = { Mathf.Max(0, gm.maxHearts - 3), gm.statRegen, gm.statAttack, gm.statAdapt, gm.statLuck, gm.moduleMinimap, gm.moduleScan, gm.moduleQuickdraw };
        int[] costs = { 5, 2, 1, 1, 2, 3, 3, 2 };
        int[] maxLv = { 99, 99, 99, 99, 99, 1, 1, gm.maxQuickdraw };
        int rows = names.Length;
        float rh = Mathf.Clamp(bodyH / rows, 22f, 40f);
        string hoverDesc = null;
        for (int i = 0; i < rows; i++)
        {
            Rect row = new Rect(sx, bodyY + i * rh, sw, rh - 4f);
            Fill(row, cSlot); Border(row, 1f, cSlotBd);
            Rect nameRect = new Rect(row.x + 10f, row.y, sw * 0.5f, row.height);
            GUI.Label(nameRect, names[i], hoodStat);
            if (nameRect.Contains(mp)) hoverDesc = descs[i] + "\n필요 개조 포인트: " + costs[i];

            bool module = maxLv[i] == 1;
            bool maxed = levels[i] >= maxLv[i];
            string lvText = module ? (maxed ? "보유" : "미보유") : ("Lv." + levels[i]);
            GUI.Label(new Rect(row.x + sw * 0.52f, row.y, sw * 0.3f, row.height), lvText, hoodStat);

            Rect plus = new Rect(row.xMax - 42f, row.y + 3f, 36f, row.height - 6f);
            if (maxed)
            {
                Fill(plus, cTabOff); Border(plus, 2f, cSlotBd);
                GUI.Label(plus, "✓", closeStyle);
            }
            else if (hoodUpgrade)   // 엔지니어로 연 '업그레이드 모드'에서만 + 버튼
            {
                bool can = gm.modPoints >= costs[i];
                Fill(plus, can ? cAccent : cTabOff); Border(plus, 2f, cBorder);
                GUI.Label(plus, "+", closeStyle);
                if (can && md && plus.Contains(mp))
                {
                    if (i < 5) gm.SpendStat(i);
                    else if (i == 5) gm.TryUnlockModule(0, costs[i]);   // 미니맵
                    else if (i == 6) gm.TryUnlockModule(1, costs[i]);   // 스캔
                    else gm.TryUpgradeQuickdraw(costs[i]);              // 빨리 뽑기(레벨형)
                    Event.current.Use();
                }
            }
            // else: C키로 연 조회 전용 & 미보유 → 버튼 없음
        }
        if (hoverDesc != null) DrawStatTooltip(hoverDesc, mp);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (countStyle != null) return;

        Color cream = new Color(0.90f, 0.95f, 1f);
        Color gold  = new Color(1f, 0.85f, 0.42f);
        countStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = 18 };
        countStyle.normal.textColor = Color.white;
        tipNameStyle   = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 24 };
        tipNameStyle.normal.textColor = gold;
        tipDescStyle   = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        tipDescStyle.normal.textColor = cream;
        tabStyle       = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        tabStyle.normal.textColor = new Color(0.62f, 0.72f, 0.82f);
        tabSelStyle    = new GUIStyle(tabStyle);
        tabSelStyle.normal.textColor = Color.white;
        goldStyle      = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16, fontStyle = FontStyle.Bold };
        goldStyle.normal.textColor = gold;
        hoodTitle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        hoodTitle.normal.textColor = gold;
        hoodStat = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
        hoodStat.normal.textColor = cream;
        hoodCenter = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hoodCenter.normal.textColor = gold;
        itemLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = true };
        itemLabelStyle.normal.textColor = cream;
        hotkeyNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        hotkeyNumStyle.normal.textColor = Color.white;
        closeStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
        closeStyle.normal.textColor = Color.white;
    }
}
