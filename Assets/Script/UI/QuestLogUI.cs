using UnityEngine;

// 진행 중 의뢰 로그 (자동부팅·영구). 일반 화면에서 J로 토글.
//  - 왼쪽: 수주한 의뢰 목록 / 오른쪽: 선택 의뢰 상세 + 포기 버튼.
public class QuestLogUI : MonoBehaviour
{
    public static QuestLogUI Instance { get; private set; }

    private bool open;
    private Quest selected;
    private GUIStyle title, rowSel, row, detTitle, detObj, detDesc, reward, rewardR, btn, closeS, hint;
    private Texture2D white;

    void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; DontDestroyOnLoad(gameObject); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("QuestLogUI").AddComponent<QuestLogUI>(); }

    public void Open() { open = true; Inventory.QuestUIOpen = true; }
    public void Close() { open = false; Inventory.QuestUIOpen = false; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J)) { if (open) Close(); else if (!Inventory.IsUIOpen) Open(); }
        if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void OnGUI()
    {
        if (!open) return;
        EnsureStyles();
        var qm = QuestManager.Instance;
        float W = Screen.width, H = Screen.height;
        GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, W, H), white); GUI.color = Color.white;
        float w = W * 0.72f, h = H * 0.72f;
        Rect p = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
        Fill(p, new Color(0.14f, 0.11f, 0.08f, 0.99f)); Border(p, 4f, new Color(0.86f, 0.63f, 0.30f));

        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        GUI.Label(new Rect(p.x, p.y + 10f, p.width, 34f), "진행 중인 의뢰", title);
        Rect cb = new Rect(p.xMax - 48f, p.y + 12f, 34f, 34f);
        Fill(cb, new Color(0.6f, 0.2f, 0.18f)); Border(cb, 2f, new Color(0.86f, 0.63f, 0.30f)); GUI.Label(cb, "X", closeS);
        if (click && cb.Contains(m)) { Close(); Event.current.Use(); return; }

        float listX = p.x + 20f, listY = p.y + 60f, listW = p.width * 0.40f, listH = p.height - 80f;
        Fill(new Rect(listX, listY, listW, listH), new Color(0.10f, 0.08f, 0.06f, 1f));

        var acc = qm != null ? qm.accepted : null;
        if (acc == null || acc.Count == 0)
        {
            GUI.Label(new Rect(listX, listY + 20f, listW, 40f), "수주한 의뢰가 없습니다.\n(마을 게시판에서 수락)", hint);
            return;
        }
        if (selected == null || !acc.Contains(selected)) selected = acc[0];

        float ry = listY + 6f;
        foreach (var q in acc)
        {
            Rect rr = new Rect(listX + 6f, ry, listW - 12f, 58f);
            bool sel = q == selected;
            Fill(rr, sel ? new Color(0.3f, 0.24f, 0.16f) : new Color(0.18f, 0.14f, 0.10f));
            Border(rr, sel ? 3f : 1f, sel ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.4f, 0.32f, 0.22f));
            GUI.Label(new Rect(rr.x + 10f, rr.y + 6f, rr.width - 16f, 24f), "[" + q.CategoryLabel() + "] " + q.title, sel ? rowSel : row);
            GUI.Label(new Rect(rr.x + 10f, rr.y + 30f, rr.width - 16f, 22f), q.ObjectiveText(), detObj);
            if (click && rr.Contains(m)) { selected = q; Event.current.Use(); }
            ry += 64f;
        }

        // 오른쪽 상세
        float dx = listX + listW + 24f, dw = p.xMax - dx - 20f;
        GUI.Label(new Rect(dx, p.y + 60f, dw, 30f), "[" + selected.CategoryLabel() + "] " + selected.title, detTitle);
        GUI.Label(new Rect(dx, p.y + 96f, dw, 26f), "● " + selected.ObjectiveText(), detObj);
        GUI.Label(new Rect(dx, p.y + 130f, dw, p.height * 0.4f), selected.description, detDesc);

        float ryy = p.y + p.height * 0.5f;
        GUI.Label(new Rect(dx, ryy, dw, 26f), "보수", detTitle);
        Rect rg = new Rect(dx, ryy + 32f, dw * 0.6f, 28f);
        Fill(rg, new Color(0.1f, 0.08f, 0.06f)); Border(rg, 1f, new Color(0.5f, 0.4f, 0.28f));
        GUI.Label(new Rect(rg.x + 8, rg.y, rg.width - 16, rg.height), "골드 G", reward);
        GUI.Label(new Rect(rg.x + 8, rg.y, rg.width - 16, rg.height), "x" + selected.rewardGold, rewardR);
        if (!string.IsNullOrEmpty(selected.rewardItemId))
        {
            var it = ItemDatabase.Get(selected.rewardItemId);
            Rect ri = new Rect(dx, ryy + 64f, dw * 0.6f, 28f);
            Fill(ri, new Color(0.1f, 0.08f, 0.06f)); Border(ri, 1f, new Color(0.5f, 0.4f, 0.28f));
            GUI.Label(new Rect(ri.x + 8, ri.y, ri.width - 16, ri.height), it != null ? it.itemName : selected.rewardItemId, reward);
            GUI.Label(new Rect(ri.x + 8, ri.y, ri.width - 16, ri.height), "x" + Mathf.Max(1, selected.rewardItemCount), rewardR);
        }

        // 포기 버튼
        Rect gb = new Rect(p.xMax - 170f, p.yMax - 64f, 150f, 46f);
        Fill(gb, new Color(0.6f, 0.22f, 0.2f)); Border(gb, 2f, new Color(0.86f, 0.63f, 0.30f));
        GUI.Label(gb, "퀘스트 포기", btn);
        if (click && gb.Contains(m)) { QuestManager.Instance.Abandon(selected); selected = null; Event.current.Use(); }
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
        title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; title.normal.textColor = goldC;
        row = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold }; row.normal.textColor = cream;
        rowSel = new GUIStyle(row); rowSel.normal.textColor = goldC;
        detTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold }; detTitle.normal.textColor = goldC;
        detObj = new GUIStyle(GUI.skin.label) { fontSize = 15 }; detObj.normal.textColor = new Color(0.85f, 0.82f, 0.72f);
        detDesc = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true }; detDesc.normal.textColor = cream;
        reward = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; reward.normal.textColor = cream;
        rewardR = new GUIStyle(reward) { alignment = TextAnchor.MiddleRight }; rewardR.normal.textColor = goldC;
        btn = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; btn.normal.textColor = Color.white;
        closeS = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; closeS.normal.textColor = Color.white;
        hint = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true }; hint.normal.textColor = new Color(0.7f, 0.65f, 0.55f);
    }
}
