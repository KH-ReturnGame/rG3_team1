using System.Collections.Generic;
using UnityEngine;

// 도움말 팝업(자동부팅·영구 싱글톤). 여러 개가 동시에 뜨면 '카드 스택'으로 중첩:
//  · 먼저 뜬 도움말이 맨 앞(완전히 보임), 나중에 뜬 것은 뒤로 쌓여 제목이 위로 살짝 삐져나옴
//  · 덕분에 겹쳐도 앞 도움말에 집중하면서 뒤 도움말 제목을 대강 읽을 수 있음
// 표시 방식 3가지(항목별):
//  · ShowManual : [ESC] 또는 [X]로 닫을 때까지 유지. ESC/X는 '맨 앞' 항목을 닫음. 핸드북 기록 O.
//  · ShowTimed  : duration초 뒤 자동으로 사라짐. 핸드북 기록 O.
//  · ShowSticky : 코드(ForceHide)로만 닫힘(패링 큐 등). 기록 X.
public class HelpPopupUI : MonoBehaviour
{
    public static HelpPopupUI Instance;

    private class Entry
    {
        public string title, body;
        public float shownAt;
        public bool manual;        // ESC/X로 닫기
        public float timedUntil;   // >0이면 그 시각에 자동 종료(Timed). 0 && !manual = Sticky(코드로만).
    }
    private readonly List<Entry> stack = new List<Entry>();   // [0]=먼저 뜬 것(맨 앞), 뒤로 갈수록 나중에 뜬 것
    public bool stackPopups = false;   // 중첩 끔(기본) — 새 도움말이 뜨면 이전 것은 즉시 사라짐(교체). true면 카드처럼 쌓임
    private const float HeaderPeek = 46f;   // 뒤 도움말이 위로 삐져나오는 양(제목 보이게)
    private const int MaxCascade = 4;       // 이 이상 쌓이면 더 위로 올리지 않음

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
    public static readonly List<HelpEntry> Seen = new List<HelpEntry>();
    private static void Record(string t, string b)
    {
        if (string.IsNullOrEmpty(t)) return;
        foreach (var e in Seen) if (e.title == t) return;
        Seen.Add(new HelpEntry { title = t, body = b });
    }

    // [ESC]·[X]로 닫을 때까지 유지.
    public void ShowManual(string t, string b) { Push(new Entry { title = t, body = b, shownAt = Time.unscaledTime, manual = true, timedUntil = 0f }); Record(t, b); }
    // duration초 뒤 자동 종료.
    public void ShowTimed(string t, string b, float duration) { Push(new Entry { title = t, body = b, shownAt = Time.unscaledTime, manual = false, timedUntil = Time.unscaledTime + Mathf.Max(0.1f, duration) }); Record(t, b); }
    // 코드(ForceHide)로만 닫힘. 일시적 입력 유도(패링 큐)라 기록하지 않음.
    public void ShowSticky(string t, string b) { Push(new Entry { title = t, body = b, shownAt = Time.unscaledTime, manual = false, timedUntil = 0f }); }
    // Sticky(패링 큐 등)만 제거.
    public void ForceHide() { stack.RemoveAll(e => !e.manual && e.timedUntil <= 0f); }
    // 중첩 옵션 처리: stackPopups가 false면 새 도움말이 뜰 때 기존 것을 모두 비움(교체).
    private void Push(Entry e) { if (!stackPopups) stack.Clear(); stack.Add(e); }
    public bool IsManualOpen { get { return stack.Count > 0 && stack[0].manual; } }

