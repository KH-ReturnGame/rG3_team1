using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 3 — 포물선 바운드 (Bouncing Charge)
///
/// 보스가 플레이어 방향으로 포물선 궤적을 그리며 날아가고,
/// 바닥에 닿을 때마다 마찰 감속을 적용해 탱탱볼처럼 튕긴다.
/// 속도가 BOUNCE_SETTLE_THRESHOLD 이하로 떨어지면 바닥에 안착.
///
/// [CONFIRM] 패링 가능 여부: 현재 isParryable = true (확정 후 hitParryable 수정).
/// [CONFIRM] 안착 후 동작: 현재 settleRecoverDuration(0.5s) 후딜레이 후 패턴 종료.
///
/// [사용법]
/// - 보스 Rigidbody2D 의 gravityScale 을 0으로 설정하고 이 스크립트가 가상 중력을 처리.
/// - 또는 씬의 Physics2D 중력 그대로 사용하고 gravityScale을 1로 유지 (아래 useCustomGravity 참고).
/// - groundLayer 에 바닥 레이어 설정 필수.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossPattern3_BouncingCharge : BossPatternBase
{
    [Header("발사 설정")]
    [Tooltip("초기 발사 수평 속도")]
    public float launchSpeedX = 12f;
    [Tooltip("초기 발사 수직 속도 (양수 = 위쪽)")]
    public float launchSpeedY = 14f;
    [Tooltip("선딜레이 (보스 점프 전 예비 동작)")]
    public float windupDuration = 0.5f;

    [Header("바운스")]
    [Tooltip("바운스마다 속도에 곱해지는 마찰 계수 (0~1, 작을수록 빨리 감속)")]
    public float bounceFriction = 0.7f;

    // ── TUNABLE: 바닥 안착 전환 임계 속도 ────────────────────────
    // 이 값 이하로 속도가 떨어지면 바운스를 멈추고 안착 처리함.
    // 밸런스 테스트 후 별도로 조정 예정 — 하드코딩 금지, 별도 변수/인스펙터 노출 필요.
    public float BOUNCE_SETTLE_THRESHOLD = 3f;
    // ─────────────────────────────────────────────────────────────

    [Tooltip("안착 후 다음 패턴까지 후딜레이 (초). [CONFIRM] 기획 의도 확인 필요.")]
    public float settleRecoverDuration = 0.5f;
    [Tooltip("가상 중력 사용 여부. false면 Rigidbody2D 자체 중력 사용.")]
    public bool useCustomGravity = true;
    [Tooltip("useCustomGravity = true 일 때 적용할 중력 가속도 (양수)")]
    public float customGravity = 25f;
    [Tooltip("감지할 바닥 레이어")]
    public LayerMask groundLayer;
    [Tooltip("바닥 감지 레이캐스트 거리")]
    public float groundCheckDist = 0.15f;

    [Header("데미지")]
    public float damage = 14f;
    [Tooltip("[CONFIRM] 패링 가능 여부 (현재 true)")]
    public bool hitParryable = true;

    // ── 런타임 ───────────────────────────────────────────────────
    private bool _isBouncing;
    private Vector2 _vel;
    private bool _hasHitPlayer; // 이번 바운스 중 타격 여부

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        FacePlayer();
        yield return new WaitForSeconds(windupDuration);

        // 발사 방향 결정 (플레이어 방향 X, 위로 Y)
        float xDir = player.position.x > transform.position.x ? 1f : -1f;
        _vel = new Vector2(xDir * launchSpeedX, launchSpeedY);

        if (useCustomGravity)
        {
            // 물리를 직접 제어 → Rigidbody2D 중력 끔
            float origGravity = rb.gravityScale;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            _isBouncing = true;
            _hasHitPlayer = false;

            yield return StartCoroutine(BounceLoop());

            rb.gravityScale = origGravity;
        }
        else
        {
            // Rigidbody2D 자체 중력 사용 — 바운스는 충돌 이벤트로 처리
            rb.linearVelocity = _vel;
            _isBouncing = true;
            _hasHitPlayer = false;

            // 안착 감지 루프
            yield return StartCoroutine(WaitForSettle());
        }

        _isBouncing = false;
        rb.linearVelocity = Vector2.zero;

        // [CONFIRM] 안착 후 후딜레이
        yield return new WaitForSeconds(settleRecoverDuration);
    }

    // ── 가상 중력 바운스 루프 ──────────────────────────────────
    IEnumerator BounceLoop()
    {
        while (_isBouncing)
        {
            // 중력 적용
            _vel.y -= customGravity * Time.deltaTime;
            rb.MovePosition(rb.position + _vel * Time.deltaTime);

            // 바닥 감지
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position, Vector2.down, groundCheckDist, groundLayer);

            if (hit.collider != null && _vel.y < 0f)
            {
                // 바운스: 수직 속도 반전 + 마찰 감속
                _vel.y = -_vel.y * bounceFriction;
                _vel.x *= bounceFriction;

                // 안착 조건 체크
                if (_vel.magnitude < BOUNCE_SETTLE_THRESHOLD)
                {
                    _isBouncing = false;
                    yield break;
                }

                _hasHitPlayer = false; // 바운스마다 타격 기회 리셋
            }

            // 보스 판정 (이번 바운스 중 아직 안 때렸으면)
            if (!_hasHitPlayer)
            {
                Collider2D playerHit = Physics2D.OverlapCircle(
                    transform.position, 0.9f, LayerMask.GetMask("Player"));
                if (playerHit != null)
                {
                    PlayerController pc = playerHit.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        _hasHitPlayer = true;
                        pc.TakeDamage(damage, hitParryable,
                                      hitParryable ? (IParryable)boss : null,
                                      transform.position);
                    }
                }
            }

            yield return null;
        }
    }

    // ── 물리 중력 사용 시 안착 대기 루프 ─────────────────────
    IEnumerator WaitForSettle()
    {
        float timeout = 8f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            if (rb.linearVelocity.magnitude < BOUNCE_SETTLE_THRESHOLD)
            {
                yield break;
            }
            yield return null;
        }
    }

    // ── 물리 충돌 기반 바운스 (useCustomGravity = false 시) ──
    void OnCollisionEnter2D(Collision2D col)
    {
        if (!_isBouncing) return;
        if ((groundLayer.value & (1 << col.gameObject.layer)) == 0) return;

        Vector2 v = rb.linearVelocity;
        v.y = Mathf.Abs(v.y) * bounceFriction;
        v.x *= bounceFriction;

        if (v.magnitude < BOUNCE_SETTLE_THRESHOLD)
        {
            rb.linearVelocity = Vector2.zero;
            _isBouncing = false;
            return;
        }

        rb.linearVelocity = v;
        _hasHitPlayer = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isBouncing || _hasHitPlayer) return;
        if (!other.CompareTag("Player")) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        _hasHitPlayer = true;
        pc.TakeDamage(damage, hitParryable,
                      hitParryable ? (IParryable)boss : null,
                      transform.position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.9f);
    }
}
