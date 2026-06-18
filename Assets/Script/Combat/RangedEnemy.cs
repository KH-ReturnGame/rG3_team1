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

    public override bool IsParryableMelee => false;   // 원거리는 패링 튜토리얼 대상 아님(투사체는 발사 후 판정)

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

    protected override void DoStrikeHit()   // Strike 시점에 투사체 발사
    {
        if (projectilePrefab == null || player == null) return;
        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 0.2f;
        Vector2 toPlayer = (Vector2)(player.position - origin);
        GameObject go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        Projectile proj = go.GetComponent<Projectile>();
        if (proj != null) proj.Init(toPlayer, projectileSpeed, attackDamage);
    }
}
