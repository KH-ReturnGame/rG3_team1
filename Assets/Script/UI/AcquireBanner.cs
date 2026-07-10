using System.Collections.Generic;
using UnityEngine;

// 모듈·능력 '획득' 연출(자동부팅·영구, OnGUI). 어디서든 AcquireBanner.Show("이름","설명")로 호출.
//  · 화면 중앙에 카드가 통통 튀어 등장(ease-out-back) → 잠깐 유지 → 페이드아웃. timeScale=0에서도 동작(unscaled).
//  · 등장 순간 시안 플래시 + 약한 히트스톱으로 임팩트. 동시에 여러 번 호출되면 큐로 하나씩 표시.
public class AcquireBanner : MonoBehaviour
{
    public static AcquireBanner Instance { get; private set; }

    private struct Entry { public string title, desc, eyebrow; public Texture2D icon; }
    private readonly Queue<Entry> queue = new Queue<Entry>();
    private Entry cur;
    private bool active;
    private float startTime;

    private const float Intro = 0.40f, Hold = 2.3f, Outro = 0.55f;
    private float Total { get { return Intro + Hold + Outro; } }

    private Texture2D white;
    private GUIStyle eyebrowStyle, titleStyle, descStyle;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("AcquireBanner").AddComponent<AcquireBanner>(); }

    public static void Show(string title, string desc, Texture2D icon = null, string eyebrow = "새 모듈 획득!")
    {
        if (Instance == null) return;
        Instance.queue.Enqueue(new Entry { title = title, desc = desc, eyebrow = eyebrow, icon = icon });
        Instance.TryNext();
    }

    private void TryNext()
    {
        if (active || queue.Count == 0) return;
        cur = queue.Dequeue();
        active = true;
        startTime = Time.unscaledTime;
        AudioManager.Sfx("acquire");
        Juice.Flash(new Color(1f, 0.78f, 0.38f, 0.26f), 0.18f);   // 앰버 번쩍(테마 통일)
        Juice.HitStop(0.07f);
    }

    void Update()
    {
        if (active && Time.unscaledTime - startTime >= Total) { active = false; TryNext(); }
    }

    void OnGUI()
    {
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        if (!active) return;
        EnsureStyles();
        UIScale.Apply();     // 해상도 독립 스케일(카드 바운스 ScaleAroundPivot은 이 위에 합성)
        GUI.depth = -2000;   // 토스트보다 위
        Color prev = GUI.color;

        float t = Time.unscaledTime - startTime;
        float scale, vig, ca;   // 카드 스케일 / 비네트 / 컨텐츠 알파
        if (t < Intro) { float p = t / Intro; scale = EaseOutBack(p); vig = p; ca = Mathf.Clamp01(p * 1.4f); }
        else if (t < Intro + Hold) { scale = 1f; vig = 1f; ca = 1f; }
        else { float p = Mathf.Clamp01((t - Intro - Hold) / Outro); scale = 1f - 0.05f * p; vig = 1f - p; ca = 1f - p; }

        // 전체 비네트(스케일 영향 없음)
        Tex(new Rect(0, 0, UIScale.W, UIScale.H), new Color(0f, 0f, 0f, 0.5f * vig));

        float cw = Mathf.Min(UIScale.W * 0.64f, 540f), ch = 152f;
        Vector2 center = new Vector2(UIScale.W * 0.5f, UIScale.H * 0.42f);
        Rect card = new Rect(center.x - cw * 0.5f, center.y - ch * 0.5f, cw, ch);

        Matrix4x4 m = GUI.matrix;
        float ds = Mathf.Max(0.02f, scale);   // 0 스케일 방지(역행렬 불가 행렬 경고 차단) — EaseOutBack(0)=0
        GUIUtility.ScaleAroundPivot(new Vector2(ds, ds), center);

        float glow = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
        // 카드: 건메탈 그라데 + 상단 하이라이트 + 하단 오렌지 라인 + 은은한 글로우(테마 통일)
        GUI.color = Color.white;
        UITheme.Glow(card, UITheme.Accent, 16f, 0.16f * ca * glow);
        UITheme.FillV(card, UITheme.A(UITheme.PanelTop, 0.97f * ca), UITheme.A(UITheme.PanelBot, 0.97f * ca));
        Tex(new Rect(card.x, card.y, card.width, 1f), new Color(1f, 1f, 1f, 0.08f * ca));
        Border(card, 1f, UITheme.A(UITheme.Border, 0.9f * ca));
        Tex(new Rect(card.x, card.yMax - 2f, card.width, 2f), UITheme.A(UITheme.Accent, 0.9f * ca));
        Tex(new Rect(card.x, card.y + 10f, 4f, card.height - 20f), UITheme.A(UITheme.Accent, ca));   // 좌측 오렌지 바

        float ic = 92f; Rect iconR = new Rect(card.x + 20f, center.y - ic * 0.5f, ic, ic);
        DrawIcon(iconR, cur.icon, ca, glow);

        float tx = iconR.xMax + 16f, tw = card.xMax - tx - 18f;
        GUI.color = Color.white;   // 라벨 텍스트는 스타일 색을 그대로(틴트 제거)
        eyebrowStyle.normal.textColor = UITheme.A(UITheme.Accent, ca);
        GUI.Label(new Rect(tx, card.y + 20f, tw, 22f), "◆ " + cur.eyebrow, eyebrowStyle);
        titleStyle.normal.textColor = new Color(0.97f, 0.96f, 0.93f, ca);
        GUI.Label(new Rect(tx, card.y + 42f, tw, 42f), cur.title, titleStyle);
        descStyle.normal.textColor = new Color(0.80f, 0.81f, 0.84f, 0.95f * ca);
        GUI.Label(new Rect(tx, card.y + 88f, tw, ch - 100f), cur.desc, descStyle);

        GUI.matrix = m;
        GUI.color = prev;
    }

    private void DrawIcon(Rect r, Texture2D tex, float a, float glow)
    {
        if (tex != null) { Tex(r, new Color(1, 1, 1, a)); GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit); return; }
        // 기본 '모듈 칩' 글리프: 오렌지 테두리 사각 + 내부 십자 격자(지도 느낌)
        Tex(r, new Color(0.10f, 0.10f, 0.12f, a));
        Border(r, 2f, UITheme.A(UITheme.Accent, a));
        Color line = UITheme.A(UITheme.Accent, a * (0.55f + 0.45f * glow));
        float g = r.width * 0.5f;
        Tex(new Rect(r.x + g - 1f, r.y + 8f, 2f, r.height - 16f), line);   // 세로선
        Tex(new Rect(r.x + 8f, r.y + g - 1f, r.width - 16f, 2f), line);    // 가로선
    }

    private static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = p - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void Tex(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white); }
    private void Border(Rect r, float t, Color c)
    {
        Tex(new Rect(r.x, r.y, r.width, t), c);
        Tex(new Rect(r.x, r.yMax - t, r.width, t), c);
        Tex(new Rect(r.x, r.y, t, r.height), c);
        Tex(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (titleStyle != null) return;
        eyebrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        descStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
    }
}
