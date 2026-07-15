using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 오버 화면(튜토리얼 전용, 자동부팅·영구) — 스테이지 사망은 GameFlow 결과창이 따로 담당.
//  튜토리얼에서 죽으면: 시간 정지 + 암전 + GAME OVER + [다시 시작(인트로부터)] / [타이틀 화면으로].
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance;
    public static bool Showing => Instance != null && Instance.showing;   // 게임오버 중인지(도움말 카드 억제 등)

    public float fadeTime = 1.1f;      // 암전·타이포 페이드 인
    public float inputDelay = 0.8f;    // 이 시간 전엔 클릭 무시(연타 오클릭 방지)

    private bool showing;
    private float shownAt;
    private bool subscribed;
    private GUIStyle titleStyle, btnStyle;
    private static Texture2D black;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("GameOverUI"); Instance = go.AddComponent<GameOverUI>(); DontDestroyOnLoad(go); }
    }

    void Update()
    {
        // GameManager 부팅 순서가 불정이라 지연 구독
        if (!subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied += OnDied;
            subscribed = true;
        }
    }

    private void OnDied()
    {
        if (SceneManager.GetActiveScene().name != CombatTutorial.TutorialSceneName) return;   // 스테이지 사망=결과창 몫
        if (showing) return;
        showing = true;
        shownAt = Time.unscaledTime;
        AudioManager.Sfx("gameover");
        AudioManager.Bgm("");    // 배경음 정지
        SlowMoFx.End();          // 예지 슬로우 중 사망 대비 — 시간 상태 정리 후 정지
        if (HelpPopupUI.Instance != null) HelpPopupUI.Instance.CloseAllCards();   // 열린/대기 중 도움말 정리(재시작 후 시간 정지 잔류 방지)
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (!showing) return;
        GUI.depth = -2500;   // HUD·다른 UI 위에
        EnsureStyles();

        float sw = Screen.width, sh = Screen.height;
        float el = Time.unscaledTime - shownAt;
        float a = Mathf.Clamp01(el / fadeTime);

        // 암전
        GUI.color = new Color(0f, 0f, 0f, 0.88f * a);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Black());
        GUI.color = Color.white;

        // GAME OVER (약간 늦게 떠오름)
        float ta = Mathf.Clamp01((el - 0.35f) / 0.8f);
        titleStyle.fontSize = Mathf.RoundToInt(sh * 0.085f);
        SetCol(titleStyle, new Color(0f, 0f, 0f, 0.6f * ta));
        GUI.Label(new Rect(3f, sh * 0.34f + 4f, sw, sh * 0.12f), "GAME OVER", titleStyle);
        SetCol(titleStyle, new Color(0.82f, 0.16f, 0.14f, ta));
        GUI.Label(new Rect(0, sh * 0.34f, sw, sh * 0.12f), "GAME OVER", titleStyle);
        float lw = Mathf.Min(sw * 0.26f, 380f);
        UITheme.Divider((sw - lw) * 0.5f, sh * 0.475f, lw, 0.6f * ta);   // 제목 아래 장식 구분선

        // 선택지(입력 유예 후)
        float ba = Mathf.Clamp01((el - 0.7f) / 0.6f);
        if (ba <= 0.01f) return;
        bool canClick = el >= inputDelay;
        Vector2 m = Event.current.mousePosition;
        bool click = canClick && Event.current.type == EventType.MouseDown && Event.current.button == 0;

        btnStyle.fontSize = Mathf.RoundToInt(sh * 0.03f);
        float rowH = sh * 0.06f;
        float y = sh * 0.55f;

        if (DrawBtn(new Rect(sw * 0.5f - 180f, y, 360f, rowH), "다시 시작", m, click, ba))
        {
            Restart();
            Event.current.Use();
        }
        if (DrawBtn(new Rect(sw * 0.5f - 180f, y + rowH * 1.35f, 360f, rowH), "타이틀 화면으로", m, click, ba))
        {
            GoTitle();
            Event.current.Use();
        }
    }

    // 다시 시작: 새 게임을 만드는 것처럼 완전히 처음부터(스탯·인벤·퀘스트 전부 초기화 + 인트로부터)
    private void Restart()
    {
        showing = false;
        Time.timeScale = 1f;
        int slot = SaveSystem.CurrentSlot >= 0 ? SaveSystem.CurrentSlot : 0;
        SaveSystem.NewGame(slot, CombatTutorial.TutorialSceneName, GameMode.Current);   // 진행 데이터 싱글톤까지 새 게임 상태로 리셋
    }

    private void GoTitle()
    {
        showing = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScene");
    }

    // 타이틀풍 텍스트 버튼: 호버=흰색+왼쪽 오렌지 바
    private bool DrawBtn(Rect r, string label, Vector2 m, bool click, float a)
    {
        bool hv = r.Contains(m);
        if (hv)
        {
            UITheme.Fill(new Rect(r.x + 60f, r.y + r.height * 0.25f, 4f, r.height * 0.5f), UITheme.A(UITheme.Accent, a));
            SetCol(btnStyle, new Color(1f, 1f, 1f, a));
        }
        else SetCol(btnStyle, new Color(0.72f, 0.73f, 0.76f, a));
        GUI.Label(r, label, btnStyle);
        return hv && click;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    private static void SetCol(GUIStyle st, Color c) { st.normal.textColor = c; st.hover.textColor = c; st.active.textColor = c; }

    private static Texture2D Black()
    {
        if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply(); }
        return black;
    }
}
