using UnityEngine;

// B키로 열고/닫는 인벤토리 창 (OnGUI 기반, Canvas 세팅 불필요). 메이플식 컴팩트 + 타르코프식 그리드:
//  ┌제목바(소지품)──────────────✕┐
//  │[장신구 구역]  │ [카테고리 탭]      │
//  │  칸 3개      │ [2D 그리드]        │
//  │  금화 행     │  아이템=W×H 칸 차지 │
//  └──────────────┴────────────────────┘
//  - 아이템 클릭=집기 → 발자국 고스트(초록/빨강) → 클릭=놓기/스택/교체, 창 밖 클릭=버리기
//  - 우클릭 메뉴: 사용(소비템) / 버리기
// 표시만 담당(데이터·배치 규칙은 Inventory).
public class InventoryUI : MonoBehaviour
{
    [Header("열기/닫기")]
    public KeyCode toggleKey = KeyCode.B;   // 배낭(인벤) 탭 열기
    public KeyCode hoodKey = KeyCode.C;     // 후드(레벨/스탯) 탭 열기
    private int panelTab;                    // 0=배낭, 1=후드
    private int hoodSubTab;                  // 후드: 0=모듈, 1=기프트

    [Header("격자")]
    public float slotRatio = 0.062f;        // 화면 높이 대비 한 칸 크기
    public float pad = 6f;

    [Header("툴팁 / 버리기")]
    public float tooltipWidth = 320f;
    public float dropWorldSize = 0.5f;

    private float curSlot;
    private bool open;
    private ItemData heldItem;
    private int heldCount;
    private int heldRot;                                                             // R 회전 단계(0~3 = 0/90/180/270도)
    private int HeldW => heldItem == null ? 1 : ((heldRot & 1) == 1 ? heldItem.GridH : heldItem.GridW);
    private int HeldH => heldItem == null ? 1 : ((heldRot & 1) == 1 ? heldItem.GridW : heldItem.GridH);
    private Transform playerT;

    private int selectedCat;   // 0=전체 1=소비 2=재료 3=장비
    private static readonly string[] catNames = { "전체", "소비", "재료", "장비" };

    // 우클릭 컨텍스트 메뉴
    private Inventory.Slot ctxEntry;
    private Vector2 ctxPos;
    private Rect ctxMenuRect;

    // 색은 공용 UITheme(그레이+오렌지) 사용. 닫기 버튼만 로컬 지정.
    private static readonly Color cClose = new Color(0.82f, 0.30f, 0.30f);
    private static readonly Color cOk  = new Color(0.35f, 0.85f, 0.40f);   // 배치 가능(초록)
    private static readonly Color cBad = new Color(0.90f, 0.30f, 0.28f);   // 배치 불가(빨강)

    private Texture2D white;
    private GUIStyle countStyle, tipNameStyle, tipDescStyle, tabStyle, tabSelStyle, goldStyle, itemLabelStyle, closeStyle, titleStyle, hoodTitle, hoodStat, hoodCenter, ctxStyle, hotkeyNumStyle;

    public static InventoryUI Instance;
    private bool hoodUpgrade;   // true면 후드에서 업그레이드(+) 가능 — 엔지니어로 열었을 때만. C키로 열면 false(조회 전용)

