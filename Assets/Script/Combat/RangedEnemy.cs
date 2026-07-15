using UnityEngine;

// 원거리 몹: 거리를 유지하며(가까우면 후퇴) 투사체를 발사한다.
// Enemy의 체력/그로기/패링/전리품/사망/체력바를 그대로 상속하고 이동·공격만 교체.
public class RangedEnemy : Enemy
{
    [Header("원거리 (투사체)")]
    public GameObject projectilePrefab;     // Projectile 컴포넌트가 붙은 프리팹
    public float projectileSpeed = 9f;
    public float preferredDistance = 4.5f;  // 유지하려는 거리(이보다 가까우면 후퇴) — attackRange보다 작게
    public Transform firePoint;             // 발사 위치(비우면 자기 위치 + 약간 위)
    [Range(0f, 89f)] public float aimAngleLimit = 80f;   // 수평 기준 조준 각도 제한(도). 80=사실상 플레이어 정조준(직선 궤적). 낮추면 수평탄

    [Header("레이저 조준(예비동작 텔레그래프)")]
    public float aimLockTime = 0.25f;    // 발사 직전 이 시간 동안 조준 고정(피할 기회)
    public float laserLength = 14f;
    private LineRenderer laser;
    private Vector2 lockedDir = Vector2.right;
    private bool aimLocked;

    public override bool IsParryableMelee => false;   // 원거리는 패링 튜토리얼 대상 아님(투사체는 발사 후 판정)
    public override bool RangedPrecog => true;        // 예지: 예비동작이 아니라 투사체가 근접했을 때 정지

    protected override void Start()
    {
        base.Start();
        BuildLaser();
    }

    private void BuildLaser()
    {
        var go = new GameObject("AimLaser");
        go.transform.SetParent(transform, false);
        laser = go.AddComponent<LineRenderer>();
        laser.material = new Material(Shader.Find("Sprites/Default"));
        laser.startWidth = 0.05f; laser.endWidth = 0.02f;
        laser.positionCount = 2;
        laser.sortingOrder = 15;
        laser.useWorldSpace = true;
        laser.enabled = false;
    }

    // attackRange = '사격 사거리'로 사용(인스펙터에서 크게: 6~7 권장)
    protected override void TickChase()
    {
        if (player == null || DistToPlayer() > detectRange) { state = State.Patrol; return; }

        dir = player.position.x >= transform.position.x ? 1 : -1;   // 항상 플레이어 바라봄
        float d = DistToPlayer();

        if (d < preferredDistance - 0.3f) SetMove(-dir * moveSpeed);   // 너무 가까움 → 뒤로
        else if (d > attackRange) SetMove(dir * moveSpeed);            // 사거리 밖 → 접근
        else SetMove(0);                                              // 적정 거리 → 정지

        if (d <= attackRange && attackCdTimer <= 0) BeginAttack();    // 사거리 안 + 쿨다운 → 발사 시퀀스(예비동작→발사)
    }

    private Vector3 FireOrigin() => firePoint != null ? firePoint.position : transform.position + Vector3.up * 0.2f;

    // 조준점 = 플레이어 '몸통'(콜라이더 중심). player.position은 발밑 피벗이라 그대로 쓰면 발을 겨냥함.
    private Vector2 PlayerAimPoint()
    {
        if (player == null) return Vector2.zero;
        var col = player.GetComponent<Collider2D>();
        return col != null ? (Vector2)col.bounds.center : (Vector2)player.position + Vector2.up * 0.8f;
    }

    // 조준 각도를 수평 기준 ±aimAngleLimit로 클램프 — 대각선 저격 대신 '직선탄'에 가깝게
    private Vector2 ClampAim(Vector2 toPlayer)
    {
        float ang = Mathf.Atan2(toPlayer.y, Mathf.Abs(toPlayer.x)) * Mathf.Rad2Deg;   // 수평 대비 상하각
        ang = Mathf.Clamp(ang, -aimAngleLimit, aimAngleLimit);
        float sign = toPlayer.x >= 0f ? 1f : -1f;
        return new Vector2(sign * Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
    }

    protected override void BeginAttack()
    {
        base.BeginAttack();
        aimLocked = false;   // 새 사격 시퀀스 — 조준 추적부터 다시
    }

    // 예비동작: 레이저가 플레이어를 따라가다 발사 직전(aimLockTime) 방향 고정 → 피할 기회
    protected override void TickWindup()
    {
        SetMove(0);
        stateTimer -= Time.deltaTime;

        Vector3 origin = FireOrigin();
        if (!aimLocked)
        {
            if (player != null) lockedDir = ClampAim(PlayerAimPoint() - (Vector2)origin);   // 몸통 조준
            if (stateTimer <= aimLockTime) aimLocked = true;   // 이후로는 방향 고정
        }
        UpdateLaser(origin);

        if (stateTimer <= 0) { state = State.Strike; stateTimer = attackActive; }
    }

    private void UpdateLaser(Vector3 origin)
    {
        if (laser == null) return;
        laser.enabled = true;
        // 벽에 막히면 거기까지만
        float len = laserLength;
        var hit = Physics2D.Raycast(origin, lockedDir, laserLength, LayerMask.GetMask("Ground"));
        if (hit.collider != null) len = hit.distance;
        laser.SetPosition(0, origin);
        laser.SetPosition(1, origin + (Vector3)(lockedDir * len));
        // 추적 중 = 흐린 빨강, 고정 = 진한 빨강(발사 임박 신호)
        Color c = aimLocked ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(1f, 0.3f, 0.2f, 0.35f);
        laser.startColor = c; laser.endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
    }

    void LateUpdate()
    {
        if (laser != null && laser.enabled && state != State.Windup) laser.enabled = false;   // 예비동작 밖에선 항상 끔
    }

    protected override void DoStrikeHit()   // Strike 시점: 고정된 레이저 방향 그대로 발사
    {
        if (projectilePrefab == null) return;
        Vector3 origin = FireOrigin();
        Vector2 dir = aimLocked ? lockedDir
            : (player != null ? ClampAim(PlayerAimPoint() - (Vector2)origin) : new Vector2(this.dir, 0f));   // 안전망(몸통 조준)

        GameObject go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        Projectile proj = go.GetComponent<Projectile>();
        if (proj != null) proj.Init(dir, projectileSpeed, attackDamage, transform);   // 자신을 넘겨 반사 목표로
    }
}
