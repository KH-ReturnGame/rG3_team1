using UnityEngine;

// 딸피(저체력) 연출 — 화면 가장자리 붉은 비네트 + 심장박동 펄스.
//  · 자동부팅·영구. GameManager 현재 체력이 임계치 이하일 때만 표시.
//  · 체력이 낮을수록 진해지고 박동이 빨라진다(체력 1로 시작하는 튜토리얼에서 강하게 뜸).
public class LowHealthFx : MonoBehaviour
{
    public static LowHealthFx Instance;

    [Range(0f, 1f)] public float threshold = 0.34f;   // 최대체력 대비 이 비율 미만에서 표시
    public Color tint = new Color(0.78f, 0.05f, 0.06f);

    private Texture2D vig;   // 방사형 비네트(중앙 투명 → 가장자리 붉음)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        BuildVignette();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("LowHealthFx").AddComponent<LowHealthFx>(); }

    private void BuildVignette()
    {
        const int N = 128;
        vig = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x / (float)(N - 1) - 0.5f) * 2f;
                float dy = (y / (float)(N - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;   // 0(중앙)~1(모서리)
                // 가장자리로 갈수록 진해지는 알파(수동 smoothstep: edge0=0.42, edge1=1.0)
                float t = Mathf.Clamp01((r - 0.42f) / (1.0f - 0.42f));
                float a = t * t * (3f - 2f * t);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        vig.SetPixels(px); vig.Apply();
    }

    // 심장박동(lub-dub) — 0~1
    private float Heartbeat(float time)
    {
        float p = time % 1.0f;
        float lub = Mathf.Exp(-Mathf.Pow((p - 0.00f) / 0.07f, 2f));
        float dub = Mathf.Exp(-Mathf.Pow((p - 0.20f) / 0.07f, 2f)) * 0.7f;
        return Mathf.Clamp01(lub + dub);
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || vig == null) return;
        int maxH = gm.MaxHalf;
        if (maxH <= 0) return;
        float frac = gm.CurrentHalf / (float)maxH;
        if (frac >= threshold) return;

        float sev = Mathf.Clamp01(1f - frac / threshold);       // 0(임계) ~ 1(빈사)
        float beat = Heartbeat(Time.unscaledTime * (1.05f + 0.7f * sev));
        float a = (0.22f + 0.42f * sev) * (0.5f + 0.5f * beat);

        GUI.depth = 800;   // 게임 위, HUD/대사창 아래
        Color o = GUI.color;
        GUI.color = new Color(tint.r, tint.g, tint.b, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vig);
        GUI.color = o;
    }
}
