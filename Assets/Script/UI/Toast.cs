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
        if (Instance != null) { Instance.msg = message; Instance.timer = Mathf.Max(0.6f, duration); }
    }

    void Update() { if (timer > 0f) timer -= Time.unscaledDeltaTime; }

    void OnGUI()
    {
        if (timer <= 0f || string.IsNullOrEmpty(msg)) return;
        EnsureStyles();
        GUI.depth = -1000;   // 다른 모든 UI 위에
        float alpha = Mathf.Clamp01(timer < 0.6f ? timer / 0.6f : 1f);
        float w = Mathf.Min(Screen.width * 0.8f, 640f), h = 56f;
        float x = (Screen.width - w) * 0.5f, y = Screen.height * 0.14f;
        var prev = GUI.color;
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.94f * alpha); GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = new Color(0.30f, 0.80f, 0.95f, alpha);
        GUI.DrawTexture(new Rect(x, y, w, 3f), white); GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), white);
        style.normal.textColor = new Color(0.85f, 0.95f, 1f, alpha);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(x, y, w, h), msg, style);
        GUI.color = prev;
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
    }
}
