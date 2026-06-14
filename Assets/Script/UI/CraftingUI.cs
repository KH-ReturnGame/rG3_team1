using UnityEngine;

// 제작대 UI(자동부팅·영구). CraftStation에 F로 열림.
//  왼쪽: 플레이어 인벤토리 / 오른쪽: 제작대(일반·포션 탭)
//  포션 탭: 재료 2칸에 재료를 넣으면 결과칸에 포션 미리보기 → 결과칸 클릭 시 제작(손에 집힘).
//  일반 탭: 포션이 아닌 제작(현재 레시피 없음 — 추후 추가).
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    private class Recipe { public string[] inIds; public int[] inCounts; public string outId; public bool potion; }
    private Recipe[] recipes;

    private bool open;
    private int tab;                                    // 0=일반, 1=포션
    private ItemData held; private int heldCount;       // 마우스에 집은 것
    private readonly ItemData[] inItem = new ItemData[2];   // 재료 2칸
    private readonly int[] inCount = new int[2];

    private GUIStyle title, sec, tipName, body, count, slotName, tabOn, tabOff, heldNum, arrow;
    private Texture2D white;
    private const int Cols = 6, Rows = 4, Cap = 24;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        recipes = new Recipe[] {
            new Recipe { inIds = new[]{"lizard","underground_flower"},     inCounts = new[]{1,1}, outId = "heal_potion",    potion = true },
            new Recipe { inIds = new[]{"lizard","slime_condensate"},       inCounts = new[]{1,1}, outId = "defense_potion", potion = true },
            new Recipe { inIds = new[]{"slime_condensate","flame_flower"}, inCounts = new[]{1,1}, outId = "combat_potion",  potion = true },
            // 일반 탭(비포션) — 재료 3종·다량으로 장신구 제작(상점 재료만으로 싸게 못 만들게)
            new Recipe { inIds = new[]{"lizard","flame_flower","slime_condensate"},        inCounts = new[]{3,3,2}, outId = "warriors_ring",    potion = false },
            new Recipe { inIds = new[]{"underground_flower","slime_condensate","lizard"},  inCounts = new[]{4,3,2}, outId = "rune_of_vitality", potion = false },
            new Recipe { inIds = new[]{"underground_flower","flame_flower","lizard"},      inCounts = new[]{3,2,3}, outId = "charm_of_leaping", potion = false },
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("CraftingUI").AddComponent<CraftingUI>(); }

    public void Open() { open = true; Inventory.CraftUIOpen = true; }
    public void Close() { ReturnHeld(); ReturnInputs(); open = false; Inventory.CraftUIOpen = false; }
    void Update() { if (open && Input.GetKeyDown(KeyCode.Escape)) Close(); }

    private void ReturnHeld() { if (held != null && Inventory.Instance != null) Inventory.Instance.Add(held, heldCount); held = null; heldCount = 0; }
    private void ReturnInputs()
    {
        for (int k = 0; k < 2; k++)
        {
            if (inItem[k] != null && Inventory.Instance != null) Inventory.Instance.Add(inItem[k], inCount[k]);
            inItem[k] = null; inCount[k] = 0;
        }
    }

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;
        EnsureStyles();
        float ss = Mathf.Clamp(Screen.height * 0.072f, 44f, 70f);
        float pad = 6f, gap = 36f, headH = 104f;
        float panelW = Cols * (ss + pad) + pad;
        float gridH = Rows * (ss + pad) + pad;
        float w = panelW * 2f + gap + 40f;
        float h = headH + gridH + 64f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = new Color(0.13f, 0.10f, 0.08f, 0.99f); GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = new Color(0.86f, 0.63f, 0.30f); GUI.DrawTexture(new Rect(x, y, w, 4f), white); GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 12f, w, 32f), "제작대", title);

        // 탭
        float tw = 96f, tabh = 32f, ty = y + 48f;
        Rect t0 = new Rect(x + 20f, ty, tw, tabh), t1 = new Rect(x + 20f + tw + 6f, ty, tw, tabh);
        DrawTabBtn(t0, "일반", tab == 0); DrawTabBtn(t1, "포션", tab == 1);
        if (click && t0.Contains(m)) { tab = 0; Event.current.Use(); }
        if (click && t1.Contains(m)) { tab = 1; Event.current.Use(); }

        float gy = y + headH;
        float lx = x + 20f, rx = x + 20f + panelW + gap;
        GUI.Label(new Rect(lx, gy - 22f, panelW, 20f), "인벤토리", sec);
        GUI.Label(new Rect(rx, gy - 22f, panelW + 40f, 20f), tab == 1 ? "포션 제작" : "일반 제작", sec);

        ItemData hover = null;

        // 왼쪽 인벤토리
        var inv = Inventory.Instance.slots;
        for (int i = 0; i < Cap; i++)
        {
            Rect r = SlotRect(lx, gy, i, ss, pad);
            DrawSlotBg(r, false);
            ItemData it = (i < inv.Count && inv[i] != null && !inv[i].IsEmpty) ? inv[i].item : null;
            int c = it != null ? inv[i].count : 0;
            if (it != null) { DrawItem(r, it, c, ss); if (held == null && r.Contains(m)) hover = it; }
            if (click && r.Contains(m)) { ClickInv(i); Event.current.Use(); }
        }

        // 오른쪽: 포션 탭 = 2재료 제작 / 일반 탭 = 레시피 목록(원래 방식)
        if (tab == 1) DrawPotionCraft(rx, panelW, gy, ss, m, click, ref hover);
        else DrawGeneralList(rx, panelW, gy, m, click);

        if (GUI.Button(new Rect(x + w - 130f, y + h - 46f, 110f, 34f), "닫기")) { Close(); return; }

        if (hover != null) DrawTip(hover, m);
        DrawHeld(m, ss);
    }

    // ── 클릭 처리 ──
    private void ClickInv(int i)   // 클릭한 '그 칸'에 집기/놓기/스택/교체
    {
        var inv = Inventory.Instance;
        if (inv == null || i < 0 || i >= inv.slots.Count) return;
        var s = inv.slots[i];
        if (held == null) { if (!s.IsEmpty) { held = s.item; heldCount = s.count; s.Clear(); inv.RaiseChanged(); } }
        else if (s.IsEmpty) { s.item = held; s.count = heldCount; held = null; heldCount = 0; inv.RaiseChanged(); }
        else if (s.item == held) { int sp = Mathf.Max(1, held.maxStack) - s.count; int mv = Mathf.Min(sp, heldCount); s.count += mv; heldCount -= mv; if (heldCount <= 0) { held = null; heldCount = 0; } inv.RaiseChanged(); }
        else { var ti = s.item; int tc = s.count; s.item = held; s.count = heldCount; held = ti; heldCount = tc; inv.RaiseChanged(); }
    }

    private void ClickInput(int k)
    {
        if (held == null) { if (inItem[k] != null) { held = inItem[k]; heldCount = inCount[k]; inItem[k] = null; inCount[k] = 0; } }
        else if (inItem[k] == null) { inItem[k] = held; inCount[k] = heldCount; held = null; heldCount = 0; }
        else if (inItem[k] == held) { int sp = Mathf.Max(1, held.maxStack) - inCount[k]; int mv = Mathf.Min(sp, heldCount); inCount[k] += mv; heldCount -= mv; if (heldCount <= 0) { held = null; heldCount = 0; } }
        else { var ti = inItem[k]; int tc = inCount[k]; inItem[k] = held; inCount[k] = heldCount; held = ti; heldCount = tc; }
    }

    private void CraftOutput(ItemData outItem)
    {
        if (outItem == null || inCount[0] < 1 || inCount[1] < 1) return;
        if (held != null && held != outItem) return;
        if (held == outItem && heldCount >= Mathf.Max(1, outItem.maxStack)) return;
        inCount[0]--; if (inCount[0] <= 0) inItem[0] = null;
        inCount[1]--; if (inCount[1] <= 0) inItem[1] = null;
        if (held == null) { held = outItem; heldCount = 1; } else heldCount++;
    }

    private Recipe MatchRecipe()
    {
        if (inItem[0] == null || inItem[1] == null) return null;
        foreach (var rc in recipes)
        {
            if (rc.potion != (tab == 1) || rc.inIds.Length != 2) continue;
            var ra = ItemDatabase.Get(rc.inIds[0]); var rb = ItemDatabase.Get(rc.inIds[1]);
            if ((ra == inItem[0] && rb == inItem[1]) || (ra == inItem[1] && rb == inItem[0])) return rc;
        }
        return null;
    }

    // 포션 탭: 재료 2칸 → 결과칸 (오른쪽 패널 중앙으로 배치)
    private void DrawPotionCraft(float rx, float panelW, float gy, float ss, Vector2 m, bool click, ref ItemData hover)
    {
        float bigSS = ss * 1.15f;
        float clusterW = bigSS + 56f + bigSS;
        float inX = rx + Mathf.Max(20f, (panelW - clusterW) * 0.5f);   // 전체적으로 오른쪽(중앙)으로
        float in0Y = gy + 30f, in1Y = in0Y + bigSS + 20f;
        Rect r0 = new Rect(inX, in0Y, bigSS, bigSS);
        Rect r1 = new Rect(inX, in1Y, bigSS, bigSS);
        Rect ro = new Rect(inX + bigSS + 56f, (in0Y + in1Y) * 0.5f, bigSS, bigSS);
        GUI.Label(new Rect(inX + bigSS + 6f, (in0Y + in1Y) * 0.5f, 48f, bigSS), ">", arrow);

        for (int k = 0; k < 2; k++)
        {
            Rect r = k == 0 ? r0 : r1;
            DrawSlotBg(r, true);
            if (inItem[k] != null) { DrawItem(r, inItem[k], inCount[k], bigSS); if (held == null && r.Contains(m)) hover = inItem[k]; }
            else { slotName.normal.textColor = new Color(0.6f, 0.55f, 0.45f); GUI.Label(r, "재료", slotName); }
            if (click && r.Contains(m)) { ClickInput(k); Event.current.Use(); }
        }

        var rc = MatchRecipe();
        ItemData outItem = rc != null ? ItemDatabase.Get(rc.outId) : null;
        DrawSlotBg(ro, true);
        Border(ro, 3f, new Color(0.95f, 0.8f, 0.35f));      // 결과칸 강조
        if (outItem != null)
        {
            DrawItem(ro, outItem, 0, bigSS);
            if (held == null && ro.Contains(m)) hover = outItem;
            if (click && ro.Contains(m)) { CraftOutput(outItem); Event.current.Use(); }
        }
    }

    // 일반 탭: 비포션 레시피 목록(원래 방식 — 인벤에서 재료 자동 소모 + 제작 버튼)
    private void DrawGeneralList(float rx, float panelW, float gy, Vector2 m, bool click)
    {
        var inv = Inventory.Instance;
        float ry = gy + 12f;
        int shown = 0;
        foreach (var rc in recipes)
        {
            if (rc.potion) continue;
            var outIt = ItemDatabase.Get(rc.outId);
            bool can = inv != null;
            string req = "";
            for (int i = 0; i < rc.inIds.Length; i++)
            {
                var it = ItemDatabase.Get(rc.inIds[i]);
                int have = (inv != null && it != null) ? inv.CountOf(it) : 0;
                if (have < rc.inCounts[i]) can = false;
                req += (i > 0 ? " + " : "") + (it != null ? it.itemName : rc.inIds[i]) + "(" + have + "/" + rc.inCounts[i] + ")";
            }
            GUI.Label(new Rect(rx + 14f, ry, panelW + 20f, 24f), outIt != null ? outIt.itemName : rc.outId, sec);
            GUI.Label(new Rect(rx + 14f, ry + 24f, panelW + 20f, 22f), req, body);
            GUI.enabled = can;
            if (GUI.Button(new Rect(rx + panelW - 104f, ry + 6f, 92f, 40f), can ? "제작" : "제작 불가")) CraftFromList(rc);
            GUI.enabled = true;
            ry += 64f; shown++;
        }
        if (shown == 0)
            GUI.Label(new Rect(rx + 14f, gy + 40f, panelW + 26f, 60f), "일반 제작 레시피가 아직 없습니다.\n(포션은 '포션' 탭에서 제작)", body);
    }

    private void CraftFromList(Recipe rc)
    {
        var inv = Inventory.Instance; if (inv == null) return;
        for (int i = 0; i < rc.inIds.Length; i++)
        { var it = ItemDatabase.Get(rc.inIds[i]); if (it == null || inv.CountOf(it) < rc.inCounts[i]) return; }
        for (int i = 0; i < rc.inIds.Length; i++)
            inv.Remove(ItemDatabase.Get(rc.inIds[i]), rc.inCounts[i]);
        var outIt = ItemDatabase.Get(rc.outId);
        if (outIt != null) inv.Add(outIt, 1);
    }

    // ── 그리기 ──
    private Rect SlotRect(float ox, float oy, int i, float ss, float pad)
    { int r = i / Cols, c = i % Cols; return new Rect(ox + pad + c * (ss + pad), oy + pad + r * (ss + pad), ss, ss); }

    private void DrawHeld(Vector2 m, float ss)
    {
        if (held == null) return;
        float hs = ss * 0.78f;
        Rect ir = new Rect(m.x + 12f, m.y + 6f, hs, hs);
        if (held.icon != null) GUI.DrawTexture(ir, held.icon.texture, ScaleMode.ScaleToFit);
        else { slotName.normal.textColor = held.RarityColor(); GUI.Label(ir, held.itemName, slotName); }
        if (heldCount > 1)
        {
            string cs = heldCount.ToString();
            Rect sr = new Rect(ir.xMax + 3f, ir.y + hs * 0.28f, 80f, 28f);
            heldNum.normal.textColor = Color.black; GUI.Label(new Rect(sr.x + 1f, sr.y + 1f, sr.width, sr.height), cs, heldNum);
            heldNum.normal.textColor = Color.white; GUI.Label(sr, cs, heldNum);
        }
    }

    private void DrawTabBtn(Rect r, string label, bool on)
    {
        Fill(r, on ? new Color(0.86f, 0.63f, 0.30f) : new Color(0.22f, 0.18f, 0.13f));
        Border(r, 2f, new Color(0.5f, 0.4f, 0.28f));
        GUI.Label(r, label, on ? tabOn : tabOff);
    }

    private void DrawSlotBg(Rect r, bool accent)
    {
        Fill(r, new Color(0.20f, 0.16f, 0.12f, 0.95f));
        Border(r, 2f, accent ? new Color(0.85f, 0.55f, 0.25f) : new Color(0.45f, 0.38f, 0.28f));
    }

    private void DrawItem(Rect r, ItemData it, int cnt, float ss)
    {
        Rect inner = new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f);
        if (it.icon != null) GUI.DrawTexture(inner, it.icon.texture, ScaleMode.ScaleToFit);
        else { slotName.normal.textColor = it.RarityColor(); GUI.Label(inner, it.itemName, slotName); }
        if (cnt > 1)
        {
            Rect cr = new Rect(r.x, r.y, r.width - 5f, r.height - 4f);
            count.normal.textColor = Color.black; GUI.Label(new Rect(cr.x + 1f, cr.y + 1f, cr.width, cr.height), cnt.ToString(), count);
            count.normal.textColor = Color.white; GUI.Label(cr, cnt.ToString(), count);
        }
    }

    private void DrawTip(ItemData it, Vector2 m)
    {
        float tw = 290f;
        tipName.normal.textColor = it.RarityColor();
        string nm = it.itemName, desc = it.description;
        float nh = tipName.CalcHeight(new GUIContent(nm), tw - 16f);
        float dh = string.IsNullOrEmpty(desc) ? 0f : body.CalcHeight(new GUIContent(desc), tw - 16f);
        float th = nh + dh + 16f;
        float tx = m.x + 16f, tyy = m.y + 16f;
        if (tx + tw > Screen.width) tx = Screen.width - tw - 4f;
        if (tyy + th > Screen.height) tyy = Screen.height - th - 4f;
        Rect tr = new Rect(tx, tyy, tw, th);
        Fill(tr, new Color(0.10f, 0.08f, 0.06f, 0.98f)); Border(tr, 2f, new Color(0.86f, 0.63f, 0.30f));
        GUI.Label(new Rect(tx + 8f, tyy + 5f, tw - 16f, nh), nm, tipName);
        if (dh > 0f) GUI.Label(new Rect(tx + 8f, tyy + 5f + nh, tw - 16f, dh), desc, body);
    }

    private void Fill(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }
    private void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (title != null) return;
        Color cream = new Color(0.95f, 0.91f, 0.80f), goldC = new Color(1f, 0.85f, 0.42f);
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; title.normal.textColor = goldC;
        sec = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; sec.normal.textColor = cream;
        tipName = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true }; body.normal.textColor = cream;
        count = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight }; count.normal.textColor = Color.white;
        slotName = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        tabOn = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; tabOn.normal.textColor = new Color(0.12f, 0.09f, 0.06f);
        tabOff = new GUIStyle(tabOn); tabOff.normal.textColor = cream;
        heldNum = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        arrow = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; arrow.normal.textColor = goldC;
    }
}
