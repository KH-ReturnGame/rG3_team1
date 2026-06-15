using UnityEngine;

// 모험 핸드북(책). G키로 열고 닫음. 왼쪽 탭으로 도움말 / 지도 / 도감을 조회.
//  - 도움말 탭: 지금까지 본 도움말(HelpPopupUI.Seen)을 목록에서 골라 다시 읽을 수 있음.
//  - 지도 / 도감: 추후 구현(자리만).
// 자동부팅 싱글톤. 열려 있으면 플레이어 입력 잠금(Inventory.HandbookUIOpen).
public class HandbookUI : MonoBehaviour
{
    public static HandbookUI Instance;
    public KeyCode toggleKey = KeyCode.G;

    private bool open;
    private int tab;          // 0 도움말 / 1 지도 / 2 도감
    private int sel;          // 도움말 목록 선택 인덱스
    private Vector2 scroll;
    private GUIStyle titleStyle, tabStyle, itemStyle, bodyTitleStyle, bodyStyle, dimStyle;
    private static Texture2D _tex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HandbookUI");
        Instance = go.AddComponent<HandbookUI>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) open = !open;
        if (open && Input.GetKeyDown(KeyCode.Escape)) open = false;
        Inventory.HandbookUIOpen = open;
    }

    void OnGUI()
    {
        if (!open) return;
        EnsureStyles();

        float pw = Mathf.Min(780f, Screen.width - 60f);
        float ph = Mathf.Min(480f, Screen.height - 60f);
        float px = (Screen.width - pw) * 0.5f, py = (Screen.height - ph) * 0.5f;
        Rect panel = new Rect(px, py, pw, ph);

        Fill(new Rect(px + 5, py + 6, pw, ph), new Color(0f, 0f, 0f, 0.4f));     // 그림자
        Fill(panel, new Color(0.09f, 0.08f, 0.07f, 0.97f));                      // 배경
        Border(panel, 3f, new Color(0.86f, 0.63f, 0.30f));                       // 금색 테두리

        GUI.Label(new Rect(px + 20f, py + 12f, pw - 40f, 32f), "모험 핸드북", titleStyle);
        // 닫기
        Rect close = new Rect(px + pw - 40f, py + 12f, 28f, 28f);
        if (Btn(close, "X", false)) { open = false; UseEvent(); }

        // 왼쪽 탭
        string[] tabs = { "도움말", "지도", "도감" };
        float ty = py + 56f;
        for (int i = 0; i < tabs.Length; i++)
        {
            Rect tr = new Rect(px + 16f, ty + i * 48f, 120f, 42f);
            if (Btn(tr, tabs[i], tab == i)) { tab = i; sel = 0; scroll = Vector2.zero; UseEvent(); }
        }

        Rect content = new Rect(px + 150f, py + 56f, pw - 166f, ph - 72f);
        Border(content, 1f, new Color(0.4f, 0.34f, 0.25f));
        if (tab == 0) DrawHelpTab(content);
        else GUI.Label(content, (tab == 1 ? "지도" : "도감") + " — 준비 중", dimStyle);
    }

    private void DrawHelpTab(Rect area)
    {
        var seen = HelpPopupUI.Seen;
        if (seen == null || seen.Count == 0) { GUI.Label(area, "아직 본 도움말이 없습니다.\n탐험하며 도움말 구역을 지나면 여기에 기록됩니다.", dimStyle); return; }

        sel = Mathf.Clamp(sel, 0, seen.Count - 1);
        float listW = area.width * 0.34f;

        // 왼쪽: 제목 목록
        for (int i = 0; i < seen.Count; i++)
        {
            Rect r = new Rect(area.x + 8f, area.y + 8f + i * 40f, listW - 12f, 34f);
            if (Btn(r, seen[i].title, sel == i)) { sel = i; UseEvent(); }
        }

        // 오른쪽: 선택한 도움말 내용
        Rect bodyArea = new Rect(area.x + listW + 8f, area.y + 8f, area.width - listW - 16f, area.height - 16f);
        GUI.Label(new Rect(bodyArea.x, bodyArea.y, bodyArea.width, 30f), seen[sel].title, bodyTitleStyle);
        Rect br = new Rect(bodyArea.x, bodyArea.y + 34f, bodyArea.width, bodyArea.height - 34f);
        float bh = bodyStyle.CalcHeight(new GUIContent(seen[sel].body), bodyArea.width - 16f);
        scroll = GUI.BeginScrollView(br, scroll, new Rect(0, 0, bodyArea.width - 18f, Mathf.Max(bh, br.height)));
        GUI.Label(new Rect(0, 0, bodyArea.width - 18f, bh), seen[sel].body, bodyStyle);
        GUI.EndScrollView();
    }

    private bool Btn(Rect r, string label, bool selected)
    {
        Fill(r, selected ? new Color(0.86f, 0.63f, 0.30f) : new Color(0.20f, 0.17f, 0.13f));
        Border(r, 2f, new Color(0.5f, 0.4f, 0.28f));
        itemStyle.normal.textColor = selected ? new Color(0.12f, 0.09f, 0.05f) : new Color(0.92f, 0.88f, 0.78f);
        GUI.Label(r, label, itemStyle);
        return Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition);
    }

    private static void UseEvent() { if (Event.current != null) Event.current.Use(); }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        Color cream = new Color(0.95f, 0.91f, 0.80f), gold = new Color(1f, 0.85f, 0.42f);
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = gold;
        tabStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        itemStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        bodyTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
        bodyTitleStyle.normal.textColor = gold;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft };
        bodyStyle.normal.textColor = cream;
        dimStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        dimStyle.normal.textColor = new Color(0.7f, 0.65f, 0.55f);
    }

    private static Texture2D Tex() { if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); } return _tex; }
    private static void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Tex()); GUI.color = o; }
    private static void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
