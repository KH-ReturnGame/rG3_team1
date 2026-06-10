using UnityEngine;

// 게임 전역 스탯 관리 (체력=하트 칸, 기력, 골드). 씬을 넘어가도 유지되는 싱글톤.
// 다른 스크립트는 GameManager.Instance 로 접근. 값이 바뀌면 OnStatsChanged 이벤트로 UI에 알림.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("체력 (하트 칸)")]
    public int maxHearts = 6;
    private int currentHearts;

    [Header("기력 (스태미나)")]
    public float maxStamina = 100f;
    private float currentStamina;

    [Header("재화")]
    public int gold = 0;

    [Header("진행도")]
    public int currentStage = 0;   // 로그라이크 스테이지 진행 (스테이지 쪽 GameManager 통합)

    [Header("디버그 표시 (실제 UI 붙이기 전 임시)")]
    public bool showDebugStats = true;

    private bool isDead;

    // 값이 바뀔 때 알림 (나중에 HP/기력 UI가 구독)
    public event System.Action OnStatsChanged;
    public event System.Action OnPlayerDied;

    // 읽기용
    public int CurrentHearts => currentHearts;
    public int MaxHearts => maxHearts;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public int Gold => gold;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentHearts = maxHearts;
        currentStamina = maxStamina;
    }

    // ───────── 체력 ─────────
    public void TakeDamage(int hearts)
    {
        if (isDead || hearts <= 0) return;
        currentHearts = Mathf.Max(0, currentHearts - hearts);
        OnStatsChanged?.Invoke();
        if (currentHearts == 0) Die();
    }

    public void Heal(int hearts)
    {
        currentHearts = Mathf.Min(maxHearts, currentHearts + Mathf.Max(0, hearts));
        OnStatsChanged?.Invoke();
    }

    public void IncreaseMaxHearts(int amount, bool refill = true)
    {
        maxHearts = Mathf.Max(1, maxHearts + amount);
        currentHearts = refill ? maxHearts : Mathf.Min(currentHearts, maxHearts);
        OnStatsChanged?.Invoke();
    }

    private void Die()
    {
        OnPlayerDied?.Invoke();
        Debug.Log("[GameManager] 플레이어 사망");
        GameFlow.Instance?.OnRunPlayerDied();   // 런 중이면 마을로 귀환(결과창 표시)
        // 임시: 테스트 편의를 위해 체력 리셋(진짜 사망/리스폰 처리는 이후 단계에서)
        currentHearts = maxHearts;
        OnStatsChanged?.Invoke();
    }

    // ───────── 기력 ─────────
    public bool TrySpendStamina(float amount)
    {
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        OnStatsChanged?.Invoke();
        return true;
    }

    public void ChangeStamina(float delta)   // 회복(+) / 소모(-) 둘 다, 0~max로 고정
    {
        float before = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina + delta, 0f, maxStamina);
        if (!Mathf.Approximately(before, currentStamina)) OnStatsChanged?.Invoke();
    }

    public void IncreaseMaxStamina(float amount, bool refill = true)
    {
        maxStamina = Mathf.Max(1f, maxStamina + amount);
        currentStamina = refill ? maxStamina : Mathf.Min(currentStamina, maxStamina);
        OnStatsChanged?.Invoke();
    }

    // ───────── 재화 ─────────
    public void AddGold(int amount) { gold = Mathf.Max(0, gold + amount); OnStatsChanged?.Invoke(); }

    public bool TrySpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        OnStatsChanged?.Invoke();
        return true;
    }

    // ───────── 세이브/로드 ─────────
    // 저장된 값으로 스탯 복원. hearts가 음수면 최대치로 시작(새 게임).
    public void LoadStats(int hearts, int maxH, float maxStam, int g)
    {
        maxHearts = Mathf.Max(1, maxH);
        maxStamina = Mathf.Max(1f, maxStam);
        gold = Mathf.Max(0, g);
        currentHearts = (hearts < 0) ? maxHearts : Mathf.Clamp(hearts, 0, maxHearts);
        currentStamina = maxStamina;
        OnStatsChanged?.Invoke();
    }

    // 임시 디버그 표시 (실제 UI 붙이기 전까지 화면 좌상단에 스탯 표시)
    private GUIStyle style;
    void OnGUI()
    {
        if (!showDebugStats) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 10, 400, 24), $"♥ {currentHearts} / {maxHearts}", style);
        GUI.Label(new Rect(12, 34, 400, 24), $"기력 {currentStamina:0} / {maxStamina:0}", style);
        GUI.Label(new Rect(12, 58, 400, 24), $"Gold {gold}", style);
    }
}
