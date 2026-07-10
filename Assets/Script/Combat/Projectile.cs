using UnityEngine;

// 원거리 몹이 쏘는 투사체. 직선으로 날아가 플레이어에 닿으면 피해, 벽/바닥에 닿거나 수명 끝나면 소멸.
// ★패링(우클릭 타이밍)에 맞으면 발사자를 향해 '반사'된다 — 반사탄은 적을 맞혀 피해를 준다.
// 필요: Rigidbody2D(Dynamic, gravityScale 0) + Collider2D(isTrigger). RangedEnemy가 Init으로 발사.
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    public float life = 4f;                 // 최대 생존 시간(초)
    public LayerMask blockMask;             // 여기 닿으면 소멸(보통 Ground)
    public bool faceDirection = true;       // 진행 방향으로 회전

    [Header("패링 반사")]
    public float reflectSpeedMult = 1.5f;    // 반사 시 속도 배율(저스트는 x1.25 추가)
    // 반사탄 피해 = '플레이어 공격력(버프 포함)' × 이 배율(저스트는 x1.5 추가).
    // ※적 공격력(하트 단위) 기준이 아님 — 적 체력 스케일(수십)에 맞는 유효한 딜이 들어가게.
    public float reflectDamageMult = 1.2f;

    private float damage = 1f;
    private float timer;
    private Rigidbody2D rb;
    private Transform owner;                // 발사자 — 반사 시 되돌아갈 목표
    private bool reflected;                 // 반사됨 → 이제 적을 맞힌다
    private bool reflectedJust;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    // 발사: 방향·속도·피해 설정(shooter는 반사 목표용 — 안 넘기면 반사 시 왔던 길로 되돌아감)
    public void Init(Vector2 direction, float speed, float dmg, Transform shooter = null)
    {
        damage = dmg;
        owner = shooter;
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
        if (!reflected)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                bool just;
                if (pc.TryDeflectProjectile(transform.position, out just)) { Reflect(just); return; }   // 패링 → 반사!
                pc.TakeDamage(damage, false, null, transform.position);   // 원거리 = 비근접
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Enemy en = other.GetComponentInParent<Enemy>();
            if (en != null)
            {
                // 플레이어 공격력 기준(스탯·버프 반영) — 평타보다 살짝 아프게, 저스트는 확실한 보상
                float baseAtk = PlayerController.Instance != null ? PlayerController.Instance.attackDamage : 10f;
                float atkMult = GameManager.Instance != null ? GameManager.Instance.AttackMultiplier : 1f;
                en.TakeDamage(baseAtk * atkMult * reflectDamageMult * (reflectedJust ? 1.5f : 1f));
                Juice.Hit();
                Destroy(gameObject);
                return;
            }
        }
        if (((1 << other.gameObject.layer) & blockMask.value) != 0)   // 벽/바닥
            Destroy(gameObject);
    }

    // 패링 반사: 발사자를 향해(없으면 왔던 방향 그대로) 더 빠르게 되돌아간다.
    private void Reflect(bool just)
    {
        reflected = true;
        reflectedJust = just;
        timer = 0f;   // 수명 리셋 — 되돌아갈 시간 확보
        float spd = Mathf.Max(6f, rb.linearVelocity.magnitude * reflectSpeedMult * (just ? 1.25f : 1f));
        Vector2 d = owner != null
            ? ((Vector2)owner.position + Vector2.up * 0.3f - (Vector2)transform.position).normalized
            : (rb.linearVelocity.sqrMagnitude > 0.001f ? -rb.linearVelocity.normalized : Vector2.right);
        rb.linearVelocity = d * spd;
        if (faceDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0.72f, 0.35f);   // 주황빛 = 반사탄(내 것)
    }
}
