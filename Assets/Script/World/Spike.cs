using UnityEngine;

// 가시/위험 지대: 닿으면 플레이어가 피해 + 넉백. 트리거 콜라이더로 배치(바닥 가시·천장 가시·용암 등).
//  - 플레이어 피격 후 무적시간(i-frame) 덕에 머물러 있어도 도배되지 않고 주기적으로만 들어감.
[RequireComponent(typeof(Collider2D))]
public class Spike : MonoBehaviour
{
    [Tooltip("닿을 때 주는 피해(하트 칸 수)")]
    public int damage = 1;

    void Reset() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }
    void Awake() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other) { Hit(other); }
    void OnTriggerStay2D(Collider2D other) { Hit(other); }

    private void Hit(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) pc.TakeDamage(damage, false, null, transform.position);   // 비근접(패링 대상 아님) + 넉백 소스
    }
}
