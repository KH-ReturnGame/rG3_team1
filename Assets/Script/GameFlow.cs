using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름(런) 매니저: 마을(허브) ↔ 우물 런(스테이지들) ↔ 보스 ↔ 결과 ↔ 마을.
// 씬 전환의 '두뇌'. 기존 teleporter(StartArea/EndPoint)는 이 매니저를 호출만 한다.
// RuntimeInitialize로 자동 부팅되므로 씬에 따로 배치할 필요 없음(원하면 배치해 값 조정 가능).
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [Header("씬 이름")]
    public string hubScene = "StartingArea";   // 마을/허브
    public string stageFormat = "Stage{0}";    // 스테이지 씬 이름 규칙 ({0}=번호)
    public int stageCount = 3;                  // 한 런의 스테이지 수(현재 Stage1~3)
    public string bossScene = "";              // 보스 씬(비우면 마지막 스테이지 후 바로 결과)

    [Header("전환")]
    public float transitionLock = 0.4f;        // 전환 직후 재발동 방지(스폰 위치가 트리거와 겹쳐도 무한루프 방지)

    // 런 상태
    public bool InRun { get; private set; }
    private int runStage;          // 1..stageCount
    private int runStartGold;      // 런 시작 시 골드(획득량 계산용)
    private float lockUntil;

    // 결과창(임시 IMGUI — 나중에 에셋으로 교체)
    private bool showResult;
    private string resultTitle;
    private int resultGold;

    public bool CanTransition => Time.unscaledTime >= lockUntil;

    // 게임 시작 시 자동 생성(어느 씬에서 시작해도 항상 존재)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("GameFlow").AddComponent<GameFlow>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────── 런 진입/진행 ─────────────────

    // 마을의 우물에 진입 → 런 시작(스테이지 1부터)
    public void EnterRun()
    {
        if (!CanTransition || InRun) return;
        InRun = true;
        runStage = 1;
        runStartGold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentStage = runStage;
            GameManager.Instance.Heal(GameManager.Instance.MaxHearts);   // 풀피로 입장
        }
        LoadStage(runStage);
    }

    // 스테이지 출구 도달 → 다음 스테이지 / 끝이면 보스 또는 결과
    public void AdvanceStage()
    {
        if (!CanTransition) return;

        // 런 밖에서 출구를 밟은 경우(스테이지 씬 직접 Play 등) → 현재 씬 번호로 런 상태 보정
        if (!InRun)
        {
            int cur = InferStageFromScene();
            if (cur < 1) { GoToScene(hubScene); return; }   // 스테이지가 아니면 그냥 마을로
            InRun = true;
            runStage = cur;
            runStartGold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        }

        if (runStage < stageCount)
        {
            runStage++;
            if (GameManager.Instance != null) GameManager.Instance.currentStage = runStage;
            LoadStage(runStage);
        }
        else if (!string.IsNullOrEmpty(bossScene))
        {
            LoadSceneLocked(bossScene);   // 마지막 스테이지 후 보스
        }
        else
        {
            FinishRun(true);              // 보스 씬이 없으면 바로 클리어 결과
        }
    }

    // 보스 처치 등에서 호출 → 런 클리어
    public void ClearRun() => FinishRun(true);

    // 사망 → 마을 복귀(런 실패). GameManager.Die()에서 호출.
    public void OnRunPlayerDied()
    {
        if (!InRun) return;   // 마을 등 런 밖에서 죽으면 별도 처리 불필요
        FinishRun(false);
    }

    // 단순 씬 이동(튜토리얼→마을 등). 런과 무관.
    public void GoToScene(string scene) => LoadSceneLocked(scene);

    // ───────────────── 내부 ─────────────────

    private void FinishRun(bool cleared)
    {
        int earned = (GameManager.Instance != null ? GameManager.Instance.Gold : 0) - runStartGold;
        ShowResult(cleared ? "탐험 완료!" : "사망 — 마을로 귀환", earned);
        InRun = false;
        if (GameManager.Instance != null) GameManager.Instance.currentStage = 0;
        GoToScene(hubScene);
    }

    private void LoadStage(int n) => LoadSceneLocked(string.Format(stageFormat, n));

    // 활성 씬 이름("Stage2" 등)에서 스테이지 번호 추출. 스테이지가 아니면 0.
    private int InferStageFromScene()
    {
        string prefix = stageFormat.Replace("{0}", "");   // "Stage{0}" → "Stage"
        string s = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(prefix) && s.StartsWith(prefix)
            && int.TryParse(s.Substring(prefix.Length), out int n)) return n;
        return 0;
    }

    private void LoadSceneLocked(string scene)
    {
        if (string.IsNullOrEmpty(scene)) { Debug.LogError("[GameFlow] 씬 이름이 비어있습니다."); return; }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[GameFlow] '{scene}' 씬을 로드할 수 없습니다. File ▸ Build Settings의 'Scenes In Build'에 추가됐는지 확인하세요.");
            return;
        }
        lockUntil = Time.unscaledTime + transitionLock;
        SceneManager.LoadScene(scene);
    }

    private void ShowResult(string title, int gold)
    {
        showResult = true;
        resultTitle = title;
        resultGold = gold;
    }

    // 결과창(임시) — 에셋 들어오면 교체. 마을 복귀 후 화면 중앙에 표시.
    private GUIStyle titleStyle, bodyStyle;
    void OnGUI()
    {
        if (!showResult) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle  = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = bodyStyle.normal.textColor = Color.white;
        }
        float w = 460, h = 240;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        GUI.Box(new Rect(x, y, w, h), "");
        GUI.Label(new Rect(x, y + 28, w, 50), resultTitle, titleStyle);
        GUI.Label(new Rect(x, y + 100, w, 40), $"획득 골드: {resultGold}", bodyStyle);
        if (GUI.Button(new Rect(x + w * 0.5f - 80, y + h - 64, 160, 46), "확인"))
            showResult = false;
    }
}
