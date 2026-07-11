using UnityEngine;

// Cainos 던전 트랩(회전 칼날 / 가시 창 / 스파이크 볼 등) 프리팹에 붙이는 데미지 어댑터.
// 에셋의 트랩 스크립트는 '움직임 연출'만 하고 피해는 없음 — 이 컴포넌트를 날/가시 오브젝트
// (Collider2D 있는 곳)에 붙이면 플레이어가 닿을 때 아프다. 콜라이더는 트리거 권장.
public class TrapDamage : MonoBehaviour
{
    public float damage = 1f;          // 하트 칸 수(0.5 = 반칸)
    public float hitCooldown = 0.8f;   // 재타격 간격(초) — 닿아있는 동안 연타 방지
    public bool unblockable = false;   // true = 가드·패링 무시(위험 트랩용)

    private float lastHit = -99f;

    void OnTriggerEnter2D(Collider2D c) { TryHit(c); }
    void OnTriggerStay2D(Collider2D c) { TryHit(c); }
    void OnCollisionEnter2D(Collision2D c) { TryHit(c.collider); }

    private void TryHit(Collider2D c)
    {
        if (Time.time - lastHit < hitCooldown) return;
        var pc = c.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        lastHit = Time.time;
        pc.TakeDamage(damage, false, null, transform.position, false, unblockable);
    }
}
