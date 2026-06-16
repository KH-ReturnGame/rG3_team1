using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 6에서 스폰되는 폭탄 투사체.
/// 바닥 접촉 후 explosionDelay 초 뒤에 폭발 판정 발생.
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

    // 패링 적용 대상 (null = 패링 불가)
    [HideInInspector] public IParryable owner;

    private bool _landed;
    private bool _exploded;
    private LayerMask _playerMask;

    void Awake()
    {
        _playerMask = LayerMask.GetMask("Player");

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = false; // 물리 충돌로 바닥 감지
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (_landed || _exploded) return;

        // 바닥 또는 지형에 닿으면 폭발 타이머 시작
        if (!col.gameObject.CompareTag("Player"))
        {
            _landed = true;
            StartCoroutine(ExplodeAfterDelay());
        }
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
