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
    public float heartSize = 34f;
    public float heartPad = 6f;

    [Header("기력 바")]
    public float barWidth = 220f;
    public float barHeight = 16f;

    private static Texture2D _heart, _white;

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        float x = Screen.width * origin.x;
        float y = Screen.height * origin.y;

        // 하트(현재=빨강 / 빈 칸=회색)
        for (int i = 0; i < gm.MaxHearts; i++)
        {
            Rect r = new Rect(x + i * (heartSize + heartPad), y, heartSize, heartSize);
            GUI.color = i < gm.CurrentHearts ? new Color(0.9f, 0.15f, 0.2f) : new Color(0.25f, 0.25f, 0.28f);
            GUI.DrawTexture(r, HeartTex());
        }
        GUI.color = Color.white;

        // 기력 바
        float by = y + heartSize + heartPad + 4f;
        Rect bg = new Rect(x, by, barWidth, barHeight);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(bg, WhiteTex());
        float frac = gm.MaxStamina > 0f ? Mathf.Clamp01(gm.CurrentStamina / gm.MaxStamina) : 0f;
        GUI.color = new Color(0.3f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(bg.x + 2, bg.y + 2, (bg.width - 4) * frac, bg.height - 4), WhiteTex());
        GUI.color = Color.white;
    }

    private static Texture2D WhiteTex()
    {
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        return _white;
    }

    // 하트 모양 텍스처(흰색, 알파). 그릴 때 GUI.color로 색칠. 음함수 (x²+y²-1)³ - x²y³ ≤ 0.
    private static Texture2D HeartTex()
    {
        if (_heart != null) return _heart;
        const int S = 32;
        _heart = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int py = 0; py < S; py++)
            for (int px = 0; px < S; px++)
            {
                float x = (px / (float)(S - 1)) * 2.6f - 1.3f;
                float y = (py / (float)(S - 1)) * 2.6f - 1.4f;        // 텍스처 좌표(아래=0) 보정: 봉우리 위·꼭지점 아래
                float a = x * x + y * y - 1f;
                float f = a * a * a - x * x * y * y * y;
                _heart.SetPixel(px, py, f <= 0f ? Color.white : Color.clear);
            }
        _heart.Apply();
        return _heart;
    }
}
