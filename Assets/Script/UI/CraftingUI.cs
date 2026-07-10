using UnityEngine;

// 제작대 UI(자동부팅·영구). CraftStation에 F로 열림 — 코어키퍼식.
//  왼쪽: 레시피 그리드(결과 아이콘, 제작 가능=밝음+초록점 / 재료 부족=흐림)
//  오른쪽: 선택 레시피 상세 — 큰 아이콘·설명 + 재료 보유/필요(색) + [제작]/[×5]
//  재료는 인벤토리에서 자동 소모(드래그 없음). 결과물이 안 들어가면 재료 환불.
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    private class Recipe { public string[] inIds; public int[] inCounts; public string outId; public bool potion; }
    private Recipe[] recipes;

    private bool open;
    private int tab;                 // 0=전체, 1=포션, 2=장신구
    private Recipe selected;

    private GUIStyle title, name_, body, sec, count, slotName, tabOn, tabOff, btnStyle, closeStyle, haveStyle;
    private Texture2D white;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        recipes = new Recipe[] {
            new Recipe { inIds = new[]{"lizard","underground_flower"},     inCounts = new[]{1,1}, outId = "heal_potion",    potion = true },
            new Recipe { inIds = new[]{"lizard","slime_condensate"},       inCounts = new[]{1,1}, outId = "defense_potion", potion = true },
            new Recipe { inIds = new[]{"slime_condensate","flame_flower"}, inCounts = new[]{1,1}, outId = "combat_potion",  potion = true },
            // 장비/도구 — 탐험가 상인 폐지: 로프는 제작으로
            new Recipe { inIds = new[]{"underground_flower","slime_condensate"},           inCounts = new[]{2,2},   outId = "escape_rope",      potion = false },
            // 장신구 — 재료 3종·다량(상점 재료만으로 싸게 못 만들게)
            new Recipe { inIds = new[]{"lizard","flame_flower","slime_condensate"},        inCounts = new[]{3,3,2}, outId = "warriors_ring",    potion = false },
            new Recipe { inIds = new[]{"underground_flower","slime_condensate","lizard"},  inCounts = new[]{4,3,2}, outId = "rune_of_vitality", potion = false },
            new Recipe { inIds = new[]{"underground_flower","flame_flower","lizard"},      inCounts = new[]{3,2,3}, outId = "charm_of_leaping", potion = false },
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("CraftingUI").AddComponent<CraftingUI>(); }

    public void Open() { open = true; selected = null; Inventory.CraftUIOpen = true; }
    public void Close() { open = false; Inventory.CraftUIOpen = false; }
    void Update() { if (open && Input.GetKeyDown(KeyCode.Escape)) Close(); }

    // ── 제작 판정/실행 ──
    private bool CanCraft(Recipe rc)
    {
        var inv = Inventory.Instance;
        if (inv == null || rc == null) return false;
        for (int i = 0; i < rc.inIds.Length; i++)
        {
            var it = ItemDatabase.Get(rc.inIds[i]);
            if (it == null || inv.CountOf(it) < rc.inCounts[i]) return false;
        }
        return true;
    }

    private bool CraftOnce(Recipe rc)
    {
        var inv = Inventory.Instance;
        if (!CanCraft(rc)) return false;
        var outIt = ItemDatabase.Get(rc.outId);
        if (outIt == null) return false;

        for (int i = 0; i < rc.inIds.Length; i++)
            inv.Remove(ItemDatabase.Get(rc.inIds[i]), rc.inCounts[i]);

        int left = inv.Add(outIt, 1);
        if (left > 0)   // 결과물 놓을 자리가 없음 → 재료 환불(방금 뺐으니 자리 보장)
        {
            for (int i = 0; i < rc.inIds.Length; i++)
                inv.Add(ItemDatabase.Get(rc.inIds[i]), rc.inCounts[i]);
            Toast.Show("소지품에 자리가 없다", 1.6f);
            return false;
        }
        return true;
    }

    private void Craft(Recipe rc, int times)
    {
        int made = 0;
        for (int n = 0; n < times; n++) { if (!CraftOnce(rc)) break; made++; }
        if (made > 0)
        {
            var outIt = ItemDatabase.Get(rc.outId);
            Toast.Show(outIt.itemName + (made > 1 ? " ×" + made : "") + " 제작 완료", 1.8f);
        }
    }

    private bool InTab(Recipe rc) => tab == 0 || (tab == 1 && rc.potion) || (tab == 2 && !rc.potion);

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;
        EnsureStyles();
        UIScale.Apply();

        float ss = Mathf.Clamp(UIScale.H * 0.062f, 48f, 68f);   // 레시피 칸
        float pad = 8f;
        int cols = 4, rowsMin = 4;
        float titleH = 40f, tabH = 32f;
        float gridW = cols * (ss + pad) + pad;
        float gridH = rowsMin * (ss + pad) + pad;
        float detailW = 380f;
        float w = pad + gridW + pad + 2f + detailW + pad;
        float bodyH = Mathf.Max(gridH, 330f);
        float h = titleH + tabH + 8f + bodyH + pad * 2f;
        float x = (UIScale.W - w) * 0.5f, y = (UIScale.H - h) * 0.5f;
        Rect win = new Rect(x, y, w, h);

        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        // 어둡게 + 패널
        UITheme.Fill(new Rect(0, 0, UIScale.W, UIScale.H), new Color(0f, 0f, 0f, 0.55f));
        UITheme.DrawPanel(win);
        UITheme.DrawHeader(win, "제작대", null, 16f, titleH);

        // 닫기 ✕
        Rect cb = new Rect(x + w - 34f, y + 9f, 26f, 26f);
        bool cbh = cb.Contains(m);
        UITheme.FillV(cb, cbh ? UITheme.Lighten(new Color(0.82f, 0.30f, 0.30f), 0.12f) : new Color(0.82f, 0.30f, 0.30f), new Color(0.40f, 0.12f, 0.14f));
        UITheme.Border2(cb, 1.5f, UITheme.A(new Color(0.82f, 0.30f, 0.30f), 0.75f));
        GUI.Label(cb, "✕", closeStyle);
        if (click && cb.Contains(m)) { Close(); Event.current.Use(); return; }

        // 탭
        string[] tabs = { "전체", "포션", "장비" };
        float tw = 92f, ty = y + titleH + 2f;
        for (int i = 0; i < tabs.Length; i++)
        {
            Rect tr = new Rect(x + pad + i * (tw + 6f), ty, tw, tabH - 4f);
            bool on = tab == i;
            if (on)
            {
                UITheme.FillV(tr, UITheme.Lighten(UITheme.Accent, 0.05f), UITheme.AccentDim);
                UITheme.Border2(tr, 1.5f, UITheme.Lighten(UITheme.Accent, 0.2f));
            }
            else
            {
                UITheme.FillV(tr, tr.Contains(m) ? UITheme.Lighten(UITheme.SlotTop, 0.05f) : UITheme.SlotTop, UITheme.SlotBot);
                UITheme.Border2(tr, 1f, UITheme.Border);
            }
            GUI.Label(tr, tabs[i], on ? tabOn : tabOff);
            if (click && tr.Contains(m)) { tab = i; selected = null; Event.current.Use(); }
        }
        UITheme.Divider(x + pad, ty + tabH + 1f, w - pad * 2f, 0.35f);   // 탭 아래 장식 구분선

        float bodyY = ty + tabH + 6f;

        // ══ 왼쪽: 레시피 그리드 ══
        Rect gridArea = new Rect(x + pad, bodyY, gridW, bodyH);
        UITheme.FillV(gridArea, UITheme.A(UITheme.SlotBot, 0.55f), UITheme.A(UITheme.SlotBot, 0.85f));
        UITheme.Border2(gridArea, 1f, UITheme.A(UITheme.Border, 0.5f));

        ItemData hover = null;
        int idx = 0;
        foreach (var rc in recipes)
        {
            if (!InTab(rc)) continue;
            var outIt = ItemDatabase.Get(rc.outId);
            if (outIt == null) continue;
            int r = idx / cols, c = idx % cols;
            Rect slot = new Rect(gridArea.x + pad + c * (ss + pad), gridArea.y + pad + r * (ss + pad), ss, ss);
            bool can = CanCraft(rc);
            bool sel = selected == rc;
            bool hv = slot.Contains(m);

            if (sel) UITheme.Glow(slot, UITheme.Accent, 5f, 0.30f);
            UITheme.DrawSlot(slot, sel ? UITheme.Accent : UITheme.Border, hv, sel ? 2.5f : 1.5f);
            UITheme.RarityRing(slot, outIt);

            var prev = GUI.color;
            if (!can) GUI.color = new Color(1f, 1f, 1f, 0.32f);   // 재료 부족 = 흐림
            if (outIt.icon != null) GUI.DrawTexture(new Rect(slot.x + 6, slot.y + 6, slot.width - 12, slot.height - 12), outIt.icon.texture, ScaleMode.ScaleToFit);
            else GUI.Label(slot, outIt.itemName, slotName);
            GUI.color = prev;

            if (can) UITheme.Fill(new Rect(slot.xMax - 10f, slot.y + 4f, 6f, 6f), UITheme.Good);   // 제작 가능 = 초록점

            if (hv) hover = outIt;
            if (click && slot.Contains(m)) { selected = rc; Event.current.Use(); }
            idx++;
        }
        if (idx == 0) GUI.Label(new Rect(gridArea.x + 12f, gridArea.y + 12f, gridW - 24f, 40f), "이 분류의 레시피가 없다.", body);

        // 세로 구분선
        UITheme.Fill(new Rect(gridArea.xMax + pad, bodyY, 2f, bodyH), UITheme.A(UITheme.Border, 0.55f));

        // ══ 오른쪽: 선택 레시피 상세 ══
        Rect det = new Rect(gridArea.xMax + pad + 2f + pad, bodyY, detailW - pad, bodyH);
        if (selected == null)
        {
            GUI.Label(new Rect(det.x, det.y + bodyH * 0.42f, det.width, 30f), "왼쪽에서 레시피를 선택하세요", sec);
        }
        else
        {
            var outIt = ItemDatabase.Get(selected.outId);
            if (outIt != null)
            {
                // 결과: 큰 아이콘 + 이름 + 설명
                float bigS = 76f;
                Rect big = new Rect(det.x, det.y + 4f, bigS, bigS);
                UITheme.DrawSlot(big, UITheme.Border, false, 1.5f);
                UITheme.RarityRing(big, outIt);
                if (outIt.icon != null) GUI.DrawTexture(new Rect(big.x + 7, big.y + 7, bigS - 14, bigS - 14), outIt.icon.texture, ScaleMode.ScaleToFit);

                name_.normal.textColor = outIt.RarityColor();
                GUI.Label(new Rect(big.xMax + 12f, det.y + 8f, det.width - bigS - 14f, 28f), outIt.itemName, name_);
                GUI.Label(new Rect(big.xMax + 12f, det.y + 38f, det.width - bigS - 14f, bigS - 34f), outIt.description ?? "", body);

                // 구분선 + 재료 목록
                float ry = det.y + bigS + 18f;
                UITheme.Fill(new Rect(det.x, ry - 8f, det.width - 8f, 1f), UITheme.A(UITheme.Border, 0.5f));
                GUI.Label(new Rect(det.x, ry, 100f, 22f), "필요 재료", sec);
                ry += 26f;

                var inv = Inventory.Instance;
                for (int i = 0; i < selected.inIds.Length; i++)
                {
                    var it = ItemDatabase.Get(selected.inIds[i]);
                    int need = selected.inCounts[i];
                    int have = it != null ? inv.CountOf(it) : 0;
                    bool ok = have >= need;

                    Rect row = new Rect(det.x, ry, det.width - 8f, 34f);
                    UITheme.FillV(row, row.Contains(m) ? UITheme.Lighten(UITheme.SlotTop, 0.04f) : UITheme.SlotTop, UITheme.SlotBot);
                    UITheme.Border2(row, 1f, UITheme.A(UITheme.Border, 0.6f));

                    Rect ic = new Rect(row.x + 4f, row.y + 4f, 26f, 26f);
                    if (it != null && it.icon != null) GUI.DrawTexture(ic, it.icon.texture, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(ic.xMax + 8f, row.y, row.width * 0.5f, row.height), it != null ? it.itemName : selected.inIds[i], body);

                    haveStyle.normal.textColor = ok ? UITheme.Good : UITheme.Danger;
                    GUI.Label(new Rect(row.xMax - 96f, row.y, 88f, row.height), have + " / " + need, haveStyle);

                    if (row.Contains(m) && it != null) hover = it;
                    ry += 38f;
                }

                // 제작 버튼
                bool canCraft = CanCraft(selected);
                Rect b1 = new Rect(det.x, det.yMax - 46f, 150f, 40f);
                Rect b5 = new Rect(det.x + 158f, det.yMax - 46f, 90f, 40f);
                if (DrawButton(b1, "제  작", canCraft, m, click)) { Craft(selected, 1); Event.current.Use(); }
                if (DrawButton(b5, "× 5", canCraft, m, click)) { Craft(selected, 5); Event.current.Use(); }
            }
        }

        if (hover != null) DrawTip(hover, m);
    }

    // 버튼(활성=오렌지, 비활성=회색). 클릭되면 true.
    private bool DrawButton(Rect r, string label, bool enabled, Vector2 m, bool click)
    {
        bool hv = enabled && r.Contains(m);
        if (enabled)
        {
            if (hv) UITheme.Glow(r, UITheme.Accent, 4f, 0.25f);
            UITheme.FillV(r, UITheme.Lighten(UITheme.Accent, hv ? 0.10f : 0.03f), UITheme.AccentDim);
            UITheme.Border2(r, 1.5f, UITheme.Lighten(UITheme.Accent, 0.2f));
        }
        else
        {
            UITheme.FillV(r, UITheme.SlotTop, UITheme.SlotBot);
            UITheme.Border2(r, 1f, UITheme.Border);
        }
        var st = enabled ? tabOn : tabOff;
        GUI.Label(r, label, st);
        return enabled && click && r.Contains(m);
    }

    private GUIStyle tipSub;   // 등급명 전용(다른 스타일 색 오염 방지)
    private void DrawTip(ItemData it, Vector2 m)
    {
        if (tipSub == null) tipSub = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        float tw = 290f;
        Color rc = it.RarityColor();
        name_.normal.textColor = rc;
        string nm = it.itemName, desc = it.description;
        float nh = name_.CalcHeight(new GUIContent(nm), tw - 20f);
        float dh = string.IsNullOrEmpty(desc) ? 0f : body.CalcHeight(new GUIContent(desc), tw - 20f);
        float th = 10f + nh + 9f + (dh > 0f ? dh + 4f : 0f) + 8f;
        float tx = m.x + 16f, tyy = m.y + 16f;
        if (tx + tw > UIScale.W) tx = UIScale.W - tw - 4f;
        if (tyy + th > UIScale.H) tyy = UIScale.H - th - 4f;
        Rect tr = new Rect(tx, tyy, tw, th);
        UITheme.TipFrame(tr, rc);   // 금테 + 상단 희귀도 라인 + 코너 캡

        float cy = tyy + 10f;
        GUI.Label(new Rect(tx + 10f, cy, tw - 20f, nh), nm, name_);
        tipSub.normal.textColor = UITheme.A(rc, 0.9f);
        GUI.Label(new Rect(tx + 10f, cy + 3f, tw - 22f, 18f), UITheme.RarityName(it.rarity), tipSub);
        cy += nh + 3f;
        UITheme.Fill(new Rect(tx + 10f, cy, tw - 20f, 1f), UITheme.A(UITheme.Border, 0.5f));
        cy += 6f;
        if (dh > 0f) GUI.Label(new Rect(tx + 10f, cy, tw - 20f, dh), desc, body);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (title != null) return;
        Color cream = new Color(0.90f, 0.95f, 1f), goldC = new Color(1f, 0.85f, 0.42f);
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; title.normal.textColor = cream;
        name_ = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
        sec = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; sec.normal.textColor = new Color(0.68f, 0.70f, 0.74f);
        body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true }; body.normal.textColor = cream;
        count = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight }; count.normal.textColor = Color.white;
        slotName = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true }; slotName.normal.textColor = cream;
        tabOn = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; tabOn.normal.textColor = Color.white;
        tabOff = new GUIStyle(tabOn); tabOff.normal.textColor = new Color(0.68f, 0.70f, 0.74f);
        closeStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; closeStyle.normal.textColor = Color.white;
        haveStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
    }
}
