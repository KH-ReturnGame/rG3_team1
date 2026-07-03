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
        UIScale.Apply();   // 해상도 독립 스케일

        float pw = Mathf.Min(780f, UIScale.W - 60f);
        float ph = Mathf.Min(480f, UIScale.H - 60f);
        float px = (UIScale.W - pw) * 0.5f, py = (UIScale.H - ph) * 0.5f;
        Rect panel = new Rect(px, py, pw, ph);

        Fill(new Rect(px + 5, py + 6, pw, ph), new Color(0f, 0f, 0f, 0.4f));     // 그림자
        Fill(panel, UITheme.A(UITheme.BgSolid, 0.98f));                      // 배경
        Border(panel, 3f, UITheme.Accent);                       // 시안 테두리

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
        Border(content, 1f, UITheme.Border);
        if (tab == 0) DrawHelpTab(content);
        else if (tab == 1) DrawMapTab(content);
        else GUI.Label(content, "도감 — 준비 중", dimStyle);
    }

    // 지도 탭: 스캔 모듈을 해금했으면 일반 지도(지형·다음 포탈, 플레이어 위치 없음) 표시.
    private void DrawMapTab(Rect area)
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.HasScanMap)
        {
            GUI.Label(area, "스캔 모듈이 필요합니다.\n후드(C) → 모듈 탭에서 '스캔 모듈'을 해금하세요.", dimStyle);
            return;
        }
        Texture2D m = MapScanner.GetMap();
        if (m == null)
        {
            var areas = MapDiscovery.DiscoveredAreas();
            if (areas != null && areas.Count == 0)
                GUI.Label(area, "아직 탐험한 구역이 없습니다.\n맵을 돌아다니면 지나온 구역이 지도에 채워집니다.", dimStyle);
            else
                GUI.Label(area, "이 구역에서는 지도를 만들 수 없습니다.", dimStyle);
            return;
        }

        Fill(area, new Color(0.04f, 0.06f, 0.09f, 1f));
        float pad = 12f;
        Rect inner = new Rect(area.x + pad, area.y + pad, area.width - pad * 2f, area.height - pad * 2f - 18f);
        float ar = (float)m.width / Mathf.Max(1, m.height);
        float w = inner.width, h = w / ar;
        if (h > inner.height) { h = inner.height; w = h * ar; }
        Rect mr = new Rect(inner.x + (inner.width - w) * 0.5f, inner.y + (inner.height - h) * 0.5f, w, h);
        GUI.DrawTexture(mr, m, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(area.x + pad, area.yMax - 20f, area.width - pad * 2f, 16f),
            "탐험한 구역만 표시(지형 · 다음 포탈=시안) — 플레이어 위치는 보이지 않습니다.", dimStyle);
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
        Fill(r, selected ? UITheme.Accent : UITheme.Panel);
        Border(r, 2f, UITheme.Border);
        itemStyle.normal.textColor = selected ? new Color(0.04f, 0.10f, 0.14f) : new Color(0.78f, 0.86f, 0.94f);
        GUI.Label(r, label, itemStyle);
        return Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition);
    }

    private static void UseEvent() { if (Event.current != null) Event.current.Use(); }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        Color cream = new Color(0.90f, 0.95f, 1f), gold = new Color(1f, 0.85f, 0.42f);
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = gold;
        tabStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        itemStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        bodyTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
        bodyTitleStyle.normal.textColor = gold;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft };
        bodyStyle.normal.textColor = cream;
        dimStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        dimStyle.normal.textColor = new Color(0.58f, 0.68f, 0.78f);
    }

    private static Texture2D Tex() { if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); } return _tex; }
    private static void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Tex()); GUI.color = o; }
    private static void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c); Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
