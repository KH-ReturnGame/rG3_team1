using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 7 — 황소 돌진 (Bull Charge)
///
/// 보스가 플레이어 방향으로 고속 돌진해 벽에 박힐 때까지 멈추지 않는다.
/// 선딜(포효/발구르기 예비 동작) → 직선 돌진 → 벽 충돌 → 후딜(경직)
///
/// 플레이어에게는 접촉 즉시 1회 데미지, 이후 통과.
/// 벽(Ground 레이어)에 부딪히면 즉시 정지하고 잠시 경직.
///
/// [CONFIRM] 패링 가능 여부: 현재 hitParryable = true. 확정 후 수정.
/// [CONFIRM] 2페이즈 속도 배율: 현재 1.3×. Inspector에서 조정.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossPattern7_BullCharge : BossPatternBase
{
    [Header("돌진")]
    [Tooltip("돌진 속도")]
    public float chargeSpeed = 22f;
    [Tooltip("선딜레이 — 보스가 포효/발구르기 예비 동작을 취하는 시간 (초)")]
    public float windupDuration = 0.7f;
    [Tooltip("벽에 박힌 뒤 경직 시간 (초)")]
    public float recoverDuration = 0.8f;
    [Tooltip("돌진 안전장치 최대 지속 시간 (초). 벽이 없거나 레이어 미설정 시 강제 종료.")]
    public float maxChargeDuration = 3f;
    [Tooltip("2페이즈에서 돌진 속도에 곱할 배율")]
    public float phase2SpeedMultiplier = 1.3f;

    [Header("벽 감지")]
    [Tooltip("벽 감지에 사용할 레이어. 비워두면 \"Ground\"로 자동 설정.")]
    public LayerMask groundLayer;
    [Tooltip("벽 감지 레이캐스트 추가 거리 (콜라이더 반폭 + 이 값)")]
    public float wallCheckDist = 0.15f;

    [Header("데미지 / 판정")]
    public float damage = 12f;
    [Tooltip("[CONFIRM] 패링 가능 여부 (현재 true)")]
    public bool hitParryable = true;
    [Tooltip("플레이어 접촉 판정 반경")]
    public float playerHitRadius = 0.9f;

    private Collider2D _col;

    protected override void Awake()
    {
        base.Awake();
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
        _col = GetComponent<Collider2D>();
    }

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        FacePlayer();
        rb.linearVelocity = Vector2.zero;

        // 선딜 (예비 동작)
        yield return new WaitForSeconds(windupDuration);

        if (player == null) yield break;

        // 돌진 방향 — 선딜 끝난 시점 플레이어 방향으로 고정
        float dir   = player.position.x > transform.position.x ? 1f : -1f;
        float speed = chargeSpeed * (isPhase2 ? phase2SpeedMultiplier : 1f);
        rb.linearVelocity = new Vector2(dir * speed, 0f);

        float elapsed   = 0f;
        bool  hitPlayer = false;

        while (elapsed < maxChargeDuration)
        {
            elapsed += Time.deltaTime;

            // ── 벽 감지 (이동 전 위치에서 확인 → 터널링 방지) ──
            float halfW    = _col != null ? _col.bounds.extents.x : 0.5f;
            float halfH    = _col != null ? _col.bounds.extents.y : 0.8f;
            float checkDist = halfW + wallCheckDist + Mathf.Abs(rb.linearVelocity.x * Time.deltaTime);
            Vector2 chargeDir = new Vector2(dir, 0f);

            // 상·중·하 3점 레이캐스트로 얇은 벽도 감지
            bool wallAhead =
                Physics2D.Raycast(transform.position,                               chargeDir, checkDist, groundLayer).collider != null ||
                Physics2D.Raycast(transform.position + Vector3.up   * halfH * 0.7f, chargeDir, checkDist, groundLayer).collider != null ||
                Physics2D.Raycast(transform.position + Vector3.down * halfH * 0.7f, chargeDir, checkDist, groundLayer).collider != null;

            if (wallAhead) break;

            // ── 플레이어 접촉 데미지 (이번 돌진 중 1회) ──
            if (!hitPlayer)
            {
                Collider2D playerCol = Physics2D.OverlapCircle(
                    transform.position, playerHitRadius, LayerMask.GetMask("player"));
                if (playerCol != null)
                {
                    PlayerController pc = playerCol.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        hitPlayer = true;
                        pc.TakeDamage(damage, hitParryable,
                                      hitParryable ? (IParryable)boss : null,
                                      transform.position);
                    }
                }
            }

            yield return null;
        }

        // 벽 충돌 / 안전장치 종료 → 경직
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(recoverDuration);
    }

    void OnDrawGizmosSelected()
    {
        if (_col == null) return;
        float halfW = _col.bounds.extents.x;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * (halfW + wallCheckDist + 0.5f));
        Gizmos.DrawLine(transform.position, transform.position + Vector3.left  * (halfW + wallCheckDist + 0.5f));
    }
}
