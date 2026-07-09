using System.Collections.Generic;
using UnityEngine;

// 게임 전역 스탯 관리 (체력=하트 칸, 기력, 골드). 씬을 넘어가도 유지되는 싱글톤.
// 다른 스크립트는 GameManager.Instance 로 접근. 값이 바뀌면 OnStatsChanged 이벤트로 UI에 알림.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("체력 (하트 칸)")]
    public int maxHearts = 3;   // 기본 체력 3칸(개조 포인트 '체력'으로 +1칸씩)
    private int currentHalf;    // 현재 체력을 '반칸' 단위로 저장(2 = 한 칸). 가드 50% 경감 등 반칸 피해 반영용.

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

    // 영구 스탯 업그레이드(상점 골드 소모처). maxHearts에 반영 → 세이브로 유지.
    public void UpgradeMaxHearts(int amt) { maxHearts += amt; currentHalf += amt * 2; OnStatsChanged?.Invoke(); }

    [Header("점프 업그레이드(영구·세이브 유지)")]
    public int bonusJumps = 0;        // 상점에서 산 추가 점프 횟수
    public int maxBonusJumps = 1;     // 상점 점프 업그레이드 상한
    public void UpgradeJumps(int amt)
    {
        bonusJumps = Mathf.Clamp(bonusJumps + amt, 0, maxBonusJumps);
        if (PlayerController.Instance != null) PlayerController.Instance.ApplyEquipment();
        OnStatsChanged?.Invoke();
    }

    [Header("레벨 / 경험치 / 개조 포인트")]
    public int level = 1;
    public int xp = 0;
    public int modPoints = 0;          // 개조 포인트(스탯 강화) — 레벨업 시 획득
    public int pointsPerLevel = 2;
    public int statRegen, statAttack, statAdapt, statLuck;   // 투자 레벨(재생력=체력회복 / 공격력 / 적응력 / 행운)

    [Header("후드 모듈 (맥스 Lv.1 — 해금형)")]
    public int moduleMinimap;   // 미니맵 표시 해금(0/1)
    public int moduleScan;      // 일반 지도(스캔) 해금(0/1)
    public int moduleQuickdraw; // 빨리 뽑기: 핫바 슬롯 +N (레벨당 +1)
    public int maxQuickdraw = 4;
    public bool HasMinimap => moduleMinimap > 0;   // 미니맵은 엔지니어가 망토 수리하며 지급(GrantMinimap)
    // (구) 스캔 모듈 — 지도는 이제 [M]으로 누구나 열람(모듈 폐지). moduleScan 필드는 세이브 호환용 잔존.
    public int XpToNext => level * 120;

    public float GoldMultiplier => 1f + statLuck * 0.1f;     // 행운 → 골드 획득량

    public void AddXp(int amt)
    {
        if (amt <= 0) return;
        xp += amt;
        while (xp >= XpToNext) { xp -= XpToNext; level++; modPoints += pointsPerLevel; currentHalf = MaxHalf; Toast.Show("레벨 업! Lv." + level + "  (개조 포인트 +" + pointsPerLevel + ")", 3f); }
        OnStatsChanged?.Invoke();
    }

    // 개조 포인트로 스탯 강화. 0체력 1재생력 2공격력 3적응력 4행운
    public int StatCost(int i) { switch (i) { case 0: return 5; case 1: return 2; case 4: return 2; default: return 1; } }
    public bool SpendStat(int i)
    {
        int cost = StatCost(i);
        if (modPoints < cost) return false;
        modPoints -= cost;
        switch (i)
        {
            case 0: UpgradeMaxHearts(1); break;       // 체력 +1칸
            case 1: statRegen++; break;               // 재생력(체력 자동 회복)
            case 2: statAttack++; break;              // 공격력(물리)
            case 3: statAdapt++; break;               // 적응력(마법/기프트 — 추후)
            case 4: statLuck++; break;                // 행운(골드/전리품/채집)
        }
        OnStatsChanged?.Invoke();
        return true;
    }

    // 후드 모듈 해금(맥스 Lv.1). id: 0=미니맵, 1=스캔. 성공 시 true.
    public bool TryUnlockModule(int id, int cost)
    {
        int cur = (id == 0) ? moduleMinimap : moduleScan;
        if (cur > 0 || modPoints < cost) return false;
        modPoints -= cost;
        if (id == 0) { moduleMinimap = 1; AcquireBanner.Show("미니맵 모듈", "탐험한 구역이 미니맵·지도에 기록된다.   [,] 토글", null, "새 모듈 획득!"); }
        else { moduleScan = 1; }   // (구) 스캔 — 폐지, 호환용
        OnStatsChanged?.Invoke();
        return true;
    }

    // 주머니 확장(엔지니어): 개조 포인트로 소지품 4×4 → 5×5 → 6×6. 성공 시 true.
    public int bagLevel;                 // 0=4×4, 1=5×5, 2=6×6
    public const int MaxBagLevel = 2;
    public bool TryExpandBackpack(int cost)
    {
        if (Inventory.Instance == null || bagLevel >= MaxBagLevel || modPoints < cost) return false;
        modPoints -= cost;
        bagLevel++;
        int dim = 4 + bagLevel;
        Inventory.Instance.ApplySize(dim);
        AcquireBanner.Show("주머니 확장", "소지품이 " + dim + "×" + dim + " 칸으로 늘었다.", null, "엔지니어 — 개조 완료");
        OnStatsChanged?.Invoke();
        return true;
    }

    // (구) 빨리 뽑기 모듈 — 핫바 폐지로 미사용(세이브 호환 유지용)
    public bool TryUpgradeQuickdraw(int cost)
    {
        if (moduleQuickdraw >= maxQuickdraw || modPoints < cost) return false;
        modPoints -= cost;
        moduleQuickdraw++;
        ApplyQuickdraw();
        OnStatsChanged?.Invoke();
        AcquireBanner.Show("빨리 뽑기 +" + moduleQuickdraw, "핫바 슬롯이 늘었다. 더 많은 아이템을 숫자키로 즉시 사용.", null, "모듈 강화!");
        return true;
    }
    // 현재 빨리 뽑기 레벨을 핫바에 반영(구매·로드 시 호출).
    public void ApplyQuickdraw() { if (Hotbar.Instance != null) Hotbar.Instance.ApplyQuickdraw(moduleQuickdraw); }

    [Header("디버그 표시 (실제 UI 붙이기 전 임시)")]
    public bool showDebugStats = false;   // StatUI(HUD)가 대체 — 기본 꺼둠

    private bool isDead;

    // 값이 바뀔 때 알림 (나중에 HP/기력 UI가 구독)
    public event System.Action OnStatsChanged;
    public event System.Action OnPlayerDied;

    // 읽기용
    public int CurrentHearts => currentHalf / 2;        // 한 칸 단위(내림) — 호환·회복판정용
    public int CurrentHalf => currentHalf;              // 반칸 단위 현재 체력(표시용)
    public int MaxHalf => MaxHearts * 2;                // 반칸 단위 최대 체력
    public int MaxHearts => maxHearts + equipHeartBonus;
    public int Gold => gold;

    // 일시 버프(전투/방어 포션) + 장신구 보너스
    private float atkBuffMult, atkBuffTimer, defReduction, defBuffTimer;
    private int equipHeartBonus;
    private float equipAttackBonus;
    public float AttackMultiplier => 1f + (atkBuffTimer > 0f ? atkBuffMult : 0f) + equipAttackBonus + statAttack * 0.05f;  // 공격력 스탯 5%/레벨
    public float DamageReduction => defBuffTimer > 0f ? defReduction : 0f;        // 피해 감량(0~1)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentHalf = maxHearts * 2;
    }

    // 엔지니어 '망토 수리' 이벤트: 미니맵 모듈 무상 지급 + 획득 연출(최초 1회만)
    public void GrantMinimap()
    {
        if (moduleMinimap > 0) return;
        moduleMinimap = 1;
        OnStatsChanged?.Invoke();
        AcquireBanner.Show("미니맵 모듈", "탐험한 구역이 미니맵·지도에 기록된다.   [,] 미니맵 켜고 끄기 · [M] 지도", null, "엔지니어 — 망토 수리 완료");
    }

    // 어느 씬에서 시작해도 스탯(체력·기력·골드)이 존재하도록 자동 생성(1회, 씬 넘어가도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    // ───────── 체력 ─────────
    public void TakeDamage(int hearts) => TakeDamageHalves(Mathf.Max(0, hearts) * 2);   // 한 칸 단위 입력 호환

    // 반칸 단위 피해. 가드 50% 경감으로 생긴 반칸 피해도 정확히 반영(반칸만 깎임).
    public void TakeDamageHalves(int halves)
    {
        if (isDead || halves <= 0) return;
        halves = Mathf.RoundToInt(halves * (1f - DamageReduction));   // 방어 포션: 피해 감량
        if (halves <= 0) return;
        currentHalf = Mathf.Max(0, currentHalf - halves);   // 튜토리얼 포함 어디서든 사망 가능(튜토 사망=GameOverUI)
        OnStatsChanged?.Invoke();
        if (currentHalf == 0) Die();
    }

    public void Heal(int hearts)
    {
        int before = currentHalf;
        currentHalf = Mathf.Min(MaxHalf, currentHalf + Mathf.Max(0, hearts) * 2);
        // 실제로 회복됐으면 플레이어 위에 초록 숫자(칸 단위)
        if (currentHalf > before && PlayerController.Instance != null)
            DamagePopup.Heal(PlayerController.Instance.transform.position + Vector3.up * 1.1f, (currentHalf - before) * 0.5f);
        OnStatsChanged?.Invoke();
    }

    // 현재 체력을 '반칸 단위'로 직접 지정(튜토리얼 딸피 시작 등 연출용). 0은 사망 처리 안 함(연출 안전).
    public void SetHalves(int halves)
    {
        isDead = false;
        currentHalf = Mathf.Clamp(halves, 1, MaxHalf);
        OnStatsChanged?.Invoke();
    }

    public void IncreaseMaxHearts(int amount, bool refill = true)
    {
        maxHearts = Mathf.Max(1, maxHearts + amount);
        currentHalf = refill ? MaxHalf : Mathf.Min(currentHalf, MaxHalf);
        OnStatsChanged?.Invoke();
    }

    private void Die()
    {
        OnPlayerDied?.Invoke();
        Debug.Log("[GameManager] 플레이어 사망");
        GameFlow.Instance?.OnRunPlayerDied();   // 런 중이면 마을로 귀환(결과창 표시)
        // 임시: 테스트 편의를 위해 체력 리셋(진짜 사망/리스폰 처리는 이후 단계에서)
        currentHalf = MaxHalf;
        OnStatsChanged?.Invoke();
    }

    // ───────── 재화 ─────────
    public void AddGold(int amount) { if (amount > 0) amount = Mathf.RoundToInt(amount * GoldMultiplier); gold = Mathf.Max(0, gold + amount); OnStatsChanged?.Invoke(); }   // 행운 → 골드 획득량↑

    // ───────── 일시 버프 ─────────
    public void ApplyAttackBuff(float mult, float dur) { atkBuffMult = mult; atkBuffTimer = dur; OnStatsChanged?.Invoke(); }
    public void ApplyDefenseBuff(float reduction, float dur) { defReduction = Mathf.Clamp01(reduction); defBuffTimer = dur; OnStatsChanged?.Invoke(); }
    private float hpRegenAccum;

    [Header("마을 자동 회복")]
    public float hubHealInterval = 0.6f;   // 마을(허브)에 있을 때 이 간격마다 반칸씩 회복
    private float hubHealTimer;

    void Update()
    {
        if (atkBuffTimer > 0f) atkBuffTimer -= Time.deltaTime;
        if (defBuffTimer > 0f) defBuffTimer -= Time.deltaTime;
        if (statRegen > 0 && currentHalf > 0 && currentHalf < MaxHalf)   // 재생력 → 체력 느린 자동 회복
        {
            hpRegenAccum += statRegen * 0.06f * Time.deltaTime;
            if (hpRegenAccum >= 1f) { int h = (int)hpRegenAccum; hpRegenAccum -= h; Heal(h); }
        }

        // 마을(허브)에 돌아오면 체력 자동 회복(반칸 단위)
        if (currentHalf < MaxHalf && IsInHub())
        {
            hubHealTimer += Time.deltaTime;
            if (hubHealTimer >= hubHealInterval) { hubHealTimer = 0f; currentHalf = Mathf.Min(MaxHalf, currentHalf + 1); OnStatsChanged?.Invoke(); }
        }
        else hubHealTimer = 0f;
    }

    private bool IsInHub()
    {
        string hub = GameFlow.Instance != null ? GameFlow.Instance.hubScene : "StartingArea";
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == hub;
    }

    // 장신구 보너스 적용(Equipment가 호출). 최대치 변동 시 현재값 클램프.
    public void SetEquipBonuses(int heart, float attack)
    {
        equipHeartBonus = heart;
        equipAttackBonus = attack;
        currentHalf = Mathf.Min(currentHalf, MaxHalf);
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
    public void LoadStats(int hearts, int maxH, int g)
    {
        maxHearts = Mathf.Max(1, maxH);
        gold = Mathf.Max(0, g);
        currentHalf = (hearts < 0) ? MaxHalf : Mathf.Clamp(hearts * 2, 0, MaxHalf);
        OnStatsChanged?.Invoke();
    }

    // 임시 디버그 표시 (실제 UI 붙이기 전까지 화면 좌상단에 스탯 표시)
    private GUIStyle style;
    void OnGUI()
    {
        if (!showDebugStats) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 10, 400, 24), $"♥ {currentHalf / 2f} / {maxHearts}", style);
        GUI.Label(new Rect(12, 34, 400, 24), $"Gold {gold}", style);
    }
}
