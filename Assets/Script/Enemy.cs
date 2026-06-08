using UnityEngine;

// 재사용 가능한 적 베이스 (간단한 지상 근접 AI)
// 순찰 → 플레이어 감지 → 접근 → 예비동작(텔레그래프) → 타격(패링 가능) → 경직/쿨다운
// 개별 몬스터는 이 클래스를 상속해 값만 바꾸거나 Die() 등을 오버라이드해 확장.
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour, IDamageable, IParryable
{
    private enum State { Patrol, Chase, Windup, Strike, Recover, Groggy, Dead }
    public enum MoveBehavior { Patrol, Stationary, Wander }   // 비전투 시 이동 방식

    [Header("Stats")]
    public float maxHealth = 60f;
    public float moveSpeed = 2f;

    [Header("Detection / Attack")]
    public float detectRange = 6f;       // 이 안에 플레이어가 들어오면 추격
    public float attackRange = 1.4f;     // 이 안이면 공격 시작
    public float attackDamage = 2f;      // 플레이어에게 주는 피해 = 하트 칸 수
    public float attackWindup = 0.45f;   // 예비동작(텔레그래프) — 플레이어가 패링 노리는 구간
    public float attackActive = 0.1f;    // 실제 타격 판정이 나가는 순간
    public float attackRecover = 0.5f;   // 공격 후 경직
    public float attackCooldown = 1.0f;  // 다음 공격까지 추가 대기

    [Header("Groggy (패링당했을 때)")]
    public float groggyDuration = 2f;
    public float groggyDamageMultiplier = 1.5f;

    [Header("Movement Behavior")]
    public MoveBehavior moveBehavior = MoveBehavior.Patrol;
    public bool randomizeStats = true;        // 인스턴스마다 값을 ±로 흔들어 "줄 맞춰 움직이는" 것 방지
    [Range(0f, 0.9f)] public float randomizeRange = 0.25f;
    public float patrolDistance = 3f;         // 스폰 위치 기준 좌우 순찰 거리
    public float wanderChangeInterval = 2f;   // Wander: 한 방향으로 걷는 시간
    public float wanderPauseTime = 1.2f;      // Wander: 중간중간 가만히 서 있는 시간

    [Header("References")]
    public Transform player;             // 비워두면 씬에서 자동으로 PlayerController 탐색

    [Header("임시 비주얼 (placeholder)")]
    public bool showHealthLabel = true;
    public float labelHeight = 1.2f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Color baseColor;
    private GUIStyle labelStyle;

    private float currentHealth;
    private State state;
    private float stateTimer;       // 공격 단계 잔여 시간
    private float attackCdTimer;    // 공격 쿨다운
    private float groggyTimer;
    private float hitFlashTimer;
    private Vector2 spawnPos;
    private int dir = 1;            // 이동/바라보는 방향(1=오른쪽)
    private bool struck;            // 이번 공격에서 이미 타격을 줬는지
    private float wanderTimer;
    private bool wanderPausing;

    // 임시 색 구분
    private readonly Color windupColor = new Color(1f, 0.85f, 0.2f); // 노랑(예비동작)
    private readonly Color strikeColor = Color.red;                  // 타격 순간
    private readonly Color groggyColor = Color.cyan;                 // 그로기
    private readonly Color hitColor = Color.white;                   // 맞는 순간

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        spawnPos = transform.position;
        if (sr != null) baseColor = sr.color;
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (randomizeStats)
        {
            moveSpeed      *= RandFactor();
            patrolDistance *= RandFactor();
            detectRange    *= RandFactor();
            attackCooldown *= RandFactor();
            dir = Random.value < 0.5f ? -1 : 1;
            wanderTimer = Random.Range(0f, wanderChangeInterval);   // 위상 분산(동시 행동 방지)
            attackCdTimer = Random.Range(0f, attackCooldown);       // 공격 타이밍 분산
        }

        state = State.Patrol;
    }

    private float RandFactor() => 1f + Random.Range(-randomizeRange, randomizeRange);

    void Update()
    {
        if (hitFlashTimer > 0) hitFlashTimer -= Time.deltaTime;
        if (attackCdTimer > 0) attackCdTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:  TickPatrol();  break;
            case State.Chase:   TickChase();   break;
            case State.Windup:  TickWindup();  break;
            case State.Strike:  TickStrike();  break;
            case State.Recover: TickRecover(); break;
            case State.Groggy:  TickGroggy();  break;
            case State.Dead:    SetMove(0);    break;
        }

        UpdateColor();
        if (sr != null) sr.flipX = dir < 0;
    }

    private float DistToPlayer()
    {
        return player == null ? Mathf.Infinity : Mathf.Abs(player.position.x - transform.position.x);
    }

    private void TickPatrol()   // 비전투(논어그로) 상태: 행동 방식에 따라 이동
    {
        switch (moveBehavior)
        {
            case MoveBehavior.Patrol:     PatrolMove(); break;
            case MoveBehavior.Stationary: SetMove(0);   break;
            case MoveBehavior.Wander:     WanderMove(); break;
        }

        if (DistToPlayer() <= detectRange)
        {
            dir = player.position.x >= transform.position.x ? 1 : -1;   // 발견 시 플레이어 쪽으로 한 번 돌아봄
            state = State.Chase;
        }
    }

    private void PatrolMove()
    {
        float offset = transform.position.x - spawnPos.x;
        if (offset > patrolDistance) dir = -1;
        else if (offset < -patrolDistance) dir = 1;
        SetMove(dir * moveSpeed);
    }

    private void WanderMove()   // 좌우로 걷다가 중간중간 가만히 서 있기
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0)
        {
            wanderPausing = !wanderPausing;   // 걷기 ↔ 멈춤 전환
            if (wanderPausing)
            {
                wanderTimer = wanderPauseTime * Random.Range(0.6f, 1.4f);      // 가만히 있는 시간
            }
            else
            {
                wanderTimer = wanderChangeInterval * Random.Range(0.6f, 1.4f); // 걷는 시간
                dir = Random.value < 0.5f ? -1 : 1;                            // 걸을 때 방향 새로 정함
            }
        }

        // 스폰에서 너무 멀어지면 걸어서 되돌아오게
        float offset = transform.position.x - spawnPos.x;
        if (Mathf.Abs(offset) > patrolDistance * 1.5f)
        {
            dir = offset > 0 ? -1 : 1;
            wanderPausing = false;
        }

        SetMove(wanderPausing ? 0f : dir * moveSpeed);
    }

    private void TickChase()
    {
        if (DistToPlayer() > detectRange) { state = State.Patrol; return; }

        if (DistToPlayer() <= attackRange)
        {
            // 사거리 안: 멈춰서 대기(좌우로 흔들지 않음). 쿨다운 끝나면 공격.
            SetMove(0);
            if (attackCdTimer <= 0) BeginAttack();
            return;
        }

        // 사거리 밖: 플레이어를 향해 접근(이때만 방향 갱신)
        dir = player.position.x >= transform.position.x ? 1 : -1;
        SetMove(dir * moveSpeed);
    }

    private void BeginAttack()
    {
        // 방향 재조정 안 함 — 사거리에 들어올 때 향한 방향 그대로 공격(공격 직전 방향 전환 X)
        SetMove(0);
        struck = false;
        state = State.Windup;
        stateTimer = attackWindup;
    }

    private void TickWindup()
    {
        SetMove(0);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Strike; stateTimer = attackActive; }
    }

    private void TickStrike()
    {
        SetMove(0);
        if (!struck)
        {
            struck = true;
            if (player != null && DistToPlayer() <= attackRange + 0.3f)
            {
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.TakeDamage(attackDamage, true, this, transform.position);
            }
            if (state != State.Strike) return;   // 패링당해 그로기로 바뀌면 중단
        }
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Recover; stateTimer = attackRecover; }
    }

    private void TickRecover()
    {
        SetMove(0);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { attackCdTimer = attackCooldown; state = State.Chase; }
    }

    private void TickGroggy()
    {
        SetMove(0);
        groggyTimer -= Time.deltaTime;
        if (groggyTimer <= 0) state = State.Chase;
    }

    private void SetMove(float vx)
    {
        if (rb != null) rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    // ── IDamageable ── (플레이어 공격에 맞음)
    public void TakeDamage(float damage)
    {
        if (state == State.Dead) return;
        if (state == State.Groggy) damage *= groggyDamageMultiplier;   // 그로기 중 치명타

        currentHealth -= damage;
        hitFlashTimer = 0.08f;

        if (currentHealth <= 0) Die();
    }

    // ── IParryable ── (패링당함)
    public void ApplyGroggy()
    {
        if (state == State.Dead) return;
        state = State.Groggy;
        groggyTimer = groggyDuration;
        SetMove(0);
    }

    protected virtual void Die()
    {
        state = State.Dead;
        SetMove(0);
        // TODO: 드랍/사망 연출은 이후 단계에서. 지금은 제거.
        Destroy(gameObject);
    }

    private void UpdateColor()
    {
        if (sr == null) return;
        if (hitFlashTimer > 0)        sr.color = hitColor;
        else if (state == State.Groggy) sr.color = groggyColor;
        else if (state == State.Strike) sr.color = strikeColor;
        else if (state == State.Windup) sr.color = windupColor;
        else                            sr.color = baseColor;
    }

    private void OnGUI()
    {
        if (!showHealthLabel || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * labelHeight);
        if (sp.z < 0) return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
        labelStyle.normal.textColor = state == State.Groggy ? Color.cyan : Color.white;
        string txt = $"HP {Mathf.Max(0, currentHealth):0}/{maxHealth:0}" + (state == State.Groggy ? "  (그로기)" : "");
        GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 22, 120, 20), txt, labelStyle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
