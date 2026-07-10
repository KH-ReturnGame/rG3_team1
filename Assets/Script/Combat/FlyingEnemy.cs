using UnityEngine;

// 공중 몹: 중력 무시하고 비행하다 플레이어를 향해 '돌진(charge)'한다.
// Enemy의 체력/그로기/패링/전리품/사망/체력바를 상속하고, 이동(2D 비행)·공격(돌진)만 교체.
// 콜라이더는 isTrigger 권장(지형 통과 + 플레이어 공격엔 피격). Rigidbody2D는 Dynamic(중력 0으로 자동 세팅).
public class FlyingEnemy : Enemy
{
    [Header("공중 (돌진)")]
    public float flySpeed = 3.5f;       // 평소/추격 비행 속도
    public float chargeSpeed = 12f;     // 돌진 속도
    public float chargeTime = 0.45f;    // 돌진 지속 시간(직진)
    public float contactRange = 0.85f;  // 돌진 중 이 거리 안이면 피해
    public float bobAmplitude = 0.4f;   // (구) 사인 호버 — 박쥐식 배회로 대체돼 미사용
    public float bobSpeed = 2.5f;       // (구)

    [Header("배회 비행(박쥐식) — 호버 대신 파닥이며 날아다님")]
    public float wanderRadius = 3.5f;   // 스폰 주변 배회 반경
    public float flapHz = 9f;           // 날개 파닥임 주파수(빠른 잔진동)
    public float flutterAmp = 1.0f;     // 파닥임 세기(수직 속도)

    [Header("돌진 사이클 — 위에서 맴돌다 급강하, 짧게 휘청이고 다시 떠오름")]
    public float hoverHeight = 2.5f;    // 추격 시 플레이어 위로 유지하는 고도
    public float diveRecover = 0.35f;   // 돌진 후 경직(짧게) — attackRecover 대신 사용, 끝나면 떠올라 재추격

    [Header("활동 범위(맵 밖 이탈 방지)")]
    public float leashRange = 14f;      // 스폰 기준 최대 거리 — 경계에서 멈추고 돌진도 중단

    private Vector2 wanderTarget;
    private float wanderNext;
    private float flutterSeed;
    private int wallMask;               // 벽/지형(Ground) — 뚫고 못 지나가게 속도 차단

    private Vector2 chargeDir;
    private float bobPhase;

    protected override void Start()
    {
        base.Start();
        if (rb != null) rb.gravityScale = 0f;      // 비행 → 중력 없음
        bobPhase = Random.value * 6.2831853f;
        flutterSeed = Random.Range(0f, 100f);
        wanderTarget = spawnPos;
        wanderNext = Time.time + Random.Range(0.3f, 1.2f);   // 개체별 위상 분산
        wallMask = LayerMask.GetMask("Ground");
    }

    // 날개 파닥임(빠른 잔진동) — 속도에 더해 '펄럭이며 나는' 느낌을 만든다
    private float Flap(float strength = 1f)
        => Mathf.Sin(Time.time * flapHz + bobPhase) * flutterAmp * strength;

    // 벽 차단: 콜라이더가 트리거(지형 통과)라 물리로는 안 막힘 → 이동 방향에 벽이 있으면 그 축 속도를 0으로.
    private Vector2 BlockWalls(Vector2 vel)
    {
        const float skin = 0.45f;   // 벽 앞 여유
        if (Mathf.Abs(vel.x) > 0.01f &&
            Physics2D.Raycast(transform.position, new Vector2(Mathf.Sign(vel.x), 0f), Mathf.Abs(vel.x) * Time.deltaTime + skin, wallMask).collider != null)
            vel.x = 0f;
        if (Mathf.Abs(vel.y) > 0.01f &&
            Physics2D.Raycast(transform.position, new Vector2(0f, Mathf.Sign(vel.y)), Mathf.Abs(vel.y) * Time.deltaTime + skin, wallMask).collider != null)
            vel.y = 0f;
        return vel;
    }

    private float Dist2D() => player == null ? Mathf.Infinity : Vector2.Distance(player.position, transform.position);
    private void FaceMoveDir(float vx) { if (Mathf.Abs(vx) > 0.05f) dir = vx >= 0 ? 1 : -1; }

    protected override void TickPatrol()   // 박쥐식 배회: 스폰 주변 무작위 지점으로 파닥이며 날아다님
    {
        // 목표점에 가까워졌거나 시간이 되면 새 무작위 지점 선정(다음 목표가 위/아래로도 튀게)
        if (Time.time >= wanderNext || ((Vector2)transform.position - wanderTarget).sqrMagnitude < 0.35f)
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            rnd.y *= 0.6f;                                  // 수평으로 더 넓게(박쥐처럼)
            wanderTarget = spawnPos + rnd;
            wanderNext = Time.time + Random.Range(0.7f, 1.8f);
        }

