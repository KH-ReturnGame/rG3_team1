using UnityEngine;

/// <summary>
/// 보스 공격 히트박스. 스폰 오브젝트(지면 파동, 폭탄 등) 또는
/// 보스 자식 오브젝트(근접 판정 영역)에 부착해 사용.
///
/// isParryable = false → 스펙의 패턴 1(지면파동), 패턴 6(폭탄투척)
/// isParryable = true  → 패턴 4(기본 평타) 등 패링 가능 공격
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossHitbox : MonoBehaviour
{
    [Tooltip("이 공격을 패링할 수 있는지 여부. 패턴 1/6은 항상 false.")]
    public bool isParryable = true;

    [Tooltip("플레이어에게 가하는 피해량")]
    public float damage = 10f;

    [Tooltip("히트 후 히트박스 비활성화 (단발성 공격에 사용)")]
    public bool disableOnHit = true;

    // 패링 성공 시 ApplyGroggy()를 받을 보스 참조
    [HideInInspector] public IParryable owner;

    private bool _hasHit;

    /// <summary>
    /// 패턴에서 동적으로 히트박스를 초기화할 때 사용.
    /// </summary>
    public void Init(float dmg, bool parryable, IParryable ownerRef)
    {
        damage    = dmg;
        isParryable = parryable;
        owner     = ownerRef;
        _hasHit   = false;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 패턴 재사용 시 타격 기록 초기화.
    /// </summary>
    public void ResetHit() => _hasHit = false;

    private void OnEnable()  => _hasHit = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasHit) return;
        if (!other.CompareTag("Player")) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        _hasHit = true;

        // isParryable = true 면 isMeleeAttacker = true로 전달 → PlayerController 내부에서 패링 체크
        pc.TakeDamage(damage, isParryable, isParryable ? owner : null, transform.position);

        if (disableOnHit) gameObject.SetActive(false);
    }
}
