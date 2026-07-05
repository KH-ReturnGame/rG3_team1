using System.Collections.Generic;
using UnityEngine;

// 상점 UI(자동부팅·영구). ShopStation에 F로 열림. 탭으로 구매/판매 분리.
//  [구매] 상점이 파는 아이템 클릭 → 가치 100% 골드로 구매
//  [판매] 왼쪽 인벤토리 → 아이템 집어 판매란 클릭 = 판매(80%). 판매품은 남아 되사기(100%) 가능.
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private class SSlot { public ItemData item; public int count; }
    private readonly List<SSlot> sold = new List<SSlot>();   // 판매란(되사기 가능)

    private List<ItemData> buyItems;                         // 희귀도순 정렬 캐시
    private int tab;                                          // 0=구매, 1=판매
    private int merchant;                                     // 0=재료 1=포션 2=탐험가
    private bool open;
    private ItemData held; private int heldCount;            // 손에 든 아이템(판매 탭)
    private int heldRot;                                      // [R] 회전 단계(0~3)
    private string hoverInfo;                                // 판매탭 호버 시 가격 줄(구매탭은 null)
    private GUIStyle title, sec, tipName, body, count, gold, slotName, price, tabOn, tabOff, val, heldNum, svc;
    private Texture2D white;
    private const int Cols = 6, Rows = 4, Cap = 24;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }

    private static string[] MerchantIds(int m)
    {
        switch (m)
        {
            case 0: return new[] { "lizard", "underground_flower", "slime_condensate", "flame_flower" };   // 재료 상인
            case 1: return new[] { "heal_potion", "combat_potion", "defense_potion" };                      // 포션 상인
            default: return new[] { "escape_rope" };                                                        // 탐험가(탐험 물품)
        }
    }
    private static string MerchantName(int m) => m == 0 ? "재료 상인" : (m == 1 ? "포션 상인" : "탐험가");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("ShopUI").AddComponent<ShopUI>(); }

    public void Open(int m = 0) { merchant = m; buyItems = null; tab = 0; open = true; Inventory.ShopUIOpen = true; }
    public void Close() { ReturnHeld(); open = false; Inventory.ShopUIOpen = false; }
    void Update()
    {
        if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (open && held != null && Input.GetKeyDown(KeyCode.R)) heldRot = (heldRot + 1) & 3;   // [R] 회전
    }

    private void ReturnHeld() { if (held != null && Inventory.Instance != null) Inventory.Instance.Add(held, heldCount); held = null; heldCount = 0; heldRot = 0; }
    private static int Value(ItemData it) { return it != null ? Mathf.Max(1, it.baseValue) : 1; }
    private static float SellRate(ItemData it) { return (it != null && it.sellAtFullValue) ? 1f : 0.8f; }   // 화폐는 전액 판매

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;
        EnsureStyles();
        UIScale.Apply();   // 해상도 독립 스케일
        float ss = Mathf.Clamp(UIScale.H * 0.072f, 44f, 70f);
        float pad = 6f, gap = 36f, headH = 104f;
        float panelW = Cols * (ss + pad) + pad;
        float gridH = Rows * (ss + pad) + pad;
        float w = panelW * 2f + gap + 40f;
        float h = headH + gridH + 64f;
        float x = (UIScale.W - w) * 0.5f, y = (UIScale.H - h) * 0.5f;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, UIScale.W, UIScale.H), white);
        GUI.color = UITheme.A(UITheme.BgSolid, 0.99f); GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = UITheme.Accent; GUI.DrawTexture(new Rect(x, y, w, 4f), white); GUI.color = Color.white;

        int g = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        GUI.Label(new Rect(x, y + 12f, w, 32f), MerchantName(merchant), title);
        GUI.Label(new Rect(x, y + 16f, w - 18f, 26f), g + " G", gold);

        // 탭
        float tw = 96f, th = 32f, ty = y + 48f;
        Rect t0 = new Rect(x + 20f, ty, tw, th), t1 = new Rect(x + 20f + tw + 6f, ty, tw, th);
        DrawTabBtn(t0, "구매", tab == 0); DrawTabBtn(t1, "판매", tab == 1);
        if (click && t0.Contains(m)) { tab = 0; Event.current.Use(); }
        if (click && t1.Contains(m)) { tab = 1; Event.current.Use(); }

        float gy = y + headH;
        hoverInfo = null;
        ItemData hover = (tab == 0) ? DrawBuy(x, panelW, gap, gy, ss, pad, m, click)
                                    : DrawSell(x, panelW, gap, gy, ss, pad, m, click);

        if (GUI.Button(new Rect(x + w - 130f, y + h - 46f, 110f, 34f), "닫기")) { Close(); return; }

        if (hover != null) DrawTip(hover, m);
        if (held != null)
        {
            float hs = ss * 0.78f;
            Rect ir = new Rect(m.x + 12f, m.y + 6f, hs, hs);   // 커서 옆(아이콘이 포인터에 안 가리게)
            if (held.icon != null) GUI.DrawTexture(ir, held.icon.texture, ScaleMode.ScaleToFit);
            else { slotName.normal.textColor = held.RarityColor(); GUI.Label(ir, held.itemName, slotName); }
            if (heldCount > 1)                                  // 스택은 아이콘 오른쪽 옆에 크게(그림자)
            {
                string cs = heldCount.ToString();
                Rect sr = new Rect(ir.xMax + 3f, ir.y + hs * 0.28f, 80f, 28f);
                heldNum.normal.textColor = Color.black; GUI.Label(new Rect(sr.x + 1f, sr.y + 1f, sr.width, sr.height), cs, heldNum);
                heldNum.normal.textColor = Color.white; GUI.Label(sr, cs, heldNum);
            }
        }
    }

    // ── 구매 탭 ──
    private ItemData DrawBuy(float x, float panelW, float gap, float gy, float ss, float pad, Vector2 m, bool click)
    {
        EnsureBuyItems();
        ItemData hover = null;
        float lx = x + 20f, rx = x + 20f + panelW + gap;
        GUI.Label(new Rect(lx, gy - 22f, panelW, 20f), "인벤토리  (원하는 칸에 직접 놓기)", sec);
        GUI.Label(new Rect(rx, gy - 22f, panelW + 40f, 20f), "구매  (희귀도순 · 클릭 = 1개씩 집기)", sec);

        // 왼쪽: 실제 인벤 그리드 — 산 것을 원하는 칸에 직접 배치(자동배치 없음, [R] 회전)
        float gridH2 = Rows * (ss + pad) + pad;
        var hvItem = InvGridGUI.Draw(new Rect(lx, gy, panelW, gridH2), m, click, ref held, ref heldCount, ref heldRot);
        if (hvItem != null) hover = hvItem;

        // 오른쪽: 상점 구매 목록 (클릭 = 마우스에 1개씩 집기/스택)
        if (buyItems != null)
        {
            int g = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
            for (int i = 0; i < buyItems.Count; i++)
            {
                var it = buyItems[i];
                Rect r = SlotRect(rx, gy, i, ss, pad);
                DrawSlotBg(r, true);
                DrawItem(r, it, 0, ss);
                int p = Value(it);
                Fill(new Rect(r.x, r.yMax - 16f, r.width, 16f), new Color(0f, 0f, 0f, 0.62f));
                price.normal.textColor = g >= p ? new Color(1f, 0.85f, 0.42f) : new Color(0.92f, 0.42f, 0.42f);
                GUI.Label(new Rect(r.x, r.yMax - 17f, r.width, 16f), p + "G", price);
                if (r.Contains(m)) { hover = it; if (click) { Buy(it, p); Event.current.Use(); } }
            }
        }

        // (탐험가의 '단축키·점프 +1' 골드 강화는 제거됨 — 핫바 증가는 엔지니어 '빨리 뽑기 모듈'로, 점프는 장신구로 대체)
        return hover;
    }

    private void EnsureBuyItems()
    {
        if (buyItems != null) return;
        var list = new List<ItemData>();
        foreach (var id in MerchantIds(merchant)) { var it = ItemDatabase.Get(id); if (it != null) list.Add(it); }
        if (list.Count == 0) return;                          // DB 준비 전 → 다음 프레임 재시도
        list.Sort((a, b) => b.rarity.CompareTo(a.rarity));    // 높은 희귀도 먼저
        buyItems = list;
    }

    // 구매: 마우스에 1개씩 집기(같은 아이템이면 스택 증가). 인벤에 넣는 건 칸 클릭(ClickInv).
    private void Buy(ItemData it, int price)
    {
        if (GameManager.Instance == null) return;
        if (held != null && held != it) return;                              // 다른 아이템 들고 있으면 먼저 내려놓기
        if (held == it && heldCount >= Mathf.Max(1, it.maxStack)) return;     // 스택 한도 도달
        if (!GameManager.Instance.TrySpendGold(price)) return;                // 골드 부족
        if (held == null) { held = it; heldCount = 1; heldRot = 0; } else heldCount++;   // 집기 / 스택 +1
    }

    // ── 판매 탭 (인벤 ↔ 판매란) ──
    private ItemData DrawSell(float x, float panelW, float gap, float gy, float ss, float pad, Vector2 m, bool click)
    {
        ItemData hover = null;
        float lx = x + 20f, rx = x + 20f + panelW + gap;
        GUI.Label(new Rect(lx, gy - 22f, panelW, 20f), "인벤토리", sec);
        GUI.Label(new Rect(rx, gy - 22f, panelW + 40f, 20f), "판매란  (판매 80% / 되사기 100%)", sec);

        // 왼쪽: 실제 인벤 그리드 — 집어서 판매란에 클릭, 되산 것도 직접 배치([R] 회전)
        float gridH2 = Rows * (ss + pad) + pad;
        var hvItem = InvGridGUI.Draw(new Rect(lx, gy, panelW, gridH2), m, click, ref held, ref heldCount, ref heldRot);
        if (hvItem != null) { hover = hvItem; hoverInfo = "판매  " + Mathf.RoundToInt(Value(hvItem) * SellRate(hvItem)) + " G"; }
        for (int i = 0; i < Cap; i++)
        {
            Rect r = SlotRect(rx, gy, i, ss, pad);
            DrawSlotBg(r, true);
            ItemData it = i < sold.Count ? sold[i].item : null;
            int c = it != null ? sold[i].count : 0;
            if (it != null) { DrawItem(r, it, c, ss); if (held == null && r.Contains(m)) { hover = it; hoverInfo = "되사기  " + Value(it) + " G"; } }
            if (click && r.Contains(m)) { ClickSell(i); Event.current.Use(); }
        }
        return hover;
    }

    private Rect SlotRect(float ox, float oy, int i, float ss, float pad)
    { int r = i / Cols, c = i % Cols; return new Rect(ox + pad + c * (ss + pad), oy + pad + r * (ss + pad), ss, ss); }

    // (구 ClickInv 삭제 — 인벤 상호작용은 InvGridGUI가 실제 그리드로 처리: 직접 배치, 자동배치 없음)

    private void ClickSell(int i)
    {
        if (held != null)
        {
            int unit = Mathf.RoundToInt(Value(held) * SellRate(held));
            if (GameManager.Instance != null) GameManager.Instance.AddGold(unit * heldCount);
            AddSold(held, heldCount);
            held = null; heldCount = 0; heldRot = 0;
        }
        else if (i < sold.Count && sold[i].item != null)
        {
            var it = sold[i].item;
            if (held != null && held != it) return;                              // 다른 걸 들고 있으면 먼저 내려놓기
            if (held == it && heldCount >= Mathf.Max(1, it.maxStack)) return;     // 스택 한도
            int p = Value(it);
            if (GameManager.Instance != null && GameManager.Instance.TrySpendGold(p))
            {
                if (held == null) { held = it; heldCount = 1; } else heldCount++; // 손에 집기(스택)
                sold[i].count--; if (sold[i].count <= 0) sold.RemoveAt(i);
            }
        }
    }

    private void AddSold(ItemData it, int cnt)
    {
        foreach (var s in sold) if (s.item == it) { s.count += cnt; return; }
        sold.Add(new SSlot { item = it, count = cnt });
    }

    // ── 그리기 ──
    private void DrawTabBtn(Rect r, string label, bool on)
    {
        Fill(r, on ? UITheme.Accent : UITheme.Panel);
        Border(r, 2f, UITheme.Border);
        GUI.Label(r, label, on ? tabOn : tabOff);
    }

    private void DrawSlotBg(Rect r, bool sell)
    {
        Fill(r, UITheme.A(UITheme.Panel, 0.95f));
        Border(r, 2f, sell ? UITheme.Accent : UITheme.Border);
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
        float ih = string.IsNullOrEmpty(hoverInfo) ? 0f : 22f;
        float th = nh + dh + ih + 16f;
        float tx = m.x + 16f, ty = m.y + 16f;
        if (tx + tw > UIScale.W) tx = UIScale.W - tw - 4f;
        if (ty + th > UIScale.H) ty = UIScale.H - th - 4f;
        Rect tr = new Rect(tx, ty, tw, th);
        Fill(tr, UITheme.A(UITheme.BgSolid, 0.98f)); Border(tr, 2f, UITheme.Accent);
        GUI.Label(new Rect(tx + 8f, ty + 5f, tw - 16f, nh), nm, tipName);
        if (dh > 0f) GUI.Label(new Rect(tx + 8f, ty + 5f + nh, tw - 16f, dh), desc, body);
        if (ih > 0f) GUI.Label(new Rect(tx + 8f, ty + 5f + nh + dh, tw - 16f, 20f), hoverInfo, val);
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
        Color cream = new Color(0.90f, 0.95f, 1f), goldC = new Color(1f, 0.85f, 0.42f);
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = goldC;
        sec = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; sec.normal.textColor = cream;
        tipName = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true }; body.normal.textColor = cream;
        count = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
        count.normal.textColor = Color.white;
        gold = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        gold.normal.textColor = goldC;
        slotName = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        price = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tabOn = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tabOn.normal.textColor = new Color(0.04f, 0.10f, 0.14f);
        tabOff = new GUIStyle(tabOn); tabOff.normal.textColor = cream;
        val = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold }; val.normal.textColor = goldC;
        heldNum = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        svc = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; svc.normal.textColor = cream;
    }
}
