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
    public float bobAmplitude = 0.4f;   // 대기 시 위아래 흔들림
    public float bobSpeed = 2.5f;

    private Vector2 chargeDir;
    private float bobPhase;

    protected override void Start()
    {
        base.Start();
        if (rb != null) rb.gravityScale = 0f;      // 비행 → 중력 없음
        bobPhase = Random.value * 6.2831853f;
    }

    private float Dist2D() => player == null ? Mathf.Infinity : Vector2.Distance(player.position, transform.position);
    private void FaceMoveDir(float vx) { if (Mathf.Abs(vx) > 0.05f) dir = vx >= 0 ? 1 : -1; }

    protected override void TickPatrol()   // 스폰 근처를 떠다니며 대기, 2D 거리로 감지
    {
        Vector2 home = spawnPos + new Vector2(0f, Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude);
        Vector2 to = home - (Vector2)transform.position;
        SetVelocity(Vector2.ClampMagnitude(to * 3f, flySpeed));   // 천천히 제자리 유지
        if (Dist2D() <= detectRange) state = State.Chase;
    }

    protected override void TickChase()
    {
        if (player == null || Dist2D() > detectRange * 1.3f) { state = State.Patrol; return; }
        Vector2 to = (Vector2)(player.position - transform.position);
        FaceMoveDir(to.x);
        if (to.magnitude <= attackRange) { SetVelocity(Vector2.zero); if (attackCdTimer <= 0) BeginAttack(); }
        else SetVelocity(to.normalized * flySpeed);
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
        SetVelocity(chargeDir * chargeSpeed);
        if (!struck && player != null && Dist2D() <= contactRange)
        {
            struck = true;
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(attackDamage, true, this, transform.position);   // 돌진 = 근접(패링 가능)
        }
        if (state != State.Strike) return;   // 패링당해 그로기로 바뀌면 중단
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { state = State.Recover; stateTimer = attackRecover; }
    }

    protected override void TickRecover()  // 돌진 후 감속
    {
        Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
        SetVelocity(Vector2.Lerp(v, Vector2.zero, 10f * Time.deltaTime));
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0) { attackCdTimer = attackCooldown; state = State.Chase; }
    }

    protected override void TickGroggy()   // 그로기: 제자리 정지(중력 없으니 안 떨어짐)
    {
        SetVelocity(Vector2.zero);
        base.TickGroggy();                 // groggyTimer 관리 + 복귀(SetMove(0)은 x만 → 그대로 0)
    }
}
