using UnityEngine;

// 플레이어 HUD — 하트(체력) + 골드 + 예지 상태. 데이터는 GameManager에서 읽음.
//  · 체력은 하트 그림(꽉/반/빈)으로 직관 표시 — 수치 없음
//  · 피격 시 깎인 반칸이 앰버색 잔상으로 잠깐 남았다가 사라짐(타격 피드백)
//  · 위기(반 칸)면 남은 반칸이 고동침
// 이 컴포넌트는 '표시'만 담당 — GameManager 게터/OnStatsChanged만 쓰므로 에셋 UI로 교체 쉬움.
public class StatUI : MonoBehaviour
{
    public static StatUI Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }   // 중복 컴포넌트 제거(한 개만)
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 어느 씬에서 시작하든 HUD가 항상 뜨도록 자동 생성(1회, 씬 넘어가도 유지)
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
    public Vector2 origin = new Vector2(0.02f, 0.05f);   // 좌상단 시작(화면 비율)
    public float heartSize = 48f;
    public float heartPad = 8f;

    [Header("피격 잔상")]
    public float chipDelay = 0.30f;   // 깎인 잔상이 사라지기 시작하기까지(실시간)
    public float chipSpeed = 6f;      // 잔상 소멸 속도(반칸/초)

    private float displayHalf = -1f;  // 표시 체력(반칸) — 피해는 즉시, 회복은 부드럽게
    private float chipHalf;           // 잔상 위치(피해 직전 값에서 천천히 따라옴)
    private float chipWait;

    private GUIStyle goldStyle, subStyle;
    private static Texture2D _white, coinTex;

    // 애니 갱신은 Update에서(OnGUI Repaint는 게임뷰가 안 보이면 안 돌 수 있음)
    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int cur = Mathf.Clamp(gm.CurrentHalf, 0, gm.MaxHalf);
        if (displayHalf < 0f) { displayHalf = cur; chipHalf = cur; return; }

        float dt = Time.unscaledDeltaTime;
        if (cur < displayHalf - 0.001f)
        {
            chipHalf = Mathf.Max(chipHalf, displayHalf);   // 잔상은 깎이기 전 위치에서 대기
            displayHalf = cur;
            chipWait = chipDelay;
        }
        else if (cur > displayHalf + 0.001f)
        {
            displayHalf = cur;                             // 회복: 즉시(하트가 채워지는 그림 자체가 피드백)
            chipHalf = Mathf.Max(displayHalf, chipHalf);
        }

        if (chipWait > 0f) chipWait -= dt;
        else if (chipHalf > displayHalf) chipHalf = Mathf.Max(displayHalf, chipHalf - chipSpeed * dt);
    }

    void OnGUI()
    {
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;   // 시작 메뉴에선 숨김
        UIScale.Apply();
        EnsureStyles();

        float x = UIScale.W * origin.x;
        float y = UIScale.H * origin.y;
        int curHalves = Mathf.Clamp(gm.CurrentHalf, 0, gm.MaxHalf);
        int ghostTop = Mathf.CeilToInt(chipHalf - 0.001f);   // 잔상이 덮는 반칸 상한(exclusive 아님 — 반칸 인덱스 < ghostTop)

        // 하트(꽉/반/빈) + 피격 잔상(앰버) — 반칸 단위
        for (int i = 0; i < gm.MaxHearts; i++)
        {
            Rect r = new Rect(x + i * (heartSize + heartPad), y, heartSize, heartSize);
            int fill = Mathf.Clamp(curHalves - i * 2, 0, 2);        // 2=꽉, 1=반, 0=빈
            int ghost = Mathf.Clamp(ghostTop - i * 2, 0, 2) - fill; // 이 하트에서 방금 깎여 잔상으로 남은 반칸 수

            GUI.color = Color.white;
            GUI.DrawTexture(r, Heart(false));                       // 빈 하트 바탕

            // 채워진 부분(위기=마지막 반칸 고동)
            if (fill > 0)
            {
                if (curHalves <= 1) GUI.color = new Color(1f, 1f, 1f, 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 8f));
                if (fill == 2) GUI.DrawTexture(r, Heart(true));
                else DrawHalf(r, Heart(true), true);                // 왼쪽 반칸
                GUI.color = Color.white;
            }

            // 잔상(방금 깎인 반칸): 앰버색으로 잠깐 남음
            if (ghost > 0)
            {
                GUI.color = new Color(1f, 0.82f, 0.5f, 0.9f);
                if (fill == 0 && ghost == 2) GUI.DrawTexture(r, Heart(true));
                else if (fill == 0) DrawHalf(r, Heart(true), true);           // 왼쪽 반칸만 잔상
                else DrawHalf(r, Heart(true), false);                          // 오른쪽 반칸 잔상(왼쪽은 채워짐)
                GUI.color = Color.white;
            }
        }

        // 골드(하트 아래): 코인 아이콘 + 수량
        float iy = y + heartSize + 10f;
        float coin = 22f;
        GUI.DrawTexture(new Rect(x + 2f, iy, coin, coin), CoinTex());
        GUI.Label(new Rect(x + coin + 9f, iy - 2f, 240f, coin + 4f), gm.Gold.ToString("n0"), goldStyle);

        // 예지안 착용 시: 준비/쿨다운 (골드 오른쪽)
        if (PrecogCharm.Equipped)
        {
            bool ready = PrecogCharm.CooldownLeft <= 0f;
            subStyle.normal.textColor = ready ? UITheme.Accent : new Color(0.55f, 0.56f, 0.60f);
            string t = ready ? "예지 준비됨" : "예지 " + Mathf.CeilToInt(PrecogCharm.CooldownLeft) + "s";
            GUI.Label(new Rect(x + coin + 9f + 130f, iy - 2f, 220f, coin + 4f), t, subStyle);
        }
    }

    // 하트의 왼쪽/오른쪽 절반만 그리기(그룹 클리핑)
    private static void DrawHalf(Rect r, Texture2D tex, bool left)
    {
        if (left)
        {
            GUI.BeginGroup(new Rect(r.x, r.y, r.width * 0.5f, r.height));
            GUI.DrawTexture(new Rect(0, 0, r.width, r.height), tex);
        }
        else
        {
            GUI.BeginGroup(new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height));
            GUI.DrawTexture(new Rect(-r.width * 0.5f, 0, r.width, r.height), tex);
        }
        GUI.EndGroup();
    }

    private void EnsureStyles()
    {
        if (goldStyle != null) return;
        goldStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        goldStyle.normal.textColor = new Color(0.94f, 0.87f, 0.70f);
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    private static Texture2D WhiteTex()
    {
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        return _white;
    }

    // 금화 아이콘(절차 생성): 밝은 중심 + 어두운 테두리의 앰버 원
    private static Texture2D CoinTex()
    {
        if (coinTex != null) return coinTex;
        const int N = 24;
        coinTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var core = new Color(1f, 0.86f, 0.42f); var rim = new Color(0.72f, 0.52f, 0.16f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((0.95f - r) * 8f);
                Color c = r > 0.62f ? rim : Color.Lerp(core, rim, r * 0.6f);
                float hd = (dx + 0.35f) * (dx + 0.35f) + (dy + 0.35f) * (dy + 0.35f);
                if (hd < 0.12f) c = Color.Lerp(c, Color.white, 0.5f * (1f - hd / 0.12f));
                c.a = a;
                coinTex.SetPixel(x, y, c);
            }
        coinTex.Apply();
        return coinTex;
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
