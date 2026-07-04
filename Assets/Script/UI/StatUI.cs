using UnityEngine;

// 플레이어 스탯(체력=하트, 기력) 표시 UI. 데이터는 GameManager에서 읽음.
// 이 컴포넌트는 '표시'만 담당 — 나중에 Canvas/에셋 UI로 교체할 땐 이걸 빼고
// 같은 GameManager 게터/OnStatsChanged 이벤트를 쓰는 에셋 UI를 넣으면 됨(데이터-표시 분리).
public class StatUI : MonoBehaviour
{
    public static StatUI Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }   // 중복 컴포넌트 제거(한 개만)
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 어느 씬에서 시작하든 HUD(체력·기력 + 단축키)가 항상 뜨도록 자동 생성(1회, 씬 넘어가도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HUD");
        go.AddComponent<Hotbar>();
        go.AddComponent<MenuUI>();
        go.AddComponent<StatUI>();
    }

    [Header("하트(체력)")]
    public Vector2 origin = new Vector2(0.02f, 0.06f);   // 좌상단 시작(화면 비율) — 메뉴 버튼 아래
    public float heartSize = 48f;
    public float heartPad = 8f;

    [Header("기력 바")]
    public float barWidth = 220f;
    public float barHeight = 16f;

    private static Texture2D _white;

    void OnGUI()
    {
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;   // 시작 메뉴에선 HP 숨김
        UIScale.Apply();   // 해상도 독립 스케일

        float x = UIScale.W * origin.x;
        float y = UIScale.H * origin.y;

        // 하트(꽉 / 반 / 빈 칸) — 반칸 단위. 음영·외곽선·하이라이트 텍스처
        GUI.color = Color.white;
        for (int i = 0; i < gm.MaxHearts; i++)
        {
            Rect r = new Rect(x + i * (heartSize + heartPad), y, heartSize, heartSize);
            int fill = Mathf.Clamp(gm.CurrentHalf - i * 2, 0, 2);   // 2=꽉, 1=반, 0=빈
            GUI.DrawTexture(r, Heart(false));                       // 빈 하트 바탕
            if (fill == 2) GUI.DrawTexture(r, Heart(true));         // 꽉 찬 하트
            else if (fill == 1)                                     // 반 칸: 왼쪽 절반만 채움
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * 0.5f, r.height));
                GUI.DrawTexture(new Rect(0, 0, r.width, r.height), Heart(true));
                GUI.EndGroup();
            }
        }
    }

    private static Texture2D WhiteTex()
    {
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        return _white;
    }

    // 하트 텍스처(64px). 음함수 (x²+y²-1)³ - x²y³ ≤ 0 으로 모양을 잡고,
    // 채워짐: 빨강 그라데이션 + 어두운 외곽선 + 좌상단 하이라이트 / 빈 칸: 어두운 채움 + 밝은 테두리.
    private static Texture2D _heartFull, _heartEmpty;
    private static Texture2D Heart(bool full)
    {
        if (full && _heartFull != null) return _heartFull;
        if (!full && _heartEmpty != null) return _heartEmpty;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color topR = new Color(1f, 0.34f, 0.42f), botR = new Color(0.80f, 0.12f, 0.20f), rim = new Color(0.42f, 0.05f, 0.10f);
        Color eFill = new Color(0.15f, 0.17f, 0.21f), eRim = new Color(0.34f, 0.38f, 0.45f);

        for (int py = 0; py < S; py++)
            for (int px = 0; px < S; px++)
            {
                // 3x3 슈퍼샘플로 가장자리 부드럽게(알파 커버리지)
                float cov = 0f; const int N = 3;
                for (int sy = 0; sy < N; sy++)
                    for (int sx = 0; sx < N; sx++)
                    {
                        float fx = (px + (sx + 0.5f) / N) / S * 2.6f - 1.3f;
                        float fy = (py + (sy + 0.5f) / N) / S * 2.6f - 1.4f;
                        float aa = fx * fx + fy * fy - 1f;
                        if (aa * aa * aa - fx * fx * fy * fy * fy <= 0f) cov += 1f;
                    }
                cov /= N * N;
                if (cov <= 0f) { tex.SetPixel(px, py, Color.clear); continue; }

                float x = (px + 0.5f) / S * 2.6f - 1.3f;
                float y = (py + 0.5f) / S * 2.6f - 1.4f;
                float a = x * x + y * y - 1f;
                float f = a * a * a - x * x * y * y * y;   // <=0 안쪽, 0 근처 = 가장자리
                float t = Mathf.Clamp01((py + 0.5f) / S);   // 0 아래 ~ 1 위

                Color col;
                if (full)
                {
                    col = Color.Lerp(botR, topR, t);
                    if (f > -0.35f) col = Color.Lerp(col, rim, 0.65f);                          // 외곽선 어둡게
                    float hx = x + 0.42f, hy = y - 0.55f, hd = hx * hx * 1.5f + hy * hy;
                    if (hd < 0.16f) col = Color.Lerp(col, Color.white, (1f - hd / 0.16f) * 0.75f);  // 좌상단 하이라이트
                }
                else col = (f > -0.30f) ? eRim : eFill;

                col.a = cov;
                tex.SetPixel(px, py, col);
            }
        tex.Apply();
        if (full) _heartFull = tex; else _heartEmpty = tex;
        return tex;
    }
}
