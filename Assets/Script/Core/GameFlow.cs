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
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteById("guide_village");   // 우물로 내려가면 길잡이 완료

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
    //  · 보스 씬 직접 플레이 등으로 InRun이 안 켜져 있어도 결과창은 항상 띄움(집계만 0으로).
    public void ClearRun()
    {
        if (!InRun) SnapshotRunStart();   // 스냅샷이 없으면 지금 기준(획득 목록은 비게 됨)
        ComputeGains();
        OpenResult(true);
    }

    // 사망 → 손실 적용 + 사망 결과창. GameManager.Die()에서 호출.
    //  · 마을(허브)·시작메뉴·튜토리얼에서의 사망은 제외, 그 외(스테이지·보스 등)에서 죽으면 항상 결과창.
    //    (InRun 플래그는 런 시작 플로우를 안 거치면 안 켜져서, 씬 기준으로 판정)
    public void OnRunPlayerDied()
    {
        string sc = SceneManager.GetActiveScene().name;
        if (sc == hubScene || sc == "StartScene" || sc == "TutorialScene") return;
        ApplyDeathLosses();
        OpenResult(false);
    }

    public void GoToScene(string scene) => LoadSceneLocked(scene);
    public void ReturnToHub() => GoToScene(hubScene);   // 동굴탈출로프 등으로 마을 복귀

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

    // ───────────────── 결과창 (사이버펑크 시안 테마) ─────────────────
    private GUIStyle titleStyle, goldStyle, bodyStyle, itemStyle, btnStyle;

    void OnGUI()
    {
        if (!showResult) return;
        EnsureStyles();
        UIScale.Apply();   // 해상도 독립 스케일

        // Enter/Space/ESC로도 확인
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter
             || Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Escape))
        { Event.current.Use(); ConfirmResult(); return; }

        UITheme.Fill(new Rect(0, 0, UIScale.W, UIScale.H), new Color(0f, 0f, 0f, 0.72f));   // 화면 어둡게

        float w = 500f, h = 410f;
        float x = (UIScale.W - w) * 0.5f, y = (UIScale.H - h) * 0.5f;
        Rect panel = new Rect(x, y, w, h);
        UITheme.DrawPanel(panel, false);   // 그림자+그라데+테두리(강조바는 헤더 색으로 직접)

        Color titleCol = resultCleared ? UITheme.Accent : UITheme.Danger;
        UITheme.Fill(new Rect(x + 2f, y + 2f, w - 4f, 64f), UITheme.A(titleCol, 0.13f));    // 헤더 강조 띠
        UITheme.Fill(new Rect(x + 2f, y + 66f, w - 4f, 2f), UITheme.A(titleCol, 0.9f));
        titleStyle.normal.textColor = titleCol;
        GUI.Label(new Rect(x, y + 12f, w, 44f), resultCleared ? "탐험 완료" : "사망", titleStyle);

        goldStyle.normal.textColor = resultCleared ? UITheme.Gold : UITheme.Danger;
        GUI.Label(new Rect(x, y + 84f, w, 30f), (resultCleared ? "획득 골드  +" : "잃은 골드  -") + resultGold.ToString("N0") + " G", goldStyle);

        bodyStyle.normal.textColor = UITheme.TextDim;
        GUI.Label(new Rect(x + 30f, y + 126f, w - 60f, 24f), resultCleared ? "획득한 전리품 · 채집물" : "잃어버린 아이템", bodyStyle);
        UITheme.Fill(new Rect(x + 30f, y + 152f, w - 60f, 1f), UITheme.A(UITheme.Border, 0.6f));

        float ly = y + 162f;
        itemStyle.normal.textColor = UITheme.Text;
        if (resultItems.Count == 0)
            GUI.Label(new Rect(x + 36f, ly, w - 72f, 24f), "—  없음  —", itemStyle);
        else
        {
            int shown = Mathf.Min(resultItems.Count, 7);
            for (int i = 0; i < shown; i++)
            {
                if ((i & 1) == 1) UITheme.Fill(new Rect(x + 30f, ly + i * 24f, w - 60f, 24f), UITheme.A(Color.white, 0.03f));   // 줄무늬
                GUI.Label(new Rect(x + 38f, ly + i * 24f, w - 76f, 24f), "·  " + resultItems[i], itemStyle);
            }
            if (resultItems.Count > shown)
                GUI.Label(new Rect(x + 38f, ly + shown * 24f, w - 76f, 24f), $"…외 {resultItems.Count - shown}개", itemStyle);
        }

        // 안내 한 줄(클리어만) + 확인 버튼 → 마을
        if (resultCleared)
        {
            bodyStyle.normal.textColor = UITheme.A(UITheme.TextDim, 0.8f);
            GUI.Label(new Rect(x, y + h - 92f, w, 20f), "전리품을 챙겨 마을로 돌아간다", bodyStyle);
        }

        Rect btn = new Rect(x + (w - 180f) * 0.5f, y + h - 62f, 180f, 44f);
        bool hover = btn.Contains(Event.current.mousePosition);
        if (hover) UITheme.Glow(btn, titleCol, 5f, 0.25f);
        UITheme.FillV(btn, hover ? UITheme.Lighten(UITheme.Accent, 0.08f) : UITheme.Accent, UITheme.AccentDim);
        UITheme.Border2(btn, 1.5f, UITheme.Lighten(UITheme.Accent, 0.2f));
        btnStyle.normal.textColor = Color.white;
        GUI.Label(btn, "확인", btnStyle);
        if (Event.current.type == EventType.MouseDown && hover) { Event.current.Use(); ConfirmResult(); }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        goldStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        btnStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }
}
