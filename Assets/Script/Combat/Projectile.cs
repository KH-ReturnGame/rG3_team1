using UnityEngine;

// 원거리 몹이 쏘는 투사체. 직선으로 날아가 플레이어에 닿으면 피해, 벽/바닥에 닿거나 수명 끝나면 소멸.
// 필요: Rigidbody2D(Dynamic, gravityScale 0) + Collider2D(isTrigger). RangedEnemy가 Init으로 발사.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    public float life = 4f;                 // 최대 생존 시간(초)
    public LayerMask blockMask;             // 여기 닿으면 소멸(보통 Ground)
    public bool faceDirection = true;       // 진행 방향으로 회전

    private float damage = 1f;
    private float timer;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    // 발사: 방향·속도·피해 설정
    public void Init(Vector2 direction, float speed, float dmg)
    {
        damage = dmg;
        Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = d * speed;
        if (faceDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= life) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(damage, false, null, transform.position);   // 원거리 = 비근접(패링 시 막히지만 그로기는 안 됨)
            Destroy(gameObject);
            return;
        }
        if (((1 << other.gameObject.layer) & blockMask.value) != 0)   // 벽/바닥
            Destroy(gameObject);
    }
}
