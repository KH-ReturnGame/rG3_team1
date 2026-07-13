using UnityEngine;

// 재사용 가능한 적 베이스 (간단한 지상 근접 AI)
// 순찰 → 플레이어 감지 → 접근 → 예비동작(텔레그래프) → 타격(패링 가능) → 경직/쿨다운
// 개별 몬스터는 이 클래스를 상속해 값만 바꾸거나 Die() 등을 오버라이드해 확장.
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour, IDamageable, IParryable
{
    protected enum State { Patrol, Chase, Windup, Strike, Recover, Groggy, Dead }
    public enum MoveBehavior { Patrol, Stationary, Wander }   // 비전투 시 이동 방식

    [Header("Stats")]
    public float maxHealth = 60f;
    public float moveSpeed = 2f;

    [Header("Detection / Attack")]
    public float detectRange = 6f;       // 이 안에 플레이어가 들어오면 추격(가로)
    public float detectHeight = 2.2f;    // 감지 세로 허용(±) — 위에서 떨어지는 중엔 안 물게 좁게
    public float attackRange = 1.4f;     // 좌우 공격 도달 거리(가로)
    public float attackHeight = 1.2f;    // 공격 세로 범위(±). 이보다 위로 점프하면 회피 가능
    public float attackDamage = 2f;      // 플레이어에게 주는 피해 = 하트 칸 수
    public float attackWindup = 0.45f;   // 예비동작(텔레그래프) — 플레이어가 패링 노리는 구간
    public float attackActive = 0.1f;    // 실제 타격 판정이 나가는 순간
    public float attackRecover = 0.5f;   // 공격 후 경직
    public float attackCooldown = 1.0f;  // 다음 공격까지 추가 대기
    public float firstAttackDelay = 0.6f;  // 첫 교전(발견) 후 첫 공격까지 추가 유예 — 발견 즉시 무는 느낌 방지

    [Header("Groggy (패링당했을 때)")]
    public float groggyDuration = 2f;
    public float groggyDamageMultiplier = 1.5f;
    public float groggyRecoverDelay = 1.2f;   // 그로기 풀린 직후 재공격까지 대기(없으면 풀자마자 공격)

    [Header("Movement Behavior")]
    public MoveBehavior moveBehavior = MoveBehavior.Patrol;
    public bool randomizeStats = true;        // 인스턴스마다 값을 ±로 흔들어 "줄 맞춰 움직이는" 것 방지
    [Range(0f, 0.9f)] public float randomizeRange = 0.25f;
    public float patrolDistance = 3f;         // 스폰 위치 기준 좌우 순찰 거리
    public float wanderChangeInterval = 2f;   // Wander: 한 방향으로 걷는 시간
    public float wanderPauseTime = 1.2f;      // Wander: 중간중간 가만히 서 있는 시간

    [Header("Drops (처치 보상 — 전리품만, 골드 없음)")]
    public LootDrop[] loot;              // 사망 시 확률로 떨어지는 채집물/전리품(바닥에 떨궈 F로 줍기)
    public float dropSize = 0.5f;        // 떨군 아이템 월드 크기
    public float dropScatter = 0.3f;     // 여러 개일 때 퍼지는 정도

    [Header("References")]
    public Transform player;             // 비워두면 씬에서 자동으로 PlayerController 탐색

    [Header("임시 비주얼 (placeholder)")]
    public bool showHealthLabel = true;
    public bool isBoss = false;          // 보스는 체력바 대신 숫자 유지(나중에 전용 UI)
    public float labelHeight = 1.2f;
    public string questKillId = "";      // 퀘스트 처치 집계용 id(예: slime). 비우면 집계 안 함

    public bool FaceForward = true;      // 진행 방향 바라보기
    public bool InvertSprite = true;    // 스프라이트 방향 뒤집기 (허수아비 이 ㅅㄲ가 스프라이트가 거꾸로임)

    protected Rigidbody2D rb;
    private SpriteRenderer sr;
    private Color baseColor;
    private GUIStyle labelStyle;

    protected float currentHealth;
    protected bool invuln;            // 무적(보스 페이즈 전환 등) — TakeDamage 무시
    protected State state;
    protected float stateTimer;       // 공격 단계 잔여 시간
    protected float attackCdTimer;    // 공격 쿨다운
    private float groggyTimer;
    private float hitFlashTimer;
    protected Vector2 spawnPos;
    protected int dir = 1;            // 이동/바라보는 방향(1=오른쪽)
    protected bool struck;            // 이번 공격에서 이미 타격을 줬는지
    private float wanderTimer;
    private bool wanderPausing;
    private bool engaged;             // 플레이어를 한 번이라도 발견(교전)했는지 — 첫 공격 유예용
    

    // 임시 색 구분
    private readonly Color windupColor = new Color(1f, 0.85f, 0.2f); // 노랑(예비동작)
    private readonly Color strikeColor = Color.red;                  // 타격 순간
    private readonly Color groggyColor = Color.cyan;                 // 그로기
    private readonly Color hitColor = Color.white;                   // 맞는 순간

    // ── 튜토리얼 훅 ── 외부(CombatTutorial)가 '피격 직전(예비동작 진입)'을 감지하기 위한 정적 이벤트(기본 무구독).
    public static System.Action<Enemy> WindupStarted;
    // 컷씬/튜토리얼용: 다음 공격까지의 대기를 외부에서 지정(접근 연출 동안 안 때리게 등)
    public void ArmAttack(float delay) { attackCdTimer = Mathf.Max(0f, delay); }
    public Transform TargetPlayer => player;                          // 이 적이 노리는 대상
    public bool IsAttacking => state == State.Windup || state == State.Strike;
    public bool IsAggro => state != State.Patrol && state != State.Dead;   // 플레이어를 인식해 교전(추격/공격) 중인지
    // 대사창·컷씬(독백/발도 연출 등) 중엔 모든 적의 '공격 개시'를 봉인 — 무방비 플레이어를 때리지 않게(이동·추격은 허용)
    protected static bool AttackHold =>
        DialogueUI.IsOpen || (PlayerController.Instance != null && PlayerController.Instance.cutsceneActive);
    public virtual bool IsParryableMelee => true;                     // 원거리는 false로 오버라이드(투사체는 패링 레슨 제외)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    protected virtual void Start()
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
        // 이 부분 변경함
        if (sr != null && FaceForward == true)
        {
            if (InvertSprite == true)
            {
                sr.flipX = -dir < 0;
            }
            else
            {
                sr.flipX = dir < 0;
            }
        }
    }

    protected float DistToPlayer()
    {
        return player == null ? Mathf.Infinity : Mathf.Abs(player.position.x - transform.position.x);
    }

    protected float DyToPlayer()
    {
        return player == null ? Mathf.Infinity : Mathf.Abs(player.position.y - transform.position.y);
    }

    protected virtual void TickPatrol()   // 비전투(논어그로) 상태: 행동 방식에 따라 이동
    {
        switch (moveBehavior)
        {
            case MoveBehavior.Patrol:     PatrolMove(); break;
            case MoveBehavior.Stationary: SetMove(0);   break;
            case MoveBehavior.Wander:     WanderMove(); break;
        }

        // 세로(detectHeight)도 만족해야 감지 — 위에서 낙하 중인 플레이어를 아래에서 물지 않게
        if (DistToPlayer() <= detectRange && DyToPlayer() <= detectHeight)
        {
            dir = player.position.x >= transform.position.x ? 1 : -1;   // 발견 시 플레이어 쪽으로 한 번 돌아봄
            if (!engaged) { engaged = true; attackCdTimer = Mathf.Max(attackCdTimer, firstAttackDelay); }   // 첫 교전 → 첫 공격까지 유예
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

    protected virtual void TickChase()
    {
        // 추격 이탈: 가로는 여유(1.15배), 세로는 감지의 2배(점프 정도로는 안 놓치고, 완전히 위층이면 놓아줌)
        if (DistToPlayer() > detectRange * 1.15f || DyToPlayer() > detectHeight * 2f) { state = State.Patrol; return; }

        if (DistToPlayer() <= attackRange)
        {
            // 사거리 안: 멈춰서 대기(좌우로 흔들지 않음). 쿨다운 끝났고 '세로도 닿는 높이'일 때만 공격.
            SetMove(0);
            if (attackCdTimer <= 0 && DyToPlayer() <= attackHeight) BeginAttack();
            return;
        }

        // 사거리 밖: 플레이어를 향해 접근(이때만 방향 갱신)
        dir = player.position.x >= transform.position.x ? 1 : -1;
        SetMove(dir * moveSpeed);
    }

    protected virtual void BeginAttack()
    {
        if (AttackHold) return;   // 독백·대사·컷씬 중엔 공격 안 함(모든 적 공통 — 서브클래스도 이 진입점 사용)
        // 방향 재조정 안 함 — 사거리에 들어올 때 향한 방향 그대로 공격(공격 직전 방향 전환 X)
        SetMove(0);
        struck = false;
        state = State.Windup;
        stateTimer = attackWindup;
        WindupStarted?.Invoke(this);   // 피격 직전(예비동작) 알림 — 튜토리얼 패링 레슨 훅
    }

    protected virtual void TickWindup()
    {
        SetMove(0);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Strike; stateTimer = attackActive; }
    }

    protected virtual void TickStrike()
    {
        SetMove(0);
        if (!struck)
        {
            struck = true;
            DoStrikeHit();
            if (state != State.Strike) return;   // 패링당해 그로기로 바뀌면 중단
        }
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Recover; stateTimer = attackRecover; }
    }

    // 실제 타격(근접 박스). 원거리/공중은 이 메서드를 오버라이드해 투사체·돌진 등으로 교체.
    protected virtual void DoStrikeHit()
    {
        if (player != null
            && Mathf.Abs(player.position.x - transform.position.x) <= attackRange + 0.3f   // 양옆
            && Mathf.Abs(player.position.y - transform.position.y) <= attackHeight)         // 위/아래는 좁게 → 점프로 회피
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(attackDamage, true, this, transform.position);
        }
    }

    protected virtual void TickRecover()
    {
        SetMove(0);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { attackCdTimer = attackCooldown; state = State.Chase; }
    }

    protected virtual void TickGroggy()
    {
        SetMove(0);
        groggyTimer -= Time.deltaTime;
        if (groggyTimer <= 0) { state = State.Chase; attackCdTimer = groggyRecoverDelay; }   // 풀린 뒤 바로 안 때리게
    }

    protected void SetMove(float vx)
    {
        if (rb != null) rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    protected void SetVelocity(Vector2 v)   // 공중 몹(2D 이동)용
    {
        if (rb != null) rb.linearVelocity = v;
    }

    // ── IDamageable ── (플레이어 공격에 맞음)
    public void TakeDamage(float damage)
    {
        if (state == State.Dead || invuln) return;
        if (state == State.Groggy) damage *= groggyDamageMultiplier;   // 그로기 중 치명타

        currentHealth -= damage;
        hitFlashTimer = 0.08f;
        DamagePopup.Damage(transform.position + Vector3.up * 0.9f, damage);   // 빨간 데미지 숫자

        if (currentHealth <= 0) Die();
    }

    // ── IParryable ── (패링당함). 보스는 오버라이드해 '누적 후 그로기'로 바꾼다.
    public virtual void ApplyGroggy()
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
        AudioManager.Sfx("enemy_die", 1f, 0.06f);
        GrantRewards();
        if (!string.IsNullOrEmpty(questKillId) && QuestManager.Instance != null) QuestManager.Instance.ReportKill(questKillId);   // 처치 퀘스트 진행
        Destroy(gameObject);
    }

    // 처치 보상: 골드는 떨구지 않음 — 적은 각자 맞는 전리품(loot)을 확률로 바닥에 떨궈 F로 줍게(상점에 팔아 환금).
    private void GrantRewards()
    {
        if (loot == null) return;
        Vector3 basePos = LootBasePos();
        foreach (var d in loot)
        {
            if (d == null || d.item == null || Random.value > d.chance) continue;
            int n = Random.Range(d.minCount, d.maxCount + 1);
            if (n <= 0) continue;
            Vector3 pos = basePos + (Vector3)(Random.insideUnitCircle * dropScatter) + Vector3.up * 0.2f;
            ItemPickup.SpawnWorld(d.item, n, pos, dropSize);
        }
    }

    // 전리품 기준점: 사망 지점 아래에 지면이 없으면(공중몹이 맵 밖·허공에서 죽음) 플레이어가 주울 수 있는 곳으로.
    private Vector3 LootBasePos()
    {
        int mask = LayerMask.GetMask("Ground");
        if (Physics2D.Raycast(transform.position + Vector3.up * 0.1f, Vector2.down, 25f, mask).collider != null)
            return transform.position;   // 아래에 지면 있음 → 그대로
        var pc = PlayerController.Instance;
        if (pc != null)
        {
            var hit = Physics2D.Raycast(pc.transform.position + Vector3.up * 0.1f, Vector2.down, 25f, mask);
            if (hit.collider != null) return new Vector3(pc.transform.position.x, hit.point.y + 0.4f, 0f);
            return pc.transform.position;   // 플레이어 발밑도 못 찾으면 플레이어 위치
        }
        return transform.position;
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

    private static Texture2D _barTex;
    private static Texture2D BarTex()
    {
        if (_barTex == null) { _barTex = new Texture2D(1, 1); _barTex.SetPixel(0, 0, Color.white); _barTex.Apply(); }
        return _barTex;
    }

    private void OnGUI()
    {
        if (!showHealthLabel || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * labelHeight);
        if (sp.z < 0) return;
        float cx = sp.x, cy = Screen.height - sp.y;

        if (isBoss)
        {
            // 보스: 숫자 유지(나중에 전용 UI로 교체)
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(cx - 60, cy - 24, 120, 20), $"HP {Mathf.Max(0, currentHealth):0}/{maxHealth:0}", labelStyle);
        }
        else if (currentHealth < maxHealth)
        {
            // 일반 적: 체력이 깎인 순간부터 체력바 표시(최대치면 숨김)
            float bw = 54f, bh = 7f;
            Rect bg = new Rect(cx - bw * 0.5f, cy - bh - 6f, bw, bh);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(bg, BarTex());                                   // 배경
            float frac = Mathf.Clamp01(currentHealth / maxHealth);
            GUI.color = Color.Lerp(new Color(0.85f, 0.16f, 0.12f), new Color(0.45f, 0.85f, 0.2f), frac);
            GUI.DrawTexture(new Rect(bg.x + 1f, bg.y + 1f, (bw - 2f) * frac, bh - 2f), BarTex());   // 남은 체력
            GUI.color = prev;
        }

        // 그로기는 글자 유지(스프라이트 적용 전)
        if (state == State.Groggy)
        {
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            labelStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(cx - 60, cy - 40, 120, 20), "그로기", labelStyle);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

// 적이 사망 시 떨어뜨리는 항목 하나(확률·개수). 인스펙터에서 적별 드랍 테이블 구성.
[System.Serializable]
public class LootDrop
{
    public ItemData item;
    [Range(0f, 1f)] public float chance = 1f;   // 떨어질 확률(1=항상)
    public int minCount = 1;
    public int maxCount = 1;
}
