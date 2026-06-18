using UnityEngine;

// 도움말 팝업(자동부팅·영구 싱글톤). 표시 방식 3가지:
//  · ShowManual  : [ESC] 또는 우상단 [X] 로 직접 닫을 때까지 유지(이동·시간 경과로 안 사라짐). 핸드북 기록 O.
//  · ShowTimed   : duration초 뒤 자동으로 사라짐(끝에서 페이드아웃). 핸드북 기록 O.
//  · ShowSticky  : 코드(ForceHide)로만 닫힘 — X·ESC 없음. 패링 큐 같은 액션 유도용. 기록 X.
// 어떤 도움말에 어떤 모드를 쓸지는 각 호출부(HelpTrigger 인스펙터 등)에서 선택.
public class HelpPopupUI : MonoBehaviour
{
    public static HelpPopupUI Instance;

    private string title, body;
    private float shownAt;
    private bool visible;
    private bool manual;        // true면 [ESC]·[X]로 닫음
    private float timedUntil;   // >0이면 그 시각에 자동으로 사라짐(Timed). 0이면 타이머 없음.
    private GUIStyle titleStyle, bodyStyle, tagStyle, closeStyle, hintStyle;
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

    // [ESC]·[X]로 직접 닫을 때까지 유지.
    public void ShowManual(string t, string b) { title = t; body = b; shownAt = Time.unscaledTime; visible = true; manual = true; timedUntil = 0f; Record(t, b); }
    // duration초 뒤 자동으로 사라짐.
    public void ShowTimed(string t, string b, float duration) { title = t; body = b; shownAt = Time.unscaledTime; visible = true; manual = false; timedUntil = Time.unscaledTime + Mathf.Max(0.1f, duration); Record(t, b); }
    // 코드(ForceHide)로만 닫힘. 일시적 입력 유도라 기록하지 않음.
    public void ShowSticky(string t, string b) { title = t; body = b; shownAt = Time.unscaledTime; visible = true; manual = false; timedUntil = 0f; }
    public void ForceHide() { visible = false; }
    public bool IsManualOpen => visible && manual;

    void Update()
    {
        if (!visible) return;
        if (manual) { if (Input.GetKeyDown(KeyCode.Escape)) visible = false; return; }
        if (timedUntil > 0f && Time.unscaledTime >= timedUntil) visible = false;   // Timed 자동 종료
    }

    void OnGUI()
    {
        if (!visible || string.IsNullOrEmpty(body)) return;
        EnsureStyles();

        float a = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.2f);                           // 페이드 인
        if (timedUntil > 0f) a *= Mathf.Clamp01((timedUntil - Time.unscaledTime) / 0.5f);         // Timed: 끝에서 페이드아웃
        Color cyan = new Color(0.30f, 0.80f, 0.95f);

        float w = Mathf.Min(720f, Screen.width - 60f);
        float pad = 24f, headH = 40f, gap = 12f, hintH = manual ? 22f : 0f;
        float bodyW = w - pad * 2f;
        float bodyH = bodyStyle.CalcHeight(new GUIContent(body), bodyW);
        float h = pad + headH + gap + bodyH + (manual ? gap + hintH : 0f) + pad;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.07f;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a);                 // 그림자
        GUI.DrawTexture(new Rect(x + 4f, y + 5f, w, h), Tex());
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.96f * a);       // 배경(슬레이트)
        GUI.DrawTexture(new Rect(x, y, w, h), Tex());
        GUI.color = new Color(cyan.r, cyan.g, cyan.b, 0.16f * a);    // 헤더 강조 띠
        GUI.DrawTexture(new Rect(x, y, w, headH + pad), Tex());
        GUI.color = prev;
        Border(new Rect(x, y, w, h), 2f, new Color(cyan.r, cyan.g, cyan.b, a));   // 테두리

        // "도움말" 태그 + 제목(같은 줄)
        tagStyle.normal.textColor = new Color(0.6f, 0.85f, 1f, a);
        GUI.Label(new Rect(x + pad, y + pad - 2f, 70f, headH), "도움말", tagStyle);
        titleStyle.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(x + pad + 70f, y + pad - 4f, w - pad * 2f - 70f - 36f, headH), title, titleStyle);

        // 본문
        bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f, a);
        GUI.Label(new Rect(x + pad, y + pad + headH + gap - 6f, bodyW, bodyH + 4f), body, bodyStyle);

        if (manual)
        {
            // 하단 닫기 힌트
            hintStyle.normal.textColor = new Color(0.6f, 0.72f, 0.82f, a);
            GUI.Label(new Rect(x, y + h - pad - hintH + 2f, w, hintH), "[ESC] 또는 [X] 를 눌러 닫기", hintStyle);

            // 우상단 X 버튼
            Rect close = new Rect(x + w - 38f, y + 10f, 26f, 26f);
            bool hover = close.Contains(Event.current.mousePosition);
            GUI.color = new Color(hover ? 0.85f : 0.5f, hover ? 0.32f : 0.24f, hover ? 0.32f : 0.24f, a);
            GUI.DrawTexture(close, Tex());
            GUI.color = prev;
            closeStyle.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(close, "X", closeStyle);
            if (Event.current.type == EventType.MouseDown && hover) { Event.current.Use(); visible = false; }
        }
    }

    private void EnsureStyles()
    {
        if (bodyStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperLeft };
        tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        closeStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
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
