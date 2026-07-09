using UnityEngine;

// 화면 상단 안내 토스트(자동부팅·영구). 어디서든 Toast.Show("메시지", 초)로 호출.
//  - 다른 UI 위에 그려지고(GUI.depth), 마지막 0.6초는 페이드아웃. timeScale=0에서도 동작.
public class Toast : MonoBehaviour
{
    public static Toast Instance { get; private set; }

    private string msg;
    private float timer;
    private GUIStyle style;
    private Texture2D white;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("Toast").AddComponent<Toast>(); }

    public static void Show(string message, float duration = 4f)
    {
        if (Instance != null) { Instance.msg = message; Instance.timer = Mathf.Max(0.6f, duration); Instance.shownAt = Time.unscaledTime; }
    }

    void Update() { if (timer > 0f) timer -= Time.unscaledDeltaTime; }

    private float shownAt;   // 등장 슬라이드용

    void OnGUI()
    {
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        if (timer <= 0f || string.IsNullOrEmpty(msg)) return;
        EnsureStyles();
        UIScale.Apply();     // 해상도 독립 스케일
        GUI.depth = -1000;   // 다른 모든 UI 위에

        float alpha = Mathf.Clamp01(timer < 0.6f ? timer / 0.6f : 1f);
        float inK = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.22f);
        float ease = 1f - (1f - inK) * (1f - inK);
        alpha *= ease;

        // 내용 크기에 맞는 컴팩트한 알약(고정 대형 바 탈피)
        float maxW = Mathf.Min(UIScale.W * 0.8f, 640f);
        Vector2 sz = style.CalcSize(new GUIContent(msg));
        float w = Mathf.Min(maxW, sz.x + 64f), h = 50f;
        float x = (UIScale.W - w) * 0.5f, y = UIScale.H * 0.13f - 10f * (1f - ease);   // 살짝 위에서 슬라이드 인

        Rect r = new Rect(x, y, w, h);
        UITheme.Shadow(r, 10f, 0.32f * alpha);
        UITheme.FillV(r, UITheme.A(UITheme.PanelTop, 0.96f * alpha), UITheme.A(UITheme.PanelBot, 0.96f * alpha));
        UITheme.Fill(new Rect(x, y, w, 1f), new Color(1f, 1f, 1f, 0.07f * alpha));    // 상단 하이라이트
        UITheme.Fill(new Rect(x, y + 6f, 4f, h - 12f), UITheme.A(UITheme.Accent, alpha));   // 좌측 오렌지 바
        UITheme.Fill(new Rect(x, y + h - 2f, w, 2f), UITheme.A(UITheme.Accent, 0.75f * alpha));   // 하단 액센트 라인

        style.normal.textColor = new Color(0.95f, 0.93f, 0.90f, alpha);
        GUI.Label(new Rect(x + 12f, y, w - 24f, h), msg, style);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }
}
