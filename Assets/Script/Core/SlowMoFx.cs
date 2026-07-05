using UnityEngine;

// 슬로우모션 + '불렛 타임' 연출(자동부팅·영구, OnGUI). 젤다 BotW 공중 활쏘기 느낌 — 절제된 연출.
//  · 시간 감속 + 카메라를 '조금만, 천천히' 줌인 + 화면 가장자리만 검정으로 포커스(비네트). 중앙은 깨끗.
//  · BeginHeld(scale): End() 전까지 유지(패링 레슨 등). BeginTimed(scale, 실시간초): 일정 시간 후 자동 복구(예지안 등).
//  · timeScale은 End/자동복구 시 즉시 1 — 히트스톱 등과 충돌 없음. 비네트·줌은 unscaled로 부드럽게.
public class SlowMoFx : MonoBehaviour
{
    public static SlowMoFx Instance;
    public static bool Active { get; private set; }

    public float zoomIn = 0.91f;       // 줌 배율(1=없음, 작을수록 줌인). 0.91 = 살짝
    public float zoomSmooth = 0.55f;   // 줌인 부드럽기(클수록 천천히)

    private bool held;
    private float endRealTime;
    private float overlay;             // 0~1 비네트 세기(부드럽게)
    private float zoomTarget = 1f;
    private float zoomVel;

    private Texture2D vignette;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) { var go = new GameObject("SlowMoFx"); Instance = go.AddComponent<SlowMoFx>(); DontDestroyOnLoad(go); } }

    public static void BeginHeld(float scale) { if (Instance != null) Instance.Begin(scale, -1f); }
    public static void BeginTimed(float scale, float realDuration) { if (Instance != null) Instance.Begin(scale, realDuration); }
    public static void End() { if (Instance != null) Instance.Stop(); }

    private void Begin(float scale, float realDuration)
    {
        float s = Mathf.Clamp(scale, 0.02f, 0.6f);
        held = realDuration < 0f;
        endRealTime = Time.unscaledTime + Mathf.Max(0.1f, realDuration);
        Active = true;
        Time.timeScale = s;
        zoomTarget = zoomIn;
    }

    private void Stop()
    {
        if (!Active) return;
        Active = false;
        Time.timeScale = 1f;
        zoomTarget = 1f;
    }

    void Update()
    {
        if (Active && !held && Time.unscaledTime >= endRealTime) Stop();

        overlay = Mathf.MoveTowards(overlay, Active ? 1f : 0f, Time.unscaledDeltaTime * (Active ? 4.5f : 3.5f));

        if (CameraFollow.Instance != null)
        {
            float st = Active ? zoomSmooth : 0.28f;   // 천천히 땡기고, 풀 땐 조금 빠르게
            CameraFollow.Instance.fxZoomMul = Mathf.SmoothDamp(CameraFollow.Instance.fxZoomMul, zoomTarget, ref zoomVel, st, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }

    void OnGUI()
    {
        if (overlay <= 0.01f) return;
        EnsureTex();
        Color prev = GUI.color;
        GUI.depth = -1800;
        // 화면 가장자리만 검정으로 포커스(중앙은 깨끗) — 비네트 텍스처 알파에 농도가 들어있고, overlay로 전체 페이드
        GUI.color = new Color(1f, 1f, 1f, overlay);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
        GUI.color = prev;
    }

    private void EnsureTex()
    {
        if (vignette != null) return;
        const int N = 256;
        vignette = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var vb = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;        // 0 중앙 ~ 1.41 모서리
                // GLSL식 smoothstep(edge0,edge1,r): r<0.78 깨끗, 1.45 이상 진함 → 가장자리만 검정
                float t = Mathf.Clamp01((r - 0.78f) / (1.45f - 0.78f));
                float a = t * t * (3f - 2f * t) * 0.85f;
                vb[y * N + x] = new Color(0f, 0f, 0f, a);
            }
        vignette.SetPixels(vb);
        vignette.Apply();
    }
}