        Vector2 to = wanderTarget - (Vector2)transform.position;
        FaceMoveDir(to.x);
        // 목표로 향하는 부드러운 속도 + 날개 파닥임(수직 잔진동) → 위아래로 펄럭이며 전진
        Vector2 seek = Vector2.ClampMagnitude(to * 2.2f, flySpeed);
        seek.y += Flap();                                   // 파닥임
        SetVelocity(BlockWalls(seek));

        if (Dist2D() <= detectRange) state = State.Chase;
    }

    protected override void TickChase()
    {
        if (player == null || Dist2D() > detectRange * 1.3f) { state = State.Patrol; return; }
        // 플레이어가 활동 범위 밖으로 벗어나면 포기하고 복귀(경계에 매달려 있지 않게)
        if (((Vector2)player.position - spawnPos).magnitude > leashRange + 2f) { state = State.Patrol; return; }

        // 플레이어 '위' 일정 고도를 향해 비행(박쥐가 머리 위를 맴도는 그림) → 수평으로 붙으면 급강하
        Vector2 hoverPt = (Vector2)player.position + Vector2.up * hoverHeight;
        Vector2 to = hoverPt - (Vector2)transform.position;
        FaceMoveDir(player.position.x - transform.position.x);

        float dx = Mathf.Abs(player.position.x - transform.position.x);
        if (dx <= attackRange && attackCdTimer <= 0) { SetVelocity(Vector2.zero); BeginAttack(); return; }

        Vector2 v = Vector2.ClampMagnitude(to * 2.5f, flySpeed);
        v.y += Flap(0.5f);                                      // 쫓을 때도 살짝 파닥임
        SetVelocity(BlockWalls(v));
    }

    protected override void TickWindup()   // 정지 후 돌진 방향 조준(텔레그래프 — 패링 노릴 구간)
    {
        SetVelocity(Vector2.zero);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            chargeDir = player != null ? ((Vector2)(player.position - transform.position)).normalized : new Vector2(dir, 0f);
            FaceMoveDir(chargeDir.x);
            struck = false;
            state = State.Strike;
            stateTimer = chargeTime;
        }
    }

    protected override void TickStrike()   // 돌진(직진) + 접촉 피해
    {
        // 돌진 경로에 벽 → 벽 앞에서 돌진 종료(뚫고 나가지 않음)
        if (Physics2D.Raycast(transform.position, chargeDir, chargeSpeed * Time.deltaTime + 0.5f, wallMask).collider != null)
        {
            SetVelocity(Vector2.zero);
            state = State.Recover; stateTimer = diveRecover;
            return;
        }
        SetVelocity(chargeDir * chargeSpeed);
        if (!struck && player != null && Dist2D() <= contactRange)
        {
            struck = true;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(attackDamage, true, this, transform.position);   // 돌진 = 근접(패링 가능)
        }
        if (state != State.Strike) return;   // 패링당해 그로기로 바뀌면 중단
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Recover; stateTimer = diveRecover; }
    }

    protected override void TickRecover()  // 돌진 후: 짧게 휘청이며 위로 떠오름 → 바로 재추격(고도 복귀)
    {
        Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
        Vector2 rise = Vector2.up * (flySpeed * 0.8f);   // 땅에 머물지 않고 곧장 날아오름
        SetVelocity(BlockWalls(Vector2.Lerp(v, rise, 8f * Time.deltaTime)));
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { attackCdTimer = attackCooldown; state = State.Chase; }
    }

    protected override void TickGroggy()   // 그로기: 제자리 정지(중력 없으니 안 떨어짐)
    {
        SetVelocity(Vector2.zero);
        base.TickGroggy();                 // groggyTimer 관리 + 복귀(SetMove(0)은 x만 → 그대로 0)
    }

    // 최종 안전망: 어떤 상태(돌진·넉백 포함)든 스폰 반경을 절대 못 벗어남
    void LateUpdate()
    {
        Vector2 off = (Vector2)transform.position - spawnPos;
        if (off.magnitude > leashRange)
        {
            transform.position = spawnPos + off.normalized * leashRange;   // 경계에 딱 멈춤
            if (state == State.Strike) { state = State.Recover; stateTimer = diveRecover; SetVelocity(Vector2.zero); }   // 돌진 중단
        }
    }
}
