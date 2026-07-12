using UnityEngine;

// 타임어택 매니저(자동부팅·영구). 트레져 헌터/스피드런 모드일 때만 동작.
//  · 게임플레이 중 시간을 누적(일시정지·도움말 정지 중엔 멈춤 — Time.deltaTime 기반).
//  · 완료 조건: 스피드런=첫 보스 처치(mq_boss 완료) / 트레져 헌터=지하(Metroidvania) 보물상자 전부 개봉.
//  · 달성 시 결과 배너 + 모드별 베스트 기록(PlayerPrefs). HUD 상단에 진행 타이머 표시.
public class TimeAttack : MonoBehaviour
{
    public static TimeAttack Instance;
    public static float PlayTime;   // 누적 플레이 시간(초)
    public static bool Done;        // 목표 달성

    private GUIStyle timeStyle, labelStyle, subStyle;
    private static Texture2D white;
    private float finishFlash;      // 달성 순간 강조(초)

    public const string TreasureScene = "Metroidvania";   // 트레져 헌터 대상 씬(지하 메인 맵)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("TimeAttack");
        Instance = go.AddComponent<TimeAttack>();
        DontDestroyOnLoad(go);
    }

    public static void Reset() { PlayTime = 0f; Done = false; }
    public static void Load(float t, bool done) { PlayTime = t; Done = done; }

    void Update()
    {
        if (finishFlash > 0f) finishFlash -= Time.unscaledDeltaTime;
        if (!GameMode.IsTimeAttack || Done) return;

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "StartScene") return;

        PlayTime += Time.deltaTime;   // 게임 시간(일시정지·도움말 정지 중엔 0)
        CheckComplete(scene);
    }

    private void CheckComplete(string scene)
    {
        bool reached = false;
        if (GameMode.Current == GameMode.Mode.SpeedRun)
        {
            reached = QuestManager.Instance != null && QuestManager.Instance.completed.Contains("mq_boss");
        }
        else if (GameMode.Current == GameMode.Mode.TreasureHunter)
        {
            if (scene == TreasureScene && TreasureChest.All.Count > 0)
            {
                reached = true;
                foreach (var c in TreasureChest.All)
                    if (c != null && !c.IsOpened) { reached = false; break; }
            }
        }
        if (reached) Finish();
    }

    private void Finish()
    {
        Done = true;
        finishFlash = 1.2f;

        string key = "ta_best_" + (int)GameMode.Current;
        float best = PlayerPrefs.GetFloat(key, float.MaxValue);
        bool newBest = PlayTime < best;
        if (newBest) { PlayerPrefs.SetFloat(key, PlayTime); PlayerPrefs.Save(); }

        SlowMoFx.BeginTimed(0.05f, 0.7f);
        Juice.Flash(new Color(1f, 0.92f, 0.6f, 0.4f), 0.4f);
        AudioManager.Sfx("skill_ready");
        AcquireBanner.Show(Format(PlayTime), newBest ? "★ 신기록! ★" : "베스트 " + Format(best), null, GameMode.CurLabel + " 클리어");
        SaveSystem.SaveCurrent();
    }

    // ── HUD: 진행 타이머(상단 중앙) ──
    void OnGUI()
    {
        if (!GameMode.IsTimeAttack) return;
        if (Letterbox.Covering) return;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "StartScene") return;
        if (Inventory.IsUIOpen && !Done) return;   // 창 열려있으면 숨김(완료 배너는 제외)
        EnsureStyles();

        float sw = Screen.width, sh = Screen.height;
        float w = 220f, h = 58f;
        float x = (sw - w) * 0.5f, y = 12f;

        Fill(new Rect(x, y, w, h), new Color(0.05f, 0.05f, 0.07f, 0.62f));
        Fill(new Rect(x, y, w, 2f), UITheme.A(UITheme.Accent, 0.9f));

        // 모드 라벨
        labelStyle.normal.textColor = Done ? new Color(0.55f, 0.95f, 0.65f) : new Color(0.90f, 0.82f, 0.55f);
        GUI.Label(new Rect(x, y + 6f, w, 18f), (Done ? "✔ " : "") + GameMode.CurLabel, labelStyle);

        // 시간(달성 순간 잠깐 확대·번쩍)
        float pop = finishFlash > 0f ? 1f + 0.18f * (finishFlash / 1.2f) : 1f;
        timeStyle.fontSize = Mathf.RoundToInt(26f * pop);
        timeStyle.normal.textColor = Done ? new Color(0.6f, 1f, 0.7f) : new Color(0.96f, 0.97f, 1f);
        GUI.Label(new Rect(x, y + 22f, w, 32f), Format(PlayTime), timeStyle);
    }

    private void EnsureStyles()
    {
        if (timeStyle != null) return;
        timeStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
    }

    // 초 → mm:ss.cc
    public static string Format(float t)
    {
        if (t >= float.MaxValue * 0.5f) return "--:--";
        int m = (int)(t / 60f);
        int s = (int)(t % 60f);
        int cc = (int)((t * 100f) % 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", m, s, cc);
    }

    private static void Fill(Rect r, Color c)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o;
    }
}
