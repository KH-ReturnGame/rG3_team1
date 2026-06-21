using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 6에서 스폰되는 폭탄 투사체.
/// 바닥 또는 벽에 닿았을 때만 explosionDelay 초 뒤 폭발 판정 발생.
/// (플레이어, 보스 자신, 다른 폭탄 등과의 충돌은 무시)
///
/// [패링 불가] isParryable = false (광역 폭발)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BossScatterBomb : MonoBehaviour
{
    [Tooltip("착탄 후 폭발까지 딜레이 (초). [CONFIRM] 즉시 폭발 vs 지연 폭발 확정 필요.")]
    public float explosionDelay = 0.5f;
    [Tooltip("폭발 반경")]
    public float explosionRadius = 2f;
    [Tooltip("폭발 데미지")]
    public float damage = 15f;
    [Tooltip("폭발 이펙트 프리팹 (없으면 스킵)")]
    public GameObject explosionEffectPrefab;

    [Header("착탄 판정")]
    [Tooltip("폭발을 트리거할 바닥 레이어. 비워두면 \"Ground\" 레이어로 자동 설정.")]
    public LayerMask groundLayer;
    [Tooltip("폭발을 트리거할 벽 레이어. 비워두면 groundLayer와 동일하게 처리.")]
    public LayerMask wallLayer;

    // 패링 적용 대상 (null = 패링 불가)
    [HideInInspector] public IParryable owner;

    private bool _landed;
    private bool _exploded;
    private LayerMask _playerMask;
    private CircleCollider2D _col;

    void Awake()
    {
        _playerMask = LayerMask.GetMask("Player");
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
        if (wallLayer.value == 0) wallLayer = groundLayer;

        _col = GetComponent<CircleCollider2D>();
        _col.isTrigger = false; // 물리 충돌로 바닥/벽 감지
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        TryLand(col.gameObject.layer);
    }

    // Tilemap/Composite Collider 환경에서 OnCollisionEnter2D가 기대대로 안 들어오는 경우를 대비한
    // 보조 안전장치 — 매 프레임 직접 바닥/벽 근접 여부를 확인해서 동일하게 착탄 처리.
    void Update()
    {
        if (_landed || _exploded || _col == null) return;

        Collider2D hit = Physics2D.OverlapCircle(
            transform.position, _col.radius * 1.05f, groundLayer | wallLayer);
        if (hit != null) TryLand(hit.gameObject.layer);
    }

    void TryLand(int otherLayer)
    {
        if (_landed || _exploded) return;

        // 바닥 또는 벽 레이어에 닿았을 때만 폭발 타이머 시작
        int layerBit = 1 << otherLayer;
        bool isGroundOrWall = ((groundLayer.value | wallLayer.value) & layerBit) != 0;
        if (!isGroundOrWall) return;

        _landed = true;
        StartCoroutine(ExplodeAfterDelay());
    }

    IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(explosionDelay);
        Explode();
    }

    void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        // 이펙트
        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

        // 범위 내 플레이어 데미지
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, _playerMask);
        foreach (var hit in hits)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            // isParryable = false → 폭발은 패링 불가
            pc?.TakeDamage(damage, false, null, transform.position);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
