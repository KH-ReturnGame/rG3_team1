using System.Collections.Generic;
using UnityEngine;

// 상점 UI(자동부팅·영구). ShopStation에 F로 열림.
//  왼쪽: 인벤토리 / 오른쪽: 판매란
//  인벤 아이템 클릭(집기) → 판매란 클릭(판매, 가치의 80% 골드)
//  판매한 것은 판매란에 남아 100% 가치로 되사기 가능(실수 방지).
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private class SSlot { public ItemData item; public int count; }
    private readonly List<SSlot> sold = new List<SSlot>();   // 판매란(되사기 가능)

    private bool open;
    private ItemData held; private int heldCount;            // 손에 든 아이템
    private GUIStyle title, sec, tipName, body, count, val, gold, slotName;
    private Texture2D white;
    private const int Cols = 6, Rows = 4, Cap = 24;

    void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("ShopUI").AddComponent<ShopUI>(); }

    public void Open() { open = true; Inventory.IsUIOpen = true; }
    public void Close()
    {
        if (held != null && Inventory.Instance != null) { Inventory.Instance.Add(held, heldCount); held = null; heldCount = 0; }
        open = false; Inventory.IsUIOpen = false;
    }
    void Update() { if (open && Input.GetKeyDown(KeyCode.Escape)) Close(); }

    private static int Value(ItemData it) { return it != null ? Mathf.Max(1, it.baseValue) : 1; }

    void OnGUI()
    {
        if (!open || Inventory.Instance == null) return;
        EnsureStyles();
        float ss = Mathf.Clamp(Screen.height * 0.072f, 44f, 70f);
        float pad = 6f, gap = 36f, headH = 56f;
        float panelW = Cols * (ss + pad) + pad;
        float gridH = Rows * (ss + pad) + pad;
        float w = panelW * 2f + gap + 40f;
        float h = headH + gridH + 70f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        Vector2 m = Event.current.mousePosition;

        // 배경 + 패널
        GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = new Color(0.13f, 0.10f, 0.08f, 0.99f); GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = new Color(0.86f, 0.63f, 0.30f); GUI.DrawTexture(new Rect(x, y, w, 4f), white); GUI.color = Color.white;

        int g = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        GUI.Label(new Rect(x, y + 12f, w, 32f), "상점", title);
        GUI.Label(new Rect(x, y + 16f, w - 18f, 26f), g + " G", gold);

        float lx = x + 20f, rx = x + 20f + panelW + gap, gy = y + headH;
        GUI.Label(new Rect(lx, gy - 22f, panelW, 20f), "인벤토리", sec);
        GUI.Label(new Rect(rx, gy - 22f, panelW, 20f), "판매란  (판매 80% / 되사기 100%)", sec);

        var inv = Inventory.Instance.slots;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;
        ItemData hover = null;

        // 인벤 그리드
        for (int i = 0; i < Cap; i++)
        {
            Rect r = SlotRect(lx, gy, i, ss, pad);
            DrawSlotBg(r, false);
            ItemData it = (i < inv.Count && inv[i] != null && !inv[i].IsEmpty) ? inv[i].item : null;
            int c = it != null ? inv[i].count : 0;
            if (it != null) { DrawItem(r, it, c, ss); if (held == null && r.Contains(m)) hover = it; }
            if (click && r.Contains(m)) { ClickInv(i); Event.current.Use(); }
        }

        // 판매란 그리드
        for (int i = 0; i < Cap; i++)
        {
            Rect r = SlotRect(rx, gy, i, ss, pad);
            DrawSlotBg(r, true);
            ItemData it = i < sold.Count ? sold[i].item : null;
            int c = it != null ? sold[i].count : 0;
            if (it != null) { DrawItem(r, it, c, ss); if (held == null && r.Contains(m)) hover = it; }
            if (click && r.Contains(m)) { ClickSell(i); Event.current.Use(); }
        }

        if (GUI.Button(new Rect(x + w - 130f, y + h - 46f, 110f, 34f), "닫기")) { Close(); return; }

        if (hover != null) DrawTip(hover, m);
        if (held != null) { float hs = ss * 0.82f; DrawItem(new Rect(m.x - hs * 0.5f, m.y - hs * 0.5f, hs, hs), held, heldCount, ss); }
    }

    private Rect SlotRect(float ox, float oy, int i, float ss, float pad)
    { int r = i / Cols, c = i % Cols; return new Rect(ox + pad + c * (ss + pad), oy + pad + r * (ss + pad), ss, ss); }

    // 인벤 칸 클릭: 집기 / 되돌려놓기
    private void ClickInv(int i)
    {
        var inv = Inventory.Instance;
        if (held == null)
        {
            if (i < inv.slots.Count && inv.slots[i] != null && !inv.slots[i].IsEmpty)
            { held = inv.slots[i].item; heldCount = inv.slots[i].count; inv.slots[i].Clear(); inv.RaiseChanged(); }
        }
        else
        {
            int left = inv.Add(held, heldCount);
            if (left <= 0) { held = null; heldCount = 0; } else heldCount = left;
            inv.RaiseChanged();
        }
    }

    // 판매란 클릭: 들고 있으면 판매(80%) / 판매품이면 되사기(100%)
    private void ClickSell(int i)
    {
        if (held != null)
        {
            int unit = Mathf.RoundToInt(Value(held) * 0.8f);
            if (GameManager.Instance != null) GameManager.Instance.AddGold(unit * heldCount);
            AddSold(held, heldCount);
            held = null; heldCount = 0;
        }
        else if (i < sold.Count && sold[i].item != null)
        {
            var it = sold[i].item;
            int price = Value(it);
            if (GameManager.Instance != null && GameManager.Instance.TrySpendGold(price))
            {
                int left = Inventory.Instance.Add(it, 1);
                if (left > 0) GameManager.Instance.AddGold(price);        // 인벤 꽉 참 → 환불
                else { sold[i].count--; if (sold[i].count <= 0) sold.RemoveAt(i); }
            }
        }
    }

    private void AddSold(ItemData it, int cnt)
    {
        foreach (var s in sold) if (s.item == it) { s.count += cnt; return; }
        sold.Add(new SSlot { item = it, count = cnt });
    }

    // ── 그리기 ──
    private void DrawSlotBg(Rect r, bool sell)
    {
        Fill(r, new Color(0.20f, 0.16f, 0.12f, 0.95f));
        Border(r, 2f, sell ? new Color(0.85f, 0.55f, 0.25f) : new Color(0.45f, 0.38f, 0.28f));
    }

    private void DrawItem(Rect r, ItemData it, int cnt, float ss)
    {
        Rect inner = new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f);
        if (it.icon != null) GUI.DrawTexture(inner, it.icon.texture, ScaleMode.ScaleToFit);
        else { slotName.normal.textColor = it.RarityColor(); GUI.Label(inner, it.itemName, slotName); }
        if (cnt > 1) GUI.Label(new Rect(r.x, r.y, r.width - 5f, r.height - 3f), cnt.ToString(), count);
    }

    private void DrawTip(ItemData it, Vector2 m)
    {
        float tw = 290f;
        tipName.normal.textColor = it.RarityColor();
        string nm = it.itemName, desc = it.description;
        string line = "가치 " + Value(it) + "G   판매 " + Mathf.RoundToInt(Value(it) * 0.8f) + "G / 되사기 " + Value(it) + "G";
        float nh = tipName.CalcHeight(new GUIContent(nm), tw - 16f);
        float dh = string.IsNullOrEmpty(desc) ? 0f : body.CalcHeight(new GUIContent(desc), tw - 16f);
        float th = nh + dh + 24f + 14f;
        float tx = m.x + 16f, ty = m.y + 16f;
        if (tx + tw > Screen.width) tx = Screen.width - tw - 4f;
        if (ty + th > Screen.height) ty = Screen.height - th - 4f;
        Rect tr = new Rect(tx, ty, tw, th);
        Fill(tr, new Color(0.10f, 0.08f, 0.06f, 0.98f)); Border(tr, 2f, new Color(0.86f, 0.63f, 0.30f));
        GUI.Label(new Rect(tx + 8f, ty + 5f, tw - 16f, nh), nm, tipName);
        if (dh > 0f) GUI.Label(new Rect(tx + 8f, ty + 5f + nh, tw - 16f, dh), desc, body);
        GUI.Label(new Rect(tx + 8f, ty + 5f + nh + dh, tw - 16f, 22f), line, val);
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
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = goldC;
        sec = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; sec.normal.textColor = cream;
        tipName = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true }; body.normal.textColor = cream;
        count = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
        count.normal.textColor = Color.white;
        val = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold }; val.normal.textColor = goldC;
        gold = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        gold.normal.textColor = goldC;
        slotName = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }
}
