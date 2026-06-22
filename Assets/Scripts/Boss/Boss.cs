using UnityEngine;
using System.Collections;

/// <summary>
/// 보스 메인 컨트롤러.
/// IDamageable / IParryable 을 구현하며, 페이즈 관리 + 패턴 선택 + 패턴 실행을 담당한다.
///
/// [CONFIRM] 페이즈 전환 조건: 현재는 HP 비율(phase2Threshold) 기준으로 구현.
///           HP % 아닌 특정 트리거로 전환해야 한다면 CheckPhaseTransition() 수정.
///
/// [CONFIRM] 패턴 선택 로직: 현재는 가중치(patternWeights) + 쿨다운 + 연속 동일 패턴 방지로 구현.
///           세부 수치는 Inspector에서 조정.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Boss : MonoBehaviour, IDamageable, IParryable
{
    // ── 페이즈 ─────────────────────────────────────────────────
    public enum BossPhase { Phase1, Phase2 }

    // ── Inspector ──────────────────────────────────────────────
    [Header("스탯")]
    public float maxHealth = 500f;

    [Header("페이즈")]
    public BossPhase currentPhase = BossPhase.Phase1;
    [Tooltip("[CONFIRM] 이 HP 비율 이하가 되면 2페이즈로 전환. (0~1, 예: 0.5 = 50%)")]
    public float phase2Threshold = 0.5f;

    [Header("패턴 컴포넌트 (자동 탐색, 비워 둬도 됨)")]
    public BossPattern1_GroundWave    pattern1;
    public BossPattern2_DashTeleport  pattern2;
    public BossPattern3_BouncingCharge pattern3;
    public BossPattern4_BasicAttack   pattern4;
    public BossPattern5_RotatingBeam  pattern5;
    public BossPattern6_ScatterBomb   pattern6;

    [Header("패턴 선택")]
    [Tooltip("패턴 1~6 각각의 선택 가중치 (높을수록 자주 선택됨)")]
    public float[] patternWeights  = { 2f, 2f, 2f, 1f, 1f, 2f };
    [Tooltip("각 패턴 사용 후 재사용 불가 시간 (초)")]
    public float[] patternCooldowns = { 3f, 2f, 4f, 1.5f, 6f, 3f };
    [Tooltip("직전과 동일한 패턴 연속 방지")]
    public bool preventRepeat = true;

    [Header("감지")]
    public float detectRange = 14f;

    [Header("피격 반응")]
    [Tooltip("플레이어 공격에 맞았을 때 플레이어 반대 방향으로 살짝 밀려나는 힘")]
    public float knockbackForce = 2.5f;

    // ── 런타임 상태 ────────────────────────────────────────────
    private float   _currentHealth;
    private bool    _isDead;
    private bool    _isExecuting;
    private bool    _firstEncounter = true;
    private int     _lastPattern    = -1;
    private float[] _cdTimers;
    private Transform _player;
    private Rigidbody2D _rb;

    // ── 초기화 ─────────────────────────────────────────────────
    void Awake()
    {
        _currentHealth = maxHealth;
        _cdTimers = new float[6];
        _rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;

        // Inspector에서 비워 둔 경우 자동으로 같은 GameObject에서 탐색
        if (pattern1 == null) pattern1 = GetComponent<BossPattern1_GroundWave>();
        if (pattern2 == null) pattern2 = GetComponent<BossPattern2_DashTeleport>();
        if (pattern3 == null) pattern3 = GetComponent<BossPattern3_BouncingCharge>();
        if (pattern4 == null) pattern4 = GetComponent<BossPattern4_BasicAttack>();
        if (pattern5 == null) pattern5 = GetComponent<BossPattern5_RotatingBeam>();
        if (pattern6 == null) pattern6 = GetComponent<BossPattern6_ScatterBomb>();
    }

    // ── 메인 루프 ──────────────────────────────────────────────
    void Update()
    {
        if (_isDead || _player == null) return;

        // 쿨다운 타이머 감소
        for (int i = 0; i < _cdTimers.Length; i++)
            if (_cdTimers[i] > 0f) _cdTimers[i] -= Time.deltaTime;

        // 페이즈 전환 체크
        CheckPhaseTransition();

        // 플레이어가 감지 범위 안에 들어오면 패턴 실행
        if (!_isExecuting && DistToPlayer() <= detectRange)
            StartCoroutine(RunNextPattern());
    }

    // ── 페이즈 전환 ────────────────────────────────────────────
    void CheckPhaseTransition()
    {
        if (currentPhase == BossPhase.Phase1
            && _currentHealth / maxHealth <= phase2Threshold)
        {
            currentPhase = BossPhase.Phase2;
            Debug.Log("[Boss] ▶ 2페이즈 전환");
            // TODO: 전환 연출 추가
        }
    }

    // ── 패턴 실행 ──────────────────────────────────────────────
    IEnumerator RunNextPattern()
    {
        _isExecuting = true;

        int idx;
        if (_firstEncounter)
        {
            // 조우 시 항상 패턴 4 (기본 평타) 가 첫 번째
            idx = 3; // 0-indexed
            _firstEncounter = false;
        }
        else
        {
            idx = SelectPattern();
        }

        _lastPattern = idx;
        _cdTimers[idx] = patternCooldowns[idx];

        yield return StartCoroutine(ExecutePattern(idx));

        _isExecuting = false;
    }

    int SelectPattern()
    {
        float total = 0f;
        float[] w = new float[6];

        for (int i = 0; i < 6; i++)
        {
            if (_cdTimers[i] > 0f) continue;
            if (preventRepeat && i == _lastPattern) continue;
            w[i] = patternWeights[i];
            total += w[i];
        }

        // 전부 쿨다운이면 쿨다운이 가장 짧게 남은 패턴 선택
        if (total <= 0f)
        {
            int best = 0;
            float minCd = float.MaxValue;
            for (int i = 0; i < 6; i++)
                if (_cdTimers[i] < minCd) { minCd = _cdTimers[i]; best = i; }
            return best;
        }

        float rand = Random.Range(0f, total);
        float acc  = 0f;
        for (int i = 0; i < 6; i++)
        {
            acc += w[i];
            if (rand <= acc) return i;
        }
        return 0;
    }

    IEnumerator ExecutePattern(int idx)
    {
        bool p2 = currentPhase == BossPhase.Phase2;
        switch (idx)
        {
            case 0: if (pattern1 != null) yield return StartCoroutine(pattern1.Execute(p2)); break;
            case 1: if (pattern2 != null) yield return StartCoroutine(pattern2.Execute(p2)); break;
            case 2: if (pattern3 != null) yield return StartCoroutine(pattern3.Execute(p2)); break;
            case 3: if (pattern4 != null) yield return StartCoroutine(pattern4.Execute(p2)); break;
            case 4: if (pattern5 != null) yield return StartCoroutine(pattern5.Execute(p2)); break;
            case 5: if (pattern6 != null) yield return StartCoroutine(pattern6.Execute(p2)); break;
        }
    }

    // ── IDamageable ────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        _currentHealth -= damage;
        Debug.Log($"[Boss] 피해 {damage:F1} | HP {_currentHealth:F0}/{maxHealth:F0}");

        // 플레이어 반대 방향으로 살짝 넉백 (플레이어 쪽 Hurt()의 넉백과 동일한 느낌)
        if (_rb != null && _player != null)
        {
            float dir = transform.position.x >= _player.position.x ? 1f : -1f;
            _rb.linearVelocity = new Vector2(dir * knockbackForce, _rb.linearVelocity.y);
        }

        if (_currentHealth <= 0f) Die();
    }

    // ── IParryable ─────────────────────────────────────────────
    public void ApplyGroggy()
    {
        if (_isDead) return;
        Debug.Log("[Boss] 패링당함! (그로기 연출 추가 예정)");
        // TODO: 그로기 상태 추가 (진행 중인 패턴 중단, 경직 모션 등)
    }

    // ── 사망 ───────────────────────────────────────────────────
    void Die()
    {
        _isDead = true;
        StopAllCoroutines();
        Debug.Log("[Boss] 사망");
        // TODO: 사망 연출 / 드랍 / 씬 전환 등
        Destroy(gameObject, 1.5f);
    }

    // ── 외부에서 참조 ──────────────────────────────────────────
    public Transform   GetPlayerTransform() => _player;
    public BossPhase   GetCurrentPhase()    => currentPhase;
    public float       GetHealthRatio()     => _currentHealth / maxHealth;
    public bool        IsDead()             => _isDead;

    float DistToPlayer() =>
        _player == null ? Mathf.Infinity
                        : Vector2.Distance(transform.position, _player.position);

    // ── 기즈모 ─────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }

    // ── 임시 체력 UI (Enemy.cs 방식 차용, 추후 전용 UI로 교체) ──
    private GUIStyle _labelStyle;
    private static Texture2D _barTex;
    private static Texture2D BarTex()
    {
        if (_barTex == null) { _barTex = new Texture2D(1,1); _barTex.SetPixel(0,0,Color.white); _barTex.Apply(); }
        return _barTex;
    }

    void OnGUI()
    {
        if (Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
        if (sp.z < 0) return;

        float cx = sp.x, cy = Screen.height - sp.y;
        float bw = 120f, bh = 10f;

        // 배경
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(cx - bw * 0.5f, cy - bh - 4f, bw, bh), BarTex());

        // 체력바
        float frac = Mathf.Clamp01(_currentHealth / maxHealth);
        GUI.color = Color.Lerp(new Color(0.85f, 0.16f, 0.12f), new Color(0.4f, 0.82f, 0.2f), frac);
        GUI.DrawTexture(new Rect(cx - bw * 0.5f + 1f, cy - bh - 3f, (bw - 2f) * frac, bh - 2f), BarTex());
        GUI.color = Color.white;

        // 페이즈 텍스트
        if (_labelStyle == null)
            _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.white } };
        GUI.Label(new Rect(cx - 60f, cy - bh - 22f, 120f, 18f),
                  $"BOSS  {currentPhase}  HP {Mathf.Max(0, _currentHealth):0}/{maxHealth:0}", _labelStyle);
    }
}
