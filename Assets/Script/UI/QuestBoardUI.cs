using System.Collections.Generic;
using UnityEngine;

// 의뢰 게시판 UI (자동부팅·영구). QuestBoard에 F로 열림.
//  - 불투명 풀스크린(게임 안 비침), 상점/제작대보다 크게.
//  - 탭(전체/주요/일반의뢰) + 가로 스크롤 카드 목록 + 하단 상세(제목·설명·보수·수락).
public class QuestBoardUI : MonoBehaviour
{
    public static QuestBoardUI Instance { get; private set; }

    private bool open;
    private int tab;                 // 0 전체, 1 주요, 2 일반의뢰
    private Quest selected;
    private float scrollX;
    private GUIStyle title, tabOn, tabOff, cardCat, cardObj, detTitle, detDesc, reward, rewardR, btn, closeS, ph;
    private Texture2D white;

    void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; DontDestroyOnLoad(gameObject); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("QuestBoardUI").AddComponent<QuestBoardUI>(); }

    public void Open() { open = true; Inventory.QuestUIOpen = true; }
    public void Close() { open = false; Inventory.QuestUIOpen = false; }
    void Update() { if (open && Input.GetKeyDown(KeyCode.Escape)) Close(); }

    private List<Quest> Filtered()
    {
        var list = new List<Quest>();
        if (QuestManager.Instance == null) return list;
        foreach (var q in QuestManager.Instance.available)
        {
            if (QuestManager.Instance.IsCompleted(q) || !QuestManager.Instance.IsUnlocked(q)) continue;   // 완료/미해금(선행 미완) 숨김
            if (tab == 1 && q.category != QuestCategory.Main) continue;
            if (tab == 2 && q.category == QuestCategory.Main) continue;
            list.Add(q);
        }
        return list;
    }

    void OnGUI()
    {
        if (!open) return;
        EnsureStyles();
        float W = Screen.width, H = Screen.height;
        GUI.color = new Color(0.16f, 0.12f, 0.09f, 1f); GUI.DrawTexture(new Rect(0, 0, W, H), white); GUI.color = Color.white;   // 불투명 풀스크린
        float mx = W * 0.035f, my = H * 0.04f;
        Rect board = new Rect(mx, my, W - 2 * mx, H - 2 * my);
        Fill(board, new Color(0.20f, 0.15f, 0.11f, 1f));
        Border(board, 4f, new Color(0.86f, 0.63f, 0.30f));

        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        GUI.Label(new Rect(board.x, board.y + 8f, board.width, 38f), "의뢰 게시판", title);
        Rect cb = new Rect(board.xMax - 54f, board.y + 12f, 38f, 38f);
        Fill(cb, new Color(0.6f, 0.2f, 0.18f)); Border(cb, 2f, new Color(0.86f, 0.63f, 0.30f)); GUI.Label(cb, "X", closeS);
        if (click && cb.Contains(m)) { Close(); Event.current.Use(); return; }

        // 탭
        string[] tabs = { "전체", "주요 퀘스트", "일반 의뢰" };
        float tx = board.x + 24f, tyy = board.y + 56f, tw = 160f, th = 42f;
        for (int i = 0; i < 3; i++)
        {
            Rect tr = new Rect(tx + i * (tw + 8f), tyy, tw, th);
            Fill(tr, tab == i ? new Color(0.86f, 0.63f, 0.30f) : new Color(0.26f, 0.20f, 0.14f));
            Border(tr, 2f, new Color(0.5f, 0.4f, 0.28f));
            GUI.Label(tr, tabs[i], tab == i ? tabOn : tabOff);
            if (click && tr.Contains(m)) { tab = i; scrollX = 0f; Event.current.Use(); }
        }

        // 카드 영역(가로 스크롤)
        var quests = Filtered();
        float detailH = board.height * 0.27f;
        float cardsY = tyy + th + 18f;
        float cardsH = board.yMax - cardsY - detailH - 26f;
        Rect cardArea = new Rect(board.x + 24f, cardsY, board.width - 48f, cardsH);
        Fill(cardArea, new Color(0.13f, 0.10f, 0.07f, 1f)); Border(cardArea, 2f, new Color(0.4f, 0.32f, 0.22f));

        float cardW = Mathf.Min(290f, cardsH * 0.8f), gap = 24f, padIn = 18f;
        float totalW = quests.Count * (cardW + gap) + padIn;
        float maxScroll = Mathf.Max(0f, totalW - cardArea.width);
        if (Event.current.type == EventType.ScrollWheel && cardArea.Contains(m)) { scrollX += Event.current.delta.y * 26f; Event.current.Use(); }
        scrollX = Mathf.Clamp(scrollX, 0f, maxScroll);

        GUI.BeginClip(cardArea);
        Vector2 lm = Event.current.mousePosition;
        for (int i = 0; i < quests.Count; i++)
        {
            Rect c = new Rect(padIn + i * (cardW + gap) - scrollX, 16f, cardW, cardsH - 32f);
            if (c.xMax < 0 || c.x > cardArea.width) continue;
            DrawCard(c, quests[i]);
            if (click && c.Contains(lm)) selected = quests[i];
        }
        GUI.EndClip();

        if (maxScroll > 0f)
        {
            if (GUI.Button(new Rect(cardArea.x - 4f, cardArea.center.y - 26f, 30f, 52f), "◀")) scrollX = Mathf.Clamp(scrollX - (cardW + gap), 0f, maxScroll);
            if (GUI.Button(new Rect(cardArea.xMax - 26f, cardArea.center.y - 26f, 30f, 52f), "▶")) scrollX = Mathf.Clamp(scrollX + (cardW + gap), 0f, maxScroll);
        }

        // 하단 상세
        Rect detail = new Rect(board.x + 24f, board.yMax - detailH - 16f, board.width - 48f, detailH);
        Fill(detail, new Color(0.13f, 0.10f, 0.07f, 1f)); Border(detail, 2f, new Color(0.4f, 0.32f, 0.22f));
        if (selected != null) DrawDetail(detail, selected, m, click);
        else GUI.Label(new Rect(detail.x, detail.y, detail.width, detail.height), "퀘스트를 선택하세요", detDesc);
    }

    private void DrawCard(Rect c, Quest q)
    {
        bool sel = q == selected;
        Fill(c, new Color(0.22f, 0.17f, 0.12f, 1f));
        Border(c, sel ? 4f : 2f, sel ? new Color(1f, 0.92f, 0.2f) : new Color(0.5f, 0.4f, 0.28f));
        float imgS = c.width - 28f;
        Rect img = new Rect(c.x + 14f, c.y + 14f, imgS, imgS);
        Fill(img, new Color(0.10f, 0.08f, 0.06f, 1f));
        Border(img, 2f, sel ? new Color(1f, 0.92f, 0.2f) : new Color(0.45f, 0.36f, 0.26f));
        if (q.icon != null) GUI.DrawTexture(new Rect(img.x + 6, img.y + 6, img.width - 12, img.height - 12), q.icon.texture, ScaleMode.ScaleToFit);
        else GUI.Label(img, "퀘스트\n이미지", ph);
        GUI.Label(new Rect(c.x + 14f, img.yMax + 8f, c.width - 22f, 26f), "[" + q.CategoryLabel() + "] " + q.title, cardCat);
        GUI.Label(new Rect(c.x + 14f, img.yMax + 34f, c.width - 22f, 24f), q.ObjectiveText(), cardObj);
        if (QuestManager.Instance != null && QuestManager.Instance.IsAccepted(q))
            GUI.Label(new Rect(c.x + 14f, img.yMax + 58f, c.width - 22f, 22f), "● 수락됨", cardObj);
    }

    private void DrawDetail(Rect d, Quest q, Vector2 m, bool click)
    {
        // 왼쪽: 제목 + 설명
        GUI.Label(new Rect(d.x + 18f, d.y + 12f, d.width * 0.42f, 28f), "[" + q.CategoryLabel() + "] " + q.title, detTitle);
        GUI.Label(new Rect(d.x + 18f, d.y + 46f, d.width * 0.42f, d.height - 58f), q.description, detDesc);

        // 가운데: 보수
        float rxx = d.x + d.width * 0.48f, rw = d.width * 0.28f;
        GUI.Label(new Rect(rxx, d.y + 12f, rw, 26f), "보수", detTitle);
        Rect rg = new Rect(rxx, d.y + 46f, rw, 28f);
        Fill(rg, new Color(0.1f, 0.08f, 0.06f)); Border(rg, 1f, new Color(0.5f, 0.4f, 0.28f));
        GUI.Label(new Rect(rg.x + 8, rg.y, rg.width - 16, rg.height), "골드 G", reward);
        GUI.Label(new Rect(rg.x + 8, rg.y, rg.width - 16, rg.height), "x" + q.rewardGold, rewardR);
        if (!string.IsNullOrEmpty(q.rewardItemId))
        {
            var it = ItemDatabase.Get(q.rewardItemId);
            Rect ri = new Rect(rxx, d.y + 78f, rw, 28f);
            Fill(ri, new Color(0.1f, 0.08f, 0.06f)); Border(ri, 1f, new Color(0.5f, 0.4f, 0.28f));
            GUI.Label(new Rect(ri.x + 8, ri.y, ri.width - 16, ri.height), it != null ? it.itemName : q.rewardItemId, reward);
            GUI.Label(new Rect(ri.x + 8, ri.y, ri.width - 16, ri.height), "x" + Mathf.Max(1, q.rewardItemCount), rewardR);
        }

        // 오른쪽: 수락 버튼
        bool acc = QuestManager.Instance != null && QuestManager.Instance.IsAccepted(q);
        Rect ab = new Rect(d.xMax - 190f, d.y + d.height * 0.5f - 30f, 160f, 60f);
        Fill(ab, acc ? new Color(0.32f, 0.32f, 0.30f) : new Color(0.24f, 0.62f, 0.30f));
        Border(ab, 3f, new Color(0.86f, 0.63f, 0.30f));
        GUI.Label(ab, acc ? "수락됨" : "수락", btn);
        if (!acc && click && ab.Contains(m)) { QuestManager.Instance.Accept(q); Event.current.Use(); }
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
        title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; title.normal.textColor = goldC;
        tabOn = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; tabOn.normal.textColor = new Color(0.12f, 0.09f, 0.06f);
        tabOff = new GUIStyle(tabOn); tabOff.normal.textColor = cream;
        cardCat = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold }; cardCat.normal.textColor = cream;
        cardObj = new GUIStyle(GUI.skin.label) { fontSize = 14 }; cardObj.normal.textColor = new Color(0.85f, 0.82f, 0.72f);
        detTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold }; detTitle.normal.textColor = goldC;
        detDesc = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true }; detDesc.normal.textColor = cream;
        reward = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; reward.normal.textColor = cream;
        rewardR = new GUIStyle(reward) { alignment = TextAnchor.MiddleRight }; rewardR.normal.textColor = goldC;
        btn = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; btn.normal.textColor = Color.white;
        closeS = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; closeS.normal.textColor = Color.white;
        ph = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; ph.normal.textColor = new Color(0.55f, 0.5f, 0.42f);
    }
}
