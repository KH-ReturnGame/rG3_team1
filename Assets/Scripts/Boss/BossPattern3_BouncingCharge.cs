using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 3 — 포물선 바운드 (Bouncing Charge)
///
/// 보스가 플레이어 방향으로 포물선 궤적을 그리며 날아가고,
/// 바닥/벽에 닿을 때마다 마찰 없이(탄성) 반사되어 탱탱볼처럼 튕긴다.
/// activeBounceDuration 시간 동안은 감속 없이 계속 튕기다가, 그 시간이 지나면
/// 그 즉시 속도를 0으로 만들고 다음 착지에서 바로 멈춘다 ("툭" 떨어지는 느낌).
/// 바닥은 약하게(groundBounceRestitution, 낮게 깔리며 진행), 벽은 강하게(wallBounceRestitution,
/// 플레이어 방향 벽쪽으로 세게 튕겨나감) 반사되도록 분리되어 있음.
///
/// [CONFIRM] 패링 가능 여부: 현재 isParryable = true (확정 후 hitParryable 수정).
/// [CONFIRM] 안착 후 동작: 현재 settleRecoverDuration(0.5s) 후딜레이 후 패턴 종료.
///
/// [사용법]
/// - 보스 Rigidbody2D 의 gravityScale 을 0으로 설정하고 이 스크립트가 가상 중력을 처리.
/// - 또는 씬의 Physics2D 중력 그대로 사용하고 gravityScale을 1로 유지 (아래 useCustomGravity 참고).
/// - groundLayer 에 바닥 레이어, wallLayer 에 벽 레이어 설정 필수 (wallLayer를 비워두면 groundLayer와 동일하게 처리).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossPattern3_BouncingCharge : BossPatternBase
{
    [Header("발사 설정")]
    [Tooltip("초기 발사 수평 속도의 최소 보장값. 플레이어가 멀어서 더 빠른 속도가 필요하면 자동으로 그만큼 더 빨라지고, 이 값을 올리면 항상 그만큼 더 빠르게 날아감.")]
    public float launchSpeedX = 12f;
    [Tooltip("초기 발사 수직 속도 (양수 = 위쪽)")]
    public float launchSpeedY = 14f;
    [Tooltip("선딜레이 (보스 점프 전 예비 동작)")]
    public float windupDuration = 0.5f;

    [Header("바운스 (마찰 없음 — 탄성 반사)")]
    [Tooltip("바닥에 닿았을 때 수직 속도 반전 비율. 낮게 잡아야 위로 크게 솟아오르지 않고 플레이어 쪽으로 낮게 깔려서 진행함.")]
    [Range(0f, 1f)] public float groundBounceRestitution = 0.35f;
    [Tooltip("벽(높게 쌓은 Ground 타일맵)에 닿았을 때 수평 속도 반전 비율. 높게 잡아야 탱탱볼처럼 벽에서 세게 튕겨나감.")]
    [Range(0f, 1f)] public float wallBounceRestitution = 0.85f;
    [Tooltip("이 시간 동안은 감속 없이 계속 튕김. 시간이 지나면 그 즉시 속도를 0으로 만들고 다음 착지에서 바로 멈춤.")]
    public float activeBounceDuration = 1.5f;

    [Tooltip("안착 후 다음 패턴까지 후딜레이 (초). [CONFIRM] 기획 의도 확인 필요.")]
    public float settleRecoverDuration = 0.5f;
    [Tooltip("바운스 루프 안전장치 — 이 시간이 지나면 강제로 안착 처리하여 보스가 영구히 멈추는 것을 방지 (초)")]
    public float maxBounceDuration = 6f;
    [Tooltip("가상 중력 사용 여부. false면 Rigidbody2D 자체 중력 사용.")]
    public bool useCustomGravity = true;
    [Tooltip("useCustomGravity = true 일 때 적용할 중력 가속도 (양수)")]
    public float customGravity = 25f;
    [Tooltip("감지할 바닥 레이어")]
    public LayerMask groundLayer;
    [Tooltip("바닥 감지 레이캐스트 거리")]
    public float groundCheckDist = 0.15f;
    [Tooltip("감지할 벽 레이어 (좌우). 비워두면 groundLayer와 동일하게 처리.")]
    public LayerMask wallLayer;
    [Tooltip("벽 감지 레이캐스트 거리")]
    public float wallCheckDist = 0.2f;

    [Header("데미지")]
    public float damage = 14f;
    [Tooltip("[CONFIRM] 패링 가능 여부 (현재 true)")]
    public bool hitParryable = true;

    // ── 런타임 ───────────────────────────────────────────────────
    private bool _isBouncing;
    private Vector2 _vel;
    private bool _hasHitPlayer; // 이번 바운스 중 타격 여부
    private Collider2D _col;
    private bool _forceSettle; // activeBounceDuration이 지나서 다음 착지에 바로 멈춰야 하는지

    protected override void Awake()
    {
        base.Awake();
        // 인스펙터에 레이어가 비어있으면(미설정 = 0) 자동으로 "Ground"로 대체.
        // (벽이 따로 없고, 높게 쌓은 Ground 타일맵을 벽으로 쓰는 구조이므로 동일 레이어로 충분)
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
        if (wallLayer.value == 0) wallLayer = groundLayer;

        _col = GetComponent<Collider2D>();
    }

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        FacePlayer();
        yield return new WaitForSeconds(windupDuration);

        // 발사 방향 결정 (플레이어 방향 X, 위로 Y)
        float xDir = player.position.x > transform.position.x ? 1f : -1f;

        // 플레이어까지 도달하는 데 필요한 최소 수평 속도를 체공 시간 기준으로 역산하고,
        // launchSpeedX를 "최소 보장 속도"로 사용 — launchSpeedX를 올리면 항상 더 빨리 날아가고,
        // 동시에 거리가 멀어도 플레이어 앞에서 멈추지 않고 도달은 보장됨.
        float dist = Mathf.Abs(player.position.x - transform.position.x);
        float g = useCustomGravity ? customGravity : Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        float airTime = g > 0.01f ? (2f * launchSpeedY / g) : 0f;
        float requiredSpeedX = airTime > 0.01f ? dist / airTime : launchSpeedX;
        float vx = Mathf.Max(launchSpeedX, requiredSpeedX);

        _vel = new Vector2(xDir * vx, launchSpeedY);

        if (useCustomGravity)
        {
            // 물리를 직접 제어 → Rigidbody2D 중력 끔
            float origGravity = rb.gravityScale;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;

            _isBouncing = true;
            _hasHitPlayer = false;
            _forceSettle = false;

            yield return StartCoroutine(BounceLoop());

            rb.gravityScale = origGravity;
        }
        else
        {
            // Rigidbody2D 자체 중력 사용 — 바운스는 충돌 이벤트로 처리
            rb.linearVelocity = _vel;
            _isBouncing = true;
            _hasHitPlayer = false;
            _forceSettle = false;

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
        float elapsed = 0f;

        while (_isBouncing)
        {
            elapsed += Time.deltaTime;

            // 안전장치: 레이어 미설정/레이캐스트 미스 등 어떤 이유로든 바운스가 끝나지 않으면
            // 보스가 영구히 멈춰 다음 패턴으로 못 넘어가는 일이 없도록 강제 종료.
            if (elapsed >= maxBounceDuration)
            {
                _isBouncing = false;
                yield break;
            }

            // activeBounceDuration이 지나면 그 즉시 속도를 0으로 만들고,
            // 다음 착지에서 바로 멈추도록 표시 ("툭" 떨어지는 느낌 — 점진적 감속 없음)
            if (!_forceSettle && elapsed >= activeBounceDuration)
            {
                _forceSettle = true;
                _vel = Vector2.zero;
            }

            // 중력 적용
            _vel.y -= customGravity * Time.deltaTime;
            Vector2 moveDelta = _vel * Time.deltaTime;
            rb.MovePosition(rb.position + moveDelta);

            bool bounced = false;

            // 레이캐스트는 보스 "중심(transform.position)"에서 쏘기 때문에,
            // 콜라이더 절반 크기를 더해야 실제 바닥/벽 표면에 닿기 전에 감지할 수 있음.
            Bounds colBounds = _col != null ? _col.bounds : new Bounds(transform.position, Vector3.zero);
            float halfHeight = colBounds.extents.y;
            float halfWidth  = colBounds.extents.x;

            // 바닥 감지 (한 프레임 이동 거리만큼 여유를 둬서 빠른 속도에서도 터널링 방지)
            float downDist = halfHeight + groundCheckDist + Mathf.Max(0f, -moveDelta.y);
            RaycastHit2D groundHit = Physics2D.Raycast(
                transform.position, Vector2.down, downDist, groundLayer);

            if (groundHit.collider != null && _vel.y < 0f)
            {
                if (_forceSettle)
                {
                    // 액티브 바운스 시간이 끝난 뒤의 첫 착지 — 바로 멈춤
                    _vel = Vector2.zero;
                    _isBouncing = false;
                    yield break;
                }

                // 바닥 바운스: 마찰 없이 groundBounceRestitution만 적용 (수평 속도는 그대로 유지)
                _vel.y = -_vel.y * groundBounceRestitution;
                bounced = true;
            }

            // 벽 감지 (좌우) — 탱탱볼처럼 벽(높게 쌓은 Ground 타일맵)에 세게 튕겨나가도록 처리
            if (!_forceSettle && Mathf.Abs(_vel.x) > 0.01f)
            {
                Vector2 xDir = new Vector2(Mathf.Sign(_vel.x), 0f);
                float sideDist = halfWidth + wallCheckDist + Mathf.Abs(moveDelta.x);
                RaycastHit2D wallHit = Physics2D.Raycast(
                    transform.position, xDir, sideDist, wallLayer);

                if (wallHit.collider != null)
                {
                    // 벽 바운스: 마찰 없이 wallBounceRestitution만 적용 (수직 속도는 그대로 유지)
                    _vel.x = -_vel.x * wallBounceRestitution;
                    bounced = true;
                }
            }

            if (bounced)
            {
                _hasHitPlayer = false; // 바운스마다 타격 기회 리셋
            }

            // 보스 판정 (이번 바운스 중 아직 안 때렸으면)
            if (!_hasHitPlayer)
            {
                // 주의: 프로젝트 레이어 이름이 "player"(소문자)임. "Player"로 쓰면 항상 매칭 0개로
                // 조용히 실패해서 데미지가 절대 안 들어갔던 원인이었음.
                Collider2D playerHit = Physics2D.OverlapCircle(
                    transform.position, 0.9f, LayerMask.GetMask("player"));
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

    // ── 물리 중력 사용 시 안착 대기 루프 (useCustomGravity = false 시) ──
    // 참고: 이 경로는 현재 기본값(useCustomGravity = true)에서는 쓰이지 않음.
    IEnumerator WaitForSettle()
    {
        float elapsed = 0f;
        while (elapsed < maxBounceDuration)
        {
            elapsed += Time.deltaTime;

            if (!_forceSettle && elapsed >= activeBounceDuration)
            {
                _forceSettle = true;
                rb.linearVelocity = Vector2.zero;
            }

            if (_forceSettle && rb.linearVelocity.magnitude < 0.5f)
            {
                yield break;
            }

            yield return null;
        }
    }

    // ── 물리 충돌 기반 바운스 (useCustomGravity = false 시) ──
    // 바닥/벽 구분 없이 충돌 표면 노멀을 기준으로 반사시켜 탱탱볼처럼 튕긴다.
    void OnCollisionEnter2D(Collision2D col)
    {
        if (!_isBouncing) return;

        int layerBit = 1 << col.gameObject.layer;
        bool isGround = (groundLayer.value & layerBit) != 0;
        bool isWall   = (wallLayer.value & layerBit) != 0;
        if (!isGround && !isWall) return;

        if (_forceSettle)
        {
            rb.linearVelocity = Vector2.zero;
            _isBouncing = false;
            return;
        }

        Vector2 n = col.GetContact(0).normal;
        Vector2 v = rb.linearVelocity;

        // 법선 성분(반사되는 축)엔 표면에 맞는 restitution을 적용, 접선 성분은 마찰 없이 그대로 유지.
        // 법선이 더 수평이면 벽(wallBounceRestitution, 세게), 더 수직이면 바닥(groundBounceRestitution, 약하게).
        bool isWallNormal = Mathf.Abs(n.x) > Mathf.Abs(n.y);
        float restitution = isWallNormal ? wallBounceRestitution : groundBounceRestitution;

        Vector2 vAlongNormal = Vector2.Dot(v, n) * n;
        Vector2 vTangent = v - vAlongNormal;
        Vector2 reflected = -vAlongNormal * restitution + vTangent;

        rb.linearVelocity = reflected;
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