    void Update()
    {
        // 만료된 Timed 제거
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var e = stack[i];
            if (!e.manual && e.timedUntil > 0f && Time.unscaledTime >= e.timedUntil) stack.RemoveAt(i);
        }
        // ESC → 맨 앞(가장 먼저 뜬) 수동 도움말 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && stack.Count > 0 && stack[0].manual) stack.RemoveAt(0);
    }

    void OnGUI()
    {
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        if (stack.Count == 0) return;
        EnsureStyles();
        UIScale.Apply();   // 해상도 독립 스케일

        float w = Mathf.Min(720f, UIScale.W - 60f);
        float x = (UIScale.W - w) * 0.5f;
        int n = stack.Count;
        int reveal = Mathf.Min(n - 1, MaxCascade);
        float frontY = UIScale.H * 0.05f + reveal * HeaderPeek;   // 맨 앞은 아래, 뒤로 갈수록 위로

        int closeFront = -1;
        // 뒤(나중·깊은 것)부터 그려 z순서상 앞(먼저 뜬 것)이 위로 오게
        for (int i = n - 1; i >= 0; i--)
        {
            int depth = Mathf.Min(i, MaxCascade);
            float y = frontY - depth * HeaderPeek;
            bool isFront = (i == 0);
            if (DrawPopup(stack[i], x, y, w, isFront) && isFront) closeFront = i;
        }
        if (closeFront >= 0) stack.RemoveAt(closeFront);
    }

    // 한 개 그리기. 맨 앞이고 수동이면 [X] 버튼·닫기 힌트 표시(X 클릭 시 true). 뒤 항목은 흐리게.
    private bool DrawPopup(Entry e, float x, float y, float w, bool isFront)
    {
        float a = Mathf.Clamp01((Time.unscaledTime - e.shownAt) / 0.2f);                          // 페이드 인
        if (e.timedUntil > 0f) a *= Mathf.Clamp01((e.timedUntil - Time.unscaledTime) / 0.5f);     // Timed 끝 페이드아웃
        if (!isFront) a *= 0.62f;                                                                  // 뒤 항목은 흐리게(앞에 집중)

        Color cyan = UITheme.Accent;
        float pad = 24f, headH = 40f, gap = 12f, hintH = (isFront && e.manual) ? 22f : 0f;
        float bodyW = w - pad * 2f;
        float bodyH = bodyStyle.CalcHeight(new GUIContent(e.body), bodyW);
        float h = pad + headH + gap + bodyH + ((isFront && e.manual) ? gap + hintH : 0f) + pad;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a);                 // 그림자
        GUI.DrawTexture(new Rect(x + 4f, y + 5f, w, h), Tex());
        GUI.color = UITheme.A(UITheme.BgSolid, 0.97f * a);       // 배경(슬레이트, 거의 불투명 → 뒤 항목 본문을 가림)
        GUI.DrawTexture(new Rect(x, y, w, h), Tex());
        GUI.color = new Color(cyan.r, cyan.g, cyan.b, 0.16f * a);    // 헤더 강조 띠
        GUI.DrawTexture(new Rect(x, y, w, headH + pad), Tex());
        GUI.color = prev;
        Border(new Rect(x, y, w, h), 2f, new Color(cyan.r, cyan.g, cyan.b, a));

        tagStyle.normal.textColor = new Color(0.6f, 0.85f, 1f, a);
        GUI.Label(new Rect(x + pad, y + pad - 2f, 70f, headH), "도움말", tagStyle);
        titleStyle.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(x + pad + 70f, y + pad - 4f, w - pad * 2f - 70f - 36f, headH), e.title, titleStyle);

        bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f, a);
        GUI.Label(new Rect(x + pad, y + pad + headH + gap - 6f, bodyW, bodyH + 4f), e.body, bodyStyle);

        bool clickedClose = false;
        if (isFront && e.manual)
        {
            hintStyle.normal.textColor = new Color(0.6f, 0.72f, 0.82f, a);
            GUI.Label(new Rect(x, y + h - pad - hintH + 2f, w, hintH), "[ESC] 또는 [X] 를 눌러 닫기", hintStyle);

            Rect close = new Rect(x + w - 38f, y + 10f, 26f, 26f);
            bool hover = close.Contains(Event.current.mousePosition);
            GUI.color = new Color(hover ? 0.85f : 0.5f, hover ? 0.32f : 0.24f, hover ? 0.32f : 0.24f, a);
            GUI.DrawTexture(close, Tex());
            GUI.color = prev;
            closeStyle.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(close, "X", closeStyle);
            if (Event.current.type == EventType.MouseDown && hover) { Event.current.Use(); clickedClose = true; }
        }
        return clickedClose;
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
