using System.Collections.Generic;
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

    [Header("포션 쿨타임")]
    public float potionCooldown = 30f;   // 포션(소비 아이템) 사용 후 쿨타임(초) — 아이템 종류별로 각각
    private readonly Dictionary<string, float> potionCdEnd = new Dictionary<string, float>();
    private static string PotionKey(ItemData it) => !string.IsNullOrEmpty(it.id) ? it.id : it.name;
    public float PotionCooldownLeft(ItemData it) { if (it == null) return 0f; float end; return potionCdEnd.TryGetValue(PotionKey(it), out end) ? Mathf.Max(0f, end - Time.time) : 0f; }
    public bool IsPotionReady(ItemData it) => PotionCooldownLeft(it) <= 0f;
    public void StartPotionCooldown(ItemData it) { if (it != null) potionCdEnd[PotionKey(it)] = Time.time + potionCooldown; }

    [Header("디버그 표시 (실제 UI 붙이기 전 임시)")]
    public bool showDebugStats = false;   // StatUI(HUD)가 대체 — 기본 꺼둠

    private bool isDead;

    // 값이 바뀔 때 알림 (나중에 HP/기력 UI가 구독)
    public event System.Action OnStatsChanged;
    public event System.Action OnPlayerDied;

    // 읽기용
    public int CurrentHearts => currentHearts;
    public int MaxHearts => maxHearts + equipHeartBonus;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina + equipStaminaBonus;
    public int Gold => gold;

    // 일시 버프(전투/방어 포션) + 장신구 보너스
    private float atkBuffMult, atkBuffTimer, defReduction, defBuffTimer;
    private int equipHeartBonus;
    private float equipStaminaBonus, equipAttackBonus;
    public float AttackMultiplier => 1f + (atkBuffTimer > 0f ? atkBuffMult : 0f) + equipAttackBonus;  // 플레이어 공격력 배수
    public float DamageReduction => defBuffTimer > 0f ? defReduction : 0f;        // 피해 감량(0~1)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentHearts = maxHearts;
        currentStamina = maxStamina;
    }

    // 어느 씬에서 시작해도 스탯(체력·기력·골드)이 존재하도록 자동 생성(1회, 씬 넘어가도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    // ───────── 체력 ─────────
    public void TakeDamage(int hearts)
    {
        if (isDead || hearts <= 0) return;
        hearts = Mathf.RoundToInt(hearts * (1f - DamageReduction));   // 방어 포션: 피해 감량
        if (hearts <= 0) return;
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

    // ───────── 일시 버프 ─────────
    public void ApplyAttackBuff(float mult, float dur) { atkBuffMult = mult; atkBuffTimer = dur; OnStatsChanged?.Invoke(); }
    public void ApplyDefenseBuff(float reduction, float dur) { defReduction = Mathf.Clamp01(reduction); defBuffTimer = dur; OnStatsChanged?.Invoke(); }
    void Update()
    {
        if (atkBuffTimer > 0f) atkBuffTimer -= Time.deltaTime;
        if (defBuffTimer > 0f) defBuffTimer -= Time.deltaTime;
    }

    // 장신구 보너스 적용(Equipment가 호출). 최대치 변동 시 현재값 클램프.
    public void SetEquipBonuses(int heart, float stamina, float attack)
    {
        equipHeartBonus = heart;
        equipStaminaBonus = stamina;
        equipAttackBonus = attack;
        currentHearts = Mathf.Min(currentHearts, MaxHearts);
        currentStamina = Mathf.Min(currentStamina, MaxStamina);
        OnStatsChanged?.Invoke();
    }

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