    // 그리드 렌더 원점(클릭 판정 공유)
    private float gridLeft, gridTop;

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
        if (open && heldItem != null && Input.GetKeyDown(KeyCode.R)) heldRot = (heldRot + 1) & 3;   // [R] 집은 아이템 회전
        Inventory.InvUIOpen = open;
    }

    void OnDisable() { Inventory.InvUIOpen = false; }

    void OnGUI()
    {
        var inv = Inventory.Instance;
        if (!open || inv == null) return;
        EnsureStyles();
        UIScale.Apply();

        int cols = inv.gridWidth, rows = inv.gridHeight;

        // ── 치수 ── 그리드 영역 픽셀 크기는 6×6 기준으로 '고정' — 칸 수가 적을수록 칸이 커짐(창 크기 유지)
        float baseCell = Mathf.Clamp(UIScale.H * slotRatio, 44f, 68f);
        float refPx = 6f * (baseCell + pad) + pad;
        curSlot = (refPx - pad) / cols - pad;
        float titleH = 36f;
        float catH = 28f;
        float eqCell = curSlot * 0.92f;                                  // 장신구 3×3 그리드 칸
        float eqGridPx = Equipment.GridW * (eqCell + pad) + pad;
        float eqGridPy = Equipment.GridH * (eqCell + pad) + pad;
        float gridW = cols * (curSlot + pad) + pad;
        float gridH = rows * (curSlot + pad) + pad;
        float leftW = Mathf.Max(eqGridPx + 12f, 170f);
        float bodyH = Mathf.Max(catH + 4f + gridH, 24f + eqGridPy + 44f);
        float w = pad + leftW + pad + 2f + gridW + pad;
        float h = titleH + bodyH + pad * 2f;
        float x0 = (UIScale.W - w) * 0.5f;
        float y0 = (UIScale.H - h) * 0.5f;
        Rect windowRect = new Rect(x0, y0, w, h);

        // 구역 원점
        float leftX = x0 + pad;
        float bodyY = y0 + titleH + pad;
        gridLeft = leftX + leftW + pad + 2f;
        float catY = bodyY;
        gridTop = catY + catH + 4f;
        float eqTop = bodyY + 24f;
        float eqLeft = leftX + (leftW - eqGridPx) * 0.5f;
        Rect eqRect = new Rect(eqLeft, eqTop, eqGridPx, eqGridPy);
        Rect gridRect = new Rect(gridLeft, gridTop, gridW, gridH);

        // 좌측 세로 탭(후드/배낭)
        float stW = 72f, stH = 84f, stGap = 8f;
        float stX = x0 - stW - 2f;
        float stY = y0 + (h - (stH * 2f + stGap)) * 0.5f;
        Rect hoodRect = new Rect(stX, stY, stW, stH);
        Rect bagRect = new Rect(stX, stY + stH + stGap, stW, stH);

        Vector2 mouse = Event.current.mousePosition;

        // --- 우클릭 컨텍스트 메뉴 열기/닫기 ---
        if (ctxEntry != null && Event.current.type == EventType.MouseDown && !ctxMenuRect.Contains(mouse))
        { ctxEntry = null; Event.current.Use(); }
        else if (ctxEntry == null && Event.current.type == EventType.MouseDown && Event.current.button == 1 && panelTab == 0 && heldItem == null)
        {
            var e = EntryAtMouse(inv, mouse, gridRect);
            if (e != null) { ctxEntry = e; ctxPos = mouse; Event.current.Use(); }
        }

        // --- 클릭 처리 (탭 먼저, 그다음 그리드/장신구/버리기) ---
        if (ctxEntry == null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (hoodRect.Contains(mouse)) { panelTab = 1; Event.current.Use(); }
            else if (bagRect.Contains(mouse)) { panelTab = 0; Event.current.Use(); }
            else if (CloseBtnRect(x0, y0, w, titleH).Contains(mouse)) { Close(); Event.current.Use(); return; }
            else if (panelTab == 0)
            {
                int tab = TabIndexAt(mouse, gridLeft, catY, gridW, catH);
                if (tab >= 0) selectedCat = tab;
                else if (gridRect.Contains(mouse)) HandleGridClick(inv, mouse, gridRect);
                else if (eqRect.Contains(mouse)) HandleEquipGridClick(mouse, eqRect, eqCell);
                else if (!windowRect.Contains(mouse) && heldItem != null) DropHeld();
                Event.current.Use();
            }
            // 후드 탭의 클릭(+ 버튼)은 DrawHoodPanel이 처리하므로 여기서 소비하지 않음
        }

        // --- 패널 + 제목바 ---
        UITheme.DrawPanel(windowRect);
        Rect titleBar = new Rect(x0 + 2f, y0 + 5f, w - 4f, titleH - 6f);
        UITheme.FillV(titleBar, UITheme.PanelTop, UITheme.A(UITheme.PanelBot, 0.6f));
        GUI.Label(new Rect(titleBar.x + 12f, titleBar.y, 200f, titleBar.height), panelTab == 0 ? "소지품" : "후드", titleStyle);

        // 좌측 세로 탭(후드/배낭)
        DrawSideTab(hoodRect, "후드", panelTab == 1);
        DrawSideTab(bagRect, "배낭", panelTab == 0);

        // 닫기(X)
        Rect cb = CloseBtnRect(x0, y0, w, titleH);
        bool cbHover = cb.Contains(mouse);
        UITheme.FillV(cb, cbHover ? UITheme.Lighten(cClose, 0.12f) : cClose, new Color(0.40f, 0.12f, 0.14f));
        UITheme.Border2(cb, 2f, cbHover ? UITheme.Lighten(cClose, 0.2f) : UITheme.A(cClose, 0.75f));
        GUI.Label(cb, "✕", closeStyle);

        if (panelTab == 1)
        {
            DrawHoodPanel(windowRect, titleH);   // 후드: 레벨 & 스탯
        }
        else
        {
            // ══ 왼쪽: 장신구 구역 ══
            Rect leftPane = new Rect(leftX, bodyY, leftW, bodyH);
            UITheme.FillV(leftPane, UITheme.A(UITheme.SlotBot, 0.55f), UITheme.A(UITheme.SlotBot, 0.85f));
            UITheme.Border2(leftPane, 1f, UITheme.A(UITheme.Border, 0.5f));
            GUI.Label(new Rect(leftX + 8f, bodyY + 2f, leftW - 16f, 20f), "장신구", tabSelStyle);

            var eq = Equipment.Instance;
            Equipment.Worn hoverWorn = null;

            // 3×3 빈 칸 — 테두리 없이 은은한 채움만(칸 사이 간격으로만 구분)
            for (int cy = 0; cy < Equipment.GridH; cy++)
                for (int cx = 0; cx < Equipment.GridW; cx++)
                    UITheme.Fill(EqCellRect(eqLeft, eqTop, eqCell, cx, cy), UITheme.A(UITheme.SlotBot, 0.45f));

            // 착용 중인 장신구(발자국)
            if (eq != null)
                foreach (var wv in eq.worn)
                {
                    if (wv == null || wv.item == null) continue;
                    Rect fr = EqFootprint(eqLeft, eqTop, eqCell, wv.x, wv.y, wv.W, wv.H);
                    bool hover = heldItem == null && fr.Contains(mouse);
                    UITheme.FillV(fr, hover ? UITheme.Lighten(UITheme.SlotTop, 0.10f) : UITheme.Lighten(UITheme.SlotTop, 0.03f), UITheme.SlotBot);
                    UITheme.Border2(fr, 1.5f, hover ? UITheme.Lighten(UITheme.Warm, 0.2f) : UITheme.A(UITheme.Warm, 0.85f));
                    UITheme.RarityRing(fr, wv.item.RarityColor());
                    DrawIcon(fr, wv.item, 1, false, wv.rot);
                    if (hover) hoverWorn = wv;
                }

            // 장착 고스트(들고 있는 게 장신구일 때 — [R] 회전 반영)
            if (heldItem != null && heldItem.kind == ItemData.ItemKind.Equipment && heldCount == 1 && eq != null && eqRect.Contains(mouse))
            {
                EqAnchor(mouse, eqLeft, eqTop, eqCell, HeldW, HeldH, out int eax, out int eay);
                bool okp = eq.CanPlace(heldItem, eax, eay, heldRot);
                Rect gr = EqFootprint(eqLeft, eqTop, eqCell, eax, eay, HeldW, HeldH);
                UITheme.Fill(gr, UITheme.A(okp ? cOk : cBad, 0.30f));
                UITheme.Border2(gr, 2f, UITheme.A(okp ? cOk : cBad, 0.85f));
            }

            // 금화 행(왼쪽 구역 하단)
            int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
            Rect goldRow = new Rect(leftX + 6f, leftPane.yMax - 30f, leftW - 12f, 24f);
            GUI.Label(new Rect(goldRow.x, goldRow.y, 44f, goldRow.height), "금화", tabStyle);
            Rect goldBox = new Rect(goldRow.x + 46f, goldRow.y, goldRow.width - 46f, goldRow.height);
            UITheme.FillV(goldBox, UITheme.SlotBot, UITheme.SlotTop);
            UITheme.Border2(goldBox, 1f, UITheme.A(UITheme.Border, 0.7f));
            GUI.Label(new Rect(goldBox.x, goldBox.y, goldBox.width - 8f, goldBox.height), gold.ToString("N0"), goldStyle);

            // 세로 구분선
            UITheme.Fill(new Rect(gridLeft - pad - 1f, bodyY, 2f, bodyH), UITheme.A(UITheme.Border, 0.55f));

            // ══ 오른쪽: 카테고리 탭 + 그리드 ══
            DrawTabs(gridLeft, catY, gridW, catH);

            // 빈 칸(전체 그리드)
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                    UITheme.DrawSlot(CellRect(cx, cy), UITheme.A(UITheme.Border, 0.55f), false, 1f);

            // 엔트리(놓인 아이템) — 발자국 전체를 하나로 그림
            Inventory.Slot hoverEntry = null;
            foreach (var s in inv.slots)
            {
                if (s == null || s.IsEmpty) continue;
                Rect fr = FootprintRect(s.x, s.y, s.W, s.H);
                bool hover = heldItem == null && fr.Contains(mouse);
                bool dim = !MatchesCat(s.item);

                UITheme.FillV(fr, hover ? UITheme.Lighten(UITheme.SlotTop, 0.10f) : UITheme.Lighten(UITheme.SlotTop, 0.03f), UITheme.SlotBot);
                UITheme.Border2(fr, 1.5f, hover ? UITheme.Lighten(UITheme.Border, 0.22f) : UITheme.Border);
                UITheme.RarityRing(fr, s.item.RarityColor());
                DrawIcon(fr, s.item, s.count, dim, s.rot);
                int hk = HotkeyOf(s.item);
                if (hk > 0)   // 핫키 등록된 아이템 = 앰버 번호 배지
                {
                    Rect badge = new Rect(fr.x + 3, fr.y + 3, 19, 19);
                    UITheme.Fill(badge, UITheme.A(UITheme.Warm, 0.9f));
                    GUI.Label(badge, hk.ToString(), hotkeyNumStyle);
                }
                if (hover) hoverEntry = s;
            }

            // 들고 있는 아이템: 발자국 고스트(초록/빨강) 미리보기 — [R] 회전 반영
            if (heldItem != null && gridRect.Contains(mouse))
            {
                GetAnchor(mouse, HeldW, HeldH, out int ax, out int ay);
                bool okPlace = inv.CanPlace(heldItem, ax, ay, heldRot);
                Rect gr = FootprintRect(ax, ay, HeldW, HeldH);
                UITheme.Fill(gr, UITheme.A(okPlace ? cOk : cBad, 0.30f));
                UITheme.Border2(gr, 2f, UITheme.A(okPlace ? cOk : cBad, 0.85f));
            }

            // 우클릭 메뉴가 열려 있으면 다른 아이템 툴팁은 표시하지 않음
            if (ctxEntry == null)
            {
                if (hoverEntry != null) DrawTooltip(hoverEntry.item, mouse);
                else if (hoverWorn != null) DrawTooltip(hoverWorn.item, mouse);
            }
        }

        // 들고 있는 아이템은 마우스에 발자국 크기로([R] 회전 반영 + 힌트)
        if (heldItem != null)
        {
            float hw = HeldW * (curSlot + pad) - pad;
            float hh = HeldH * (curSlot + pad) - pad;
            DrawIcon(new Rect(mouse.x - hw * 0.5f, mouse.y - hh * 0.5f, hw, hh), heldItem, heldCount, false, heldRot);
            if (heldItem.GridW != heldItem.GridH)
            {
                hotkeyNumStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(mouse.x - 40f, mouse.y + hh * 0.5f + 4f, 80f, 18f), "[R] 회전", hotkeyNumStyle);
                hotkeyNumStyle.normal.textColor = new Color(0.10f, 0.06f, 0.02f);
            }
        }

        if (ctxEntry != null) DrawContextMenu(mouse);
    }

    // ── 그리드 좌표 ──
    private Rect CellRect(int cx, int cy)
        => new Rect(gridLeft + pad + cx * (curSlot + pad), gridTop + pad + cy * (curSlot + pad), curSlot, curSlot);

    // 발자국 픽셀 사각형(칸 (x,y)부터 w×h칸)
    private Rect FootprintRect(int x, int y, int gw, int gh)
    {
        Rect a = CellRect(x, y);
        return new Rect(a.x, a.y, gw * (curSlot + pad) - pad, gh * (curSlot + pad) - pad);
    }

    // 마우스가 가리키는 셀
    private bool CellAt(Vector2 m, Rect gridRect, out int cx, out int cy)
    {
        cx = cy = 0;
        if (!gridRect.Contains(m)) return false;
        cx = Mathf.Clamp(Mathf.FloorToInt((m.x - gridLeft - pad) / (curSlot + pad)), 0, Inventory.Instance.gridWidth - 1);
        cy = Mathf.Clamp(Mathf.FloorToInt((m.y - gridTop - pad) / (curSlot + pad)), 0, Inventory.Instance.gridHeight - 1);
        return true;
    }

    // 들고 있는 아이템의 발자국 기준점(마우스 셀이 발자국 중앙쯤 오도록). w/h = 회전 반영된 유효 크기.
    private void GetAnchor(Vector2 m, int w, int h, out int ax, out int ay)
    {
        CellAt(m, new Rect(gridLeft, gridTop, 99999f, 99999f), out int cx, out int cy);
        var inv = Inventory.Instance;
        ax = Mathf.Clamp(cx - (w - 1) / 2, 0, Mathf.Max(0, inv.gridWidth - w));
        ay = Mathf.Clamp(cy - (h - 1) / 2, 0, Mathf.Max(0, inv.gridHeight - h));
    }

    private Inventory.Slot EntryAtMouse(Inventory inv, Vector2 m, Rect gridRect)
    {
        if (!CellAt(m, gridRect, out int cx, out int cy)) return null;
        return inv.EntryAt(cx, cy);
    }

    // ── 그리드 클릭: 집기 / 놓기 / 스택 / 교체 (회전 반영) ──
    private void HandleGridClick(Inventory inv, Vector2 m, Rect gridRect)
    {
        if (heldItem == null)
        {
            var e = EntryAtMouse(inv, m, gridRect);
            if (e != null) { heldItem = e.item; heldCount = e.count; heldRot = e.rot; inv.slots.Remove(e); inv.RaiseChanged(); }
            return;
        }

        GetAnchor(m, HeldW, HeldH, out int ax, out int ay);

        // 1) 빈 자리면 그대로 놓기
        if (inv.CanPlace(heldItem, ax, ay, heldRot))
        {
            inv.Place(heldItem, heldCount, ax, ay, heldRot);
            heldItem = null; heldCount = 0; heldRot = 0;
            inv.RaiseChanged();
            return;
        }

        // 2) 발자국과 겹치는 엔트리 수집
        var overlaps = new System.Collections.Generic.List<Inventory.Slot>();
        foreach (var s in inv.slots)
        {
            if (s == null || s.IsEmpty) continue;
            if (ax < s.x + s.W && s.x < ax + HeldW && ay < s.y + s.H && s.y < ay + HeldH)
                overlaps.Add(s);
        }
        if (overlaps.Count != 1) return;   // 여러 개와 겹치면 놓을 수 없음

        var t = overlaps[0];
        if (t.item == heldItem && t.count < heldItem.maxStack)
        {
            // 같은 아이템 → 스택 합치기
            int put = Mathf.Min(heldItem.maxStack - t.count, heldCount);
            t.count += put; heldCount -= put;
            if (heldCount <= 0) { heldItem = null; heldCount = 0; heldRot = 0; }
            inv.RaiseChanged();
        }
        else if (inv.CanPlace(heldItem, ax, ay, heldRot, t))
        {
            // 교체(그 엔트리만 비켜준 자리에 들어가면 서로 교환)
            var ti = t.item; int tc = t.count; int tr = t.rot;
            inv.slots.Remove(t);
            inv.Place(heldItem, heldCount, ax, ay, heldRot);
            heldItem = ti; heldCount = tc; heldRot = tr;
            inv.RaiseChanged();
        }
    }

    // 우클릭 메뉴: 사용(소비템) / 버리기
    private void DrawContextMenu(Vector2 mouse)
    {
        var inv = Inventory.Instance;
        if (inv == null || ctxEntry == null || ctxEntry.IsEmpty || !inv.slots.Contains(ctxEntry)) { ctxEntry = null; return; }
        var item = ctxEntry.item;

        bool usable = item.kind == ItemData.ItemKind.Consumable;
        int hkCount = Hotbar.Instance != null ? Hotbar.Instance.hotkeySlots : 0;
        var labels = new System.Collections.Generic.List<string>();
        var acts = new System.Collections.Generic.List<System.Action>();
        if (usable) { labels.Add("사용"); acts.Add(CtxUse); }
        if (usable) for (int k = 0; k < hkCount; k++) { int kk = k; labels.Add((k + 1) + "번 슬롯에 등록"); acts.Add(() => CtxRegister(kk)); }
        labels.Add("버리기"); acts.Add(CtxDrop);

        float mw = 200f, rowH = 32f, mpad = 5f;
        float mh = labels.Count * rowH + mpad * 2f;
        float mx = Mathf.Min(ctxPos.x, UIScale.W - mw - 6f);
        float my = Mathf.Min(ctxPos.y, UIScale.H - mh - 6f);
        ctxMenuRect = new Rect(mx, my, mw, mh);

        UITheme.Shadow(ctxMenuRect, 12f, 0.42f);
        UITheme.FillV(ctxMenuRect, UITheme.PanelTop, UITheme.PanelBot);
        UITheme.Border2(ctxMenuRect, 2f, UITheme.Accent);
        UITheme.Fill(new Rect(mx + 2f, my + 2f, mw - 4f, 3f), UITheme.Accent);

        for (int i = 0; i < labels.Count; i++)
        {
            Rect row = new Rect(mx + mpad, my + mpad + i * rowH, mw - mpad * 2f, rowH - 2f);
            if (row.Contains(mouse)) UITheme.Fill(row, UITheme.A(UITheme.Accent, 0.18f));
            ctxStyle.normal.textColor = (labels[i] == "버리기") ? UITheme.Danger : UITheme.Text;
            GUI.Label(new Rect(row.x + 12f, row.y, row.width - 14f, row.height), labels[i], ctxStyle);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { acts[i](); ctxEntry = null; }
        }
    }

    private void CtxUse()
    {
        if (ctxEntry == null || ctxEntry.IsEmpty) return;
        var item = ctxEntry.item;
        if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null && !GameManager.Instance.IsPotionReady(item))
        { Toast.Show(item.itemName + " 쿨타임 " + Mathf.CeilToInt(GameManager.Instance.PotionCooldownLeft(item)) + "초", 1.5f); return; }
        if (item.Use())
        {
            Inventory.Instance.ConsumeEntry(ctxEntry, 1);
            if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null) GameManager.Instance.StartPotionCooldown(item);
        }
    }

    private void CtxRegister(int k)
    {
        if (ctxEntry == null || ctxEntry.IsEmpty || Hotbar.Instance == null) return;
        Hotbar.Instance.Register(ctxEntry.item, k);
        Toast.Show(ctxEntry.item.itemName + " → " + (k + 1) + "번 단축키 등록", 1.5f);
    }

    // 이 아이템이 등록된 핫키 번호(1-based). 없으면 0.
    private int HotkeyOf(ItemData item)
    {
        var b = Hotbar.Instance;
        if (b == null || item == null) return 0;
        for (int k = 0; k < b.hotkeySlots; k++) if (b.GetRegistered(k) == item) return k + 1;
        return 0;
    }

    private void CtxDrop()
    {
        if (ctxEntry == null || ctxEntry.IsEmpty) return;
        Vector3 basePos = (Player() != null ? Player().position : Vector3.zero);
        SpawnWorldItem(ctxEntry.item, ctxEntry.count, basePos + Vector3.up * 0.3f);
        ctxEntry.Clear();
        Inventory.Instance.RaiseChanged();
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

    private void DrawTabs(float gridLeft, float catY, float gridW, float catH)
    {
        float tabW = TabWidth(gridW);
        Vector2 m = Event.current.mousePosition;
        for (int c = 0; c < catNames.Length; c++)
        {
            Rect t = TabRect(c, gridLeft, catY, tabW, catH);
            bool sel = selectedCat == c;
            if (sel)
            {
                UITheme.FillV(t, UITheme.Lighten(UITheme.Accent, 0.05f), UITheme.AccentDim);
                UITheme.Border2(t, 1.5f, UITheme.Lighten(UITheme.Accent, 0.2f));
            }
            else
            {
                bool hv = t.Contains(m);
                UITheme.FillV(t, hv ? UITheme.Lighten(UITheme.SlotTop, 0.05f) : UITheme.SlotTop, UITheme.SlotBot);
                UITheme.Border2(t, 1f, UITheme.Border);
            }
            GUI.Label(t, catNames[c], sel ? tabSelStyle : tabStyle);
        }
    }

    private float TabWidth(float gridW) => (gridW - pad * 2f - (catNames.Length - 1) * 4f) / catNames.Length;
    private Rect TabRect(int c, float gridLeft, float catY, float tabW, float catH)
        => new Rect(gridLeft + pad + c * (tabW + 4f), catY, tabW, catH);
    private int TabIndexAt(Vector2 m, float gridLeft, float catY, float gridW, float catH)
    {
        float tabW = TabWidth(gridW);
        for (int c = 0; c < catNames.Length; c++)
            if (TabRect(c, gridLeft, catY, tabW, catH).Contains(m)) return c;
        return -1;
    }

    // 오른쪽 위 닫기(X) 버튼 영역
    private Rect CloseBtnRect(float x0, float y0, float w, float titleH)
    {
        float s = titleH - 12f;
        return new Rect(x0 + w - s - 8f, y0 + (titleH - s) * 0.5f + 2f, s, s);
    }

    private void DropHeld()
    {
        if (heldItem != null)
        {
            Vector3 basePos = (Player() != null ? Player().position : Vector3.zero);
            SpawnWorldItem(heldItem, heldCount, basePos + Vector3.up * 0.3f);
        }
        heldItem = null; heldCount = 0; heldRot = 0;
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
        heldItem = null; heldCount = 0; heldRot = 0;
    }

    // B 없이도 닫기(X 버튼) — 들고 있던 아이템은 되돌림
    private void Close()
    {
        open = false;
        ReturnHeld();
        hoodUpgrade = false;
        Inventory.InvUIOpen = false;
    }

    // ── 장신구 3×3 그리드 (왼쪽 구역) ──
    private Rect EqCellRect(float eqLeft, float eqTop, float cell, int cx, int cy)
        => new Rect(eqLeft + pad + cx * (cell + pad), eqTop + pad + cy * (cell + pad), cell, cell);

    private Rect EqFootprint(float eqLeft, float eqTop, float cell, int x, int y, int gw, int gh)
    {
        Rect a = EqCellRect(eqLeft, eqTop, cell, x, y);
        return new Rect(a.x, a.y, gw * (cell + pad) - pad, gh * (cell + pad) - pad);
    }

    private void EqCellAt(Vector2 m, float eqLeft, float eqTop, float cell, out int cx, out int cy)
    {
        cx = Mathf.Clamp(Mathf.FloorToInt((m.x - eqLeft - pad) / (cell + pad)), 0, Equipment.GridW - 1);
        cy = Mathf.Clamp(Mathf.FloorToInt((m.y - eqTop - pad) / (cell + pad)), 0, Equipment.GridH - 1);
    }

    private void EqAnchor(Vector2 m, float eqLeft, float eqTop, float cell, int w, int h, out int ax, out int ay)
    {
        EqCellAt(m, eqLeft, eqTop, cell, out int cx, out int cy);
        ax = Mathf.Clamp(cx - (w - 1) / 2, 0, Mathf.Max(0, Equipment.GridW - w));
        ay = Mathf.Clamp(cy - (h - 1) / 2, 0, Mathf.Max(0, Equipment.GridH - h));
    }

    // 장신구 그리드 클릭: 집기(해제) / 발자국 배치 / 1:1 교체
    private void HandleEquipGridClick(Vector2 m, Rect eqRect, float cell)
    {
        var eq = Equipment.Instance;
        if (eq == null) return;
        float eqLeft = eqRect.x, eqTop = eqRect.y;

        if (heldItem == null)
        {
            EqCellAt(m, eqLeft, eqTop, cell, out int cx, out int cy);
            var e = eq.EntryAt(cx, cy);
            if (e != null) { heldItem = e.item; heldCount = 1; heldRot = e.rot; eq.Remove(e); Inventory.Instance.RaiseChanged(); }
            return;
        }

        if (heldItem.kind != ItemData.ItemKind.Equipment || heldCount != 1) return;   // 장신구 1개 단위만 장착
        EqAnchor(m, eqLeft, eqTop, cell, HeldW, HeldH, out int ax, out int ay);

        if (eq.CanPlace(heldItem, ax, ay, heldRot))
        {
            eq.Place(heldItem, ax, ay, heldRot);
            heldItem = null; heldCount = 0; heldRot = 0;
            Inventory.Instance.RaiseChanged();
            return;
        }

        // 겹치는 착용품이 정확히 1개면 교체
        var overlaps = new System.Collections.Generic.List<Equipment.Worn>();
        foreach (var s in eq.worn)
        {
            if (s == null || s.item == null) continue;
            if (ax < s.x + s.W && s.x < ax + HeldW && ay < s.y + s.H && s.y < ay + HeldH)
                overlaps.Add(s);
        }
        if (overlaps.Count != 1) return;
        var t = overlaps[0];
        if (eq.CanPlace(heldItem, ax, ay, heldRot, t))
        {
            var ti = t.item; int tr = t.rot;
            eq.Remove(t);
            eq.Place(heldItem, ax, ay, heldRot);
            heldItem = ti; heldCount = 1; heldRot = tr;
            Inventory.Instance.RaiseChanged();
        }
    }

    private void DrawIcon(Rect r, ItemData item, int count, bool dim, int rot = 0)
    {
        var prev = GUI.color;
        if (dim) GUI.color = new Color(1f, 1f, 1f, 0.30f);
        if (item.icon != null)
        {
            rot &= 3;
            if (rot != 0)
            {
                // rot*90도 회전해 그리기(홀수 단계는 발자국이 스왑됐으니 그리기 사각형도 스왑해서 중앙 정렬)
                var mtx = GUI.matrix;
                GUIUtility.RotateAroundPivot(rot * 90f, r.center);
                Rect rr = (rot & 1) == 1
                    ? new Rect(r.center.x - r.height * 0.5f + 4, r.center.y - r.width * 0.5f + 4, r.height - 8, r.width - 8)
                    : new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8);
                GUI.DrawTexture(rr, item.icon.texture, ScaleMode.ScaleToFit);
                GUI.matrix = mtx;
            }
            else GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), item.icon.texture, ScaleMode.ScaleToFit);
        }
        else GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), item.itemName, itemLabelStyle);
        GUI.color = prev;
        if (count > 1) GUI.Label(new Rect(r.x, r.y, r.width - 4, r.height - 2), count.ToString(), countStyle);
    }

    private void DrawTooltip(ItemData item, Vector2 mouse)
    {
        float tw = Mathf.Max(120f, tooltipWidth);
        tipNameStyle.normal.textColor = item.RarityColor();   // 희귀도 색으로 이름 표시
        string name = item.itemName;
        string desc = item.description;
        string size = item.GridW + "×" + item.GridH;
        float nameH = tipNameStyle.CalcHeight(new GUIContent(name), tw - 16);
        float descH = string.IsNullOrEmpty(desc) ? 0f : tipDescStyle.CalcHeight(new GUIContent(desc), tw - 16);
        float th = nameH + descH + 34;

        float tx = mouse.x + 18, ty = mouse.y + 18;
        if (tx + tw > UIScale.W) tx = UIScale.W - tw - 4;
        if (ty + th > UIScale.H) ty = UIScale.H - th - 4;

        Rect tr = new Rect(tx, ty, tw, th);
        UITheme.Shadow(tr, 12f, 0.35f);
        UITheme.FillV(tr, UITheme.PanelTop, UITheme.PanelBot);
        UITheme.Border2(tr, 2f, UITheme.A(item.RarityColor(), 0.85f));       // 희귀도 테두리
        UITheme.Fill(new Rect(tr.x + 2f, tr.y + 2f, tr.width - 4f, 3f), item.RarityColor());   // 상단 희귀도 바
        GUI.Label(new Rect(tx + 8, ty + 7, tw - 16, nameH), name, tipNameStyle);
        if (descH > 0) GUI.Label(new Rect(tx + 8, ty + 7 + nameH, tw - 16, descH), desc, tipDescStyle);
        GUI.Label(new Rect(tx + 8, ty + th - 24, tw - 16, 18), "크기 " + size, tabStyle);
    }

    // 좌측 세로 탭(후드/배낭)
    private void DrawSideTab(Rect r, string label, bool sel)
    {
        if (sel) UITheme.Glow(r, UITheme.Accent, 5f, 0.24f);
        UITheme.FillV(r, sel ? UITheme.Lighten(UITheme.PanelTop, 0.04f) : UITheme.SlotTop, sel ? UITheme.PanelBot : UITheme.SlotBot);
        UITheme.Border2(r, sel ? 2.5f : 1.5f, sel ? UITheme.Accent : UITheme.Border);
        if (sel) UITheme.Fill(new Rect(r.x, r.y, 3f, r.height), UITheme.Accent);   // 좌측 강조바
        GUI.Label(r, label, sel ? tabSelStyle : tabStyle);
    }

    // 스탯 설명 툴팁
    private void DrawStatTooltip(string text, Vector2 m)
    {
        float tw = 290f;
        float th = tipDescStyle.CalcHeight(new GUIContent(text), tw - 16f) + 14f;
        float tx = m.x + 16f, ty = m.y + 16f;
        if (tx + tw > UIScale.W) tx = UIScale.W - tw - 4f;
        if (ty + th > UIScale.H) ty = UIScale.H - th - 4f;
        Rect tr = new Rect(tx, ty, tw, th);
        UITheme.Shadow(tr, 10f, 0.32f);
        UITheme.FillV(tr, UITheme.PanelTop, UITheme.PanelBot);
        UITheme.Border2(tr, 2f, UITheme.Border);
        GUI.Label(new Rect(tx + 8f, ty + 6f, tw - 16f, th - 12f), text, tipDescStyle);
    }

    // 후드 탭: [기프트/모듈] 서브탭 + 레벨(중앙) + (모듈) 캐릭터 자리 + 우측 스탯 강화
    private void DrawHoodPanel(Rect win, float topBarH)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        Vector2 mp = Event.current.mousePosition;
        bool md = Event.current.type == EventType.MouseDown && Event.current.button == 0;
        float hpad = 14f, cx = win.x + hpad, cy = win.y + topBarH + 8f, cw = win.width - hpad * 2f;

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
        Rect bar = new Rect(cx, topY, cw, 14f);
        UITheme.FillV(bar, UITheme.SlotBot, UITheme.SlotTop); UITheme.Border2(bar, 1f, UITheme.Border);
        float frac = gm.XpToNext > 0 ? (float)gm.xp / gm.XpToNext : 0f;
        UITheme.FillV(new Rect(bar.x + 2f, bar.y + 2f, (cw - 4f) * Mathf.Clamp01(frac), bar.height - 4f), UITheme.Lighten(UITheme.Accent, 0.08f), UITheme.AccentDim);
        GUI.Label(bar, "XP " + gm.xp + " / " + gm.XpToNext, tabStyle);
        float bodyY = topY + 20f, bodyH = win.yMax - 14f - bodyY;

        if (hoodSubTab == 1)   // ── 기프트 탭(추후) ──
        {
            Rect g = new Rect(cx, bodyY, cw, bodyH);
            UITheme.FillV(g, UITheme.SlotTop, UITheme.SlotBot); UITheme.Border2(g, 2f, UITheme.Border);
            GUI.Label(g, "[???]", hoodCenter);
            return;
        }

        // ── 모듈 탭: 왼쪽 캐릭터 자리 + 오른쪽 스탯 ──
        float spriteW = cw * 0.36f;
        Rect spriteBox = new Rect(cx, bodyY, spriteW, bodyH);
        UITheme.FillV(spriteBox, UITheme.SlotTop, UITheme.SlotBot); UITheme.Border2(spriteBox, 2f, UITheme.Border);
        GUI.Label(spriteBox, "캐릭터\n(레드 후드)\n— 추후 —", tabStyle);

        float sx = cx + spriteW + 12f, sw = cw - spriteW - 12f;
        int bagDim = Inventory.Instance != null ? Inventory.Instance.gridWidth : 4;
        string[] names = { "체력 모듈", "재생력 모듈", "공격력 모듈", "적응력 모듈", "행운 모듈", "미니맵 모듈", "주머니 확장" };
        string[] descs = {
            "체력 한 칸씩 증가", "체력 자동 재생력 증가",
            "물리 공격력 증가", "마법 공격력 + 기프트 효율 상승", "골드 획득량 + 전리품 획득량 + 채집물 조우 확률 상승",
            "미니맵 — 엔지니어가 망토 수리 시 장착해 줌. 탐험해 발견한 구역의 상자·출구·적·채집물 표시. [,] 토글 · 지도는 [M]",
            "소지품 칸 확장 — 현재 " + bagDim + "×" + bagDim + " (4×4 → 5×5 → 6×6)"
        };
        int[] levels = { Mathf.Max(0, gm.maxHearts - 3), gm.statRegen, gm.statAttack, gm.statAdapt, gm.statLuck, gm.moduleMinimap, gm.bagLevel };
        int[] costs = { 5, 2, 1, 1, 2, 3, 4 };
        int[] maxLv = { 99, 99, 99, 99, 99, 1, GameManager.MaxBagLevel };
        int rows = names.Length;
        float rh = Mathf.Clamp(bodyH / rows, 22f, 40f);
        string hoverDesc = null;
        for (int i = 0; i < rows; i++)
        {
            Rect row = new Rect(sx, bodyY + i * rh, sw, rh - 4f);
            UITheme.FillV(row, row.Contains(mp) ? UITheme.Lighten(UITheme.SlotTop, 0.04f) : UITheme.SlotTop, UITheme.SlotBot);
            UITheme.Border2(row, 1f, UITheme.Border);
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
                UITheme.FillV(plus, UITheme.SlotTop, UITheme.SlotBot); UITheme.Border2(plus, 1.5f, UITheme.A(UITheme.Good, 0.6f));
                closeStyle.normal.textColor = UITheme.Good;
                GUI.Label(plus, "✓", closeStyle);
                closeStyle.normal.textColor = Color.white;
            }
            else if (hoodUpgrade && i != 5)   // 엔지니어 '업그레이드 모드'에서만 + 버튼 (미니맵 i==5는 망토수리로 지급 — 구매 불가)
            {
                bool can = gm.modPoints >= costs[i];
                if (can) { UITheme.FillV(plus, UITheme.Lighten(UITheme.Warm, 0.05f), UITheme.WarmDim); UITheme.Border2(plus, 2f, UITheme.Lighten(UITheme.Warm, 0.15f)); }
                else { UITheme.FillV(plus, UITheme.SlotTop, UITheme.SlotBot); UITheme.Border2(plus, 1.5f, UITheme.Border); }
                GUI.Label(plus, "+", closeStyle);
                if (can && md && plus.Contains(mp))
                {
                    if (i < 5) gm.SpendStat(i);
                    else if (i == 6) gm.TryExpandBackpack(costs[i]);    // 배낭 확장(+1열)
                    Event.current.Use();
                }
            }
            // else: C키 조회 전용 / 미니맵(지급형) 미보유 → 버튼 없음
        }
        if (hoverDesc != null) DrawStatTooltip(hoverDesc, mp);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (countStyle != null) return;

        Color cream = new Color(0.90f, 0.95f, 1f);
        Color gold  = new Color(1f, 0.85f, 0.42f);
        countStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = 14 };
        countStyle.normal.textColor = Color.white;
        tipNameStyle   = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 24 };
        tipNameStyle.normal.textColor = gold;
        tipDescStyle   = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        tipDescStyle.normal.textColor = cream;
        tabStyle       = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        tabStyle.normal.textColor = new Color(0.68f, 0.70f, 0.74f);
        tabSelStyle    = new GUIStyle(tabStyle);
        tabSelStyle.normal.textColor = Color.white;
        goldStyle      = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 15, fontStyle = FontStyle.Bold };
        goldStyle.normal.textColor = gold;
        titleStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 17, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = cream;
        hoodTitle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        hoodTitle.normal.textColor = gold;
        hoodStat = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        hoodStat.normal.textColor = cream;
        hoodCenter = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hoodCenter.normal.textColor = gold;
        itemLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, wordWrap = true };
        itemLabelStyle.normal.textColor = cream;
        closeStyle     = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        closeStyle.normal.textColor = Color.white;
        ctxStyle       = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16, fontStyle = FontStyle.Bold };
        ctxStyle.normal.textColor = UITheme.Text;
        hotkeyNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hotkeyNumStyle.normal.textColor = new Color(0.10f, 0.06f, 0.02f);   // 앰버 배지 위 짙은 글자
    }
}
