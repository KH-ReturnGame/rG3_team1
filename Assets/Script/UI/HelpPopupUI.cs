using UnityEngine;

// 도움말 팝업(자동부팅·영구 싱글톤). HelpTrigger가 Show/Hide로 제어.
// 화면 상단 중앙에 제목+본문 패널을 페이드인으로 표시. 게임플레이는 멈추지 않음(읽으면서 이동 가능).
public class HelpPopupUI : MonoBehaviour
{
    public static HelpPopupUI Instance;

    private HelpTrigger owner;
    private string title, body;
    private float shownAt;
    private GUIStyle titleStyle, bodyStyle, tagStyle;
    private static Texture2D _tex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HelpPopupUI");
        Instance = go.AddComponent<HelpPopupUI>();
        DontDestroyOnLoad(go);
    }

    // 지나간 도움말 기록(핸드북에서 다시 보기). 제목 기준 중복 제거. 세션 동안 유지.
    public class HelpEntry { public string title; public string body; }
    public static readonly System.Collections.Generic.List<HelpEntry> Seen = new System.Collections.Generic.List<HelpEntry>();
    private static void Record(string t, string b)
    {
        if (string.IsNullOrEmpty(t)) return;
        foreach (var e in Seen) if (e.title == t) return;
        Seen.Add(new HelpEntry { title = t, body = b });
    }

    public void Show(HelpTrigger o, string t, string b) { owner = o; title = t; body = b; shownAt = Time.unscaledTime; Record(t, b); }
    public void Hide(HelpTrigger o) { if (owner == o) owner = null; }

    void OnGUI()
    {
        if (owner == null || string.IsNullOrEmpty(body)) return;
        EnsureStyles();

        float a = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.25f);   // 페이드 인
        Color cyan = new Color(0.35f, 0.85f, 1f);

        float w = Mathf.Min(720f, Screen.width - 60f);
        float pad = 24f, headH = 40f, gap = 12f;
        float bodyW = w - pad * 2f;
        float bodyH = bodyStyle.CalcHeight(new GUIContent(body), bodyW);
        float h = pad + headH + gap + bodyH + pad;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.07f;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a);              // 그림자
        GUI.DrawTexture(new Rect(x + 4f, y + 5f, w, h), Tex());
        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.95f * a);    // 배경
        GUI.DrawTexture(new Rect(x, y, w, h), Tex());
        GUI.color = new Color(cyan.r, cyan.g, cyan.b, 0.16f * a); // 헤더 강조 띠
        GUI.DrawTexture(new Rect(x, y, w, headH + pad), Tex());
        GUI.color = prev;
        Border(new Rect(x, y, w, h), 2f, new Color(cyan.r, cyan.g, cyan.b, a));   // 테두리

        // "도움말" 태그 + 제목(같은 줄)
        tagStyle.normal.textColor = new Color(0.6f, 0.85f, 1f, a);
        GUI.Label(new Rect(x + pad, y + pad - 2f, 70f, headH), "도움말", tagStyle);
        titleStyle.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(x + pad + 70f, y + pad - 4f, w - pad * 2f - 70f, headH), title, titleStyle);

        // 본문
        bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f, a);
        GUI.Label(new Rect(x + pad, y + pad + headH + gap - 6f, bodyW, bodyH + 4f), body, bodyStyle);
    }

    private void EnsureStyles()
    {
        if (bodyStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperLeft };
        tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    private static Texture2D Tex()
    {
        if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
        return _tex;
    }
    private static void Border(Rect r, float t, Color c)
    {
        Color o = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Tex());
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Tex());
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Tex());
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Tex());
        GUI.color = o;
    }
}
