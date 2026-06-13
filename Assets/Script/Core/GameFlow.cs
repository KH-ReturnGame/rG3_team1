using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름(런) 매니저: 마을(허브) ↔ 우물 런(스테이지들) ↔ 보스 ↔ 결과 ↔ 마을.
// 씬 전환의 '두뇌'. 기존 teleporter(StartArea/EndPoint)·클리어 포탈은 이 매니저를 호출만 한다.
// RuntimeInitialize로 자동 부팅되므로 씬에 따로 배치할 필요 없음.
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [Header("씬 이름")]
    public string hubScene = "StartingArea";
    public string stageFormat = "Stage{0}";
    public int stageCount = 3;
    public string bossScene = "BossScene";     // 마지막 스테이지 후 보스 씬(비우면 바로 클리어)

    [Header("전환")]
    public float transitionLock = 0.4f;

    [Header("사망 페널티")]
    [Range(0f, 1f)] public float deathGoldLoss = 0.3f;        // 골드 30% 손실
    [Range(0f, 1f)] public float deathItemLossChance = 0.6f;  // 아이템 슬롯 각 60% 확률 손실

    public bool InRun { get; private set; }
    private int runStage;
    private int runStartGold;
    private readonly Dictionary<ItemData, int> runStartItems = new Dictionary<ItemData, int>();
    private float lockUntil;

    // 결과창 상태
    private bool showResult;
    private bool resultCleared;             // true=클리어 / false=사망
    private int resultGold;                 // 클리어=획득 골드 / 사망=잃은 골드
    private readonly List<string> resultItems = new List<string>();

    public bool CanTransition => Time.unscaledTime >= lockUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("GameFlow").AddComponent<GameFlow>(); }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────── 런 진입/진행 ─────────────────

    public void EnterRun()
    {
        if (!CanTransition || InRun) return;
        InRun = true;
        runStage = 1;
        SnapshotRunStart();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentStage = runStage;
            GameManager.Instance.Heal(GameManager.Instance.MaxHearts);
        }
        LoadStage(runStage);
    }

    public void AdvanceStage()
    {
        if (!CanTransition) return;

        if (!InRun)   // 스테이지 직접 Play 등 → 현재 씬 번호로 보정
        {
            int cur = InferStageFromScene();
            if (cur < 1) { GoToScene(hubScene); return; }
            InRun = true;
            runStage = cur;
            SnapshotRunStart();
        }

        if (runStage < stageCount)
        {
            runStage++;
            if (GameManager.Instance != null) GameManager.Instance.currentStage = runStage;
            LoadStage(runStage);
        }
        else if (!string.IsNullOrEmpty(bossScene))
            LoadSceneLocked(bossScene);   // 마지막 스테이지 → 보스 씬
        else
            ClearRun();                   // 보스 씬 없으면 바로 클리어
    }

    // 보스 클리어 포탈 등에서 호출 → 클리어 결과창
    public void ClearRun()
    {
        if (!InRun) return;
        ComputeGains();
        OpenResult(true);
    }

    // 사망 → 손실 적용 + 사망 결과창. GameManager.Die()에서 호출.
    public void OnRunPlayerDied()
    {
        if (!InRun) return;   // 마을 등 런 밖 사망은 페널티 없음
        ApplyDeathLosses();
        OpenResult(false);
    }

    public void GoToScene(string scene) => LoadSceneLocked(scene);

    // ───────────────── 보상/손실 집계 ─────────────────

    private void SnapshotRunStart()
    {
        runStartGold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        runStartItems.Clear();
        if (Inventory.Instance != null)
            foreach (var s in Inventory.Instance.slots)
                if (s != null && !s.IsEmpty)
                    runStartItems[s.item] = (runStartItems.ContainsKey(s.item) ? runStartItems[s.item] : 0) + s.count;
    }

    // 이번 런에서 획득한 것(현재 보유 - 런 시작 보유)
    private void ComputeGains()
    {
        resultGold = Mathf.Max(0, (GameManager.Instance != null ? GameManager.Instance.Gold : 0) - runStartGold);
        resultItems.Clear();
        if (Inventory.Instance == null) return;

        var now = new Dictionary<ItemData, int>();
        foreach (var s in Inventory.Instance.slots)
            if (s != null && !s.IsEmpty)
                now[s.item] = (now.ContainsKey(s.item) ? now[s.item] : 0) + s.count;

        foreach (var kv in now)
        {
            int start = runStartItems.ContainsKey(kv.Key) ? runStartItems[kv.Key] : 0;
            int gained = kv.Value - start;
            if (gained > 0) resultItems.Add(kv.Key.itemName + "  x" + gained);
        }
    }

    // 사망: 골드 30% + 아이템 슬롯 각 60% 확률 손실
    private void ApplyDeathLosses()
    {
        resultItems.Clear();
        if (GameManager.Instance != null)
        {
            int lost = Mathf.RoundToInt(GameManager.Instance.Gold * deathGoldLoss);
            if (lost > 0) GameManager.Instance.TrySpendGold(lost);
            resultGold = lost;
        }
        if (Inventory.Instance != null)
        {
            foreach (var s in Inventory.Instance.slots)
            {
                if (s == null || s.IsEmpty) continue;
                if (Random.value < deathItemLossChance)
                {
                    resultItems.Add(s.item.itemName + "  x" + s.count);
                    s.Clear();
                }
            }
            Inventory.Instance.RaiseChanged();
        }
    }

    private void OpenResult(bool cleared)
    {
        resultCleared = cleared;
        showResult = true;
        InRun = false;
        if (GameManager.Instance != null) GameManager.Instance.currentStage = 0;
        Time.timeScale = 0f;   // 결과창 동안 일시정지
    }

    private void ConfirmResult()
    {
        showResult = false;
        Time.timeScale = 1f;
        GoToScene(hubScene);   // 확인 → 마을로
    }

    // ───────────────── 내부 ─────────────────

    private void LoadStage(int n) => LoadSceneLocked(string.Format(stageFormat, n));

    private int InferStageFromScene()
    {
        string prefix = stageFormat.Replace("{0}", "");
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
            Debug.LogError($"[GameFlow] '{scene}' 씬을 로드할 수 없습니다. Build Settings에 추가됐는지 확인하세요.");
            return;
        }
        lockUntil = Time.unscaledTime + transitionLock;
        SceneFader.FadeToScene(scene);   // 페이드아웃 후 로드
    }

    // ───────────────── 결과창 (임시 IMGUI) ─────────────────
    private GUIStyle titleStyle, bodyStyle, itemStyle;
    private Texture2D white;

    void OnGUI()
    {
        if (!showResult) return;
        EnsureStyles();

        // 화면 어둡게
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

        float w = 480f, h = 390f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        GUI.color = new Color(0.13f, 0.10f, 0.08f, 0.99f);
        GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = new Color(0.86f, 0.63f, 0.30f);
        GUI.DrawTexture(new Rect(x, y, w, 4), white);
        GUI.DrawTexture(new Rect(x, y + h - 4, w, 4), white);
        GUI.color = Color.white;

        titleStyle.normal.textColor = resultCleared ? new Color(1f, 0.85f, 0.32f) : new Color(1f, 0.42f, 0.36f);
        GUI.Label(new Rect(x, y + 22, w, 44), resultCleared ? "탐험 완료!" : "사망", titleStyle);

        GUI.Label(new Rect(x, y + 76, w, 30),
            resultCleared ? ($"획득 골드   +{resultGold}") : ($"잃은 골드   -{resultGold}"), bodyStyle);

        GUI.Label(new Rect(x + 28, y + 118, w - 56, 26),
            resultCleared ? "획득한 채집물 · 전리품" : "잃어버린 아이템", bodyStyle);

        float ly = y + 150;
        if (resultItems.Count == 0)
            GUI.Label(new Rect(x + 34, ly, w - 68, 24), "—  없음  —", itemStyle);
        else
        {
            int shown = Mathf.Min(resultItems.Count, 9);
            for (int i = 0; i < shown; i++)
                GUI.Label(new Rect(x + 36, ly + i * 24, w - 72, 24), "·  " + resultItems[i], itemStyle);
            if (resultItems.Count > shown)
                GUI.Label(new Rect(x + 36, ly + shown * 24, w - 72, 24), $"…외 {resultItems.Count - shown}개", itemStyle);
        }

        // 확인 버튼 (오른쪽 아래) → 마을
        if (GUI.Button(new Rect(x + w - 150f, y + h - 56f, 130f, 40f), "확인"))
            ConfirmResult();
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        bodyStyle.normal.textColor = Color.white;
        itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        itemStyle.normal.textColor = new Color(0.92f, 0.9f, 0.82f);
    }
}
