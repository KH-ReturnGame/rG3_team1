using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 3 — 포물선 바운드 (Bouncing Charge)
///
/// 보스가 플레이어 방향으로 포물선 궤적을 그리며 날아가고,
/// 바닥/벽에 닿을 때마다 마찰 감속을 적용한 반사로 탱탱볼처럼 튕긴다.
/// 바닥은 약하게(groundBounceRestitution, 낮게 깔리며 진행), 벽은 강하게(wallBounceRestitution,
/// 플레이어 방향 벽쪽으로 세게 튕겨나감) 반사되도록 분리되어 있음.
/// 속도가 BOUNCE_SETTLE_THRESHOLD 이하로 떨어지면 바닥에 안착.
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

    [Header("바운스")]
    [Tooltip("바운스 시, 반사되는 축이 아닌 쪽 속도에 곱해지는 마찰 계수 (0~1, 작을수록 빨리 감속)")]
    public float bounceFriction = 0.7f;
    [Tooltip("바닥에 닿았을 때 수직 속도 반전 비율. 낮게 잡아야 위로 크게 솟아오르지 않고 플레이어 쪽으로 낮게 깔려서 진행함.")]
    [Range(0f, 1f)] public float groundBounceRestitution = 0.35f;
    [Tooltip("벽(높게 쌓은 Ground 타일맵)에 닿았을 때 수평 속도 반전 비율. 높게 잡아야 탱탱볼처럼 벽에서 세게 튕겨나감.")]
    [Range(0f, 1f)] public float wallBounceRestitution = 0.85f;

    // ── TUNABLE: 바닥 안착 전환 임계 속도 ────────────────────────
    // 이 값 이하로 속도가 떨어지면 바운스를 멈추고 안착 처리함.
    // 밸런스 테스트 후 별도로 조정 예정 — 하드코딩 금지, 별도 변수/인스펙터 노출 필요.
    public float BOUNCE_SETTLE_THRESHOLD = 3f;
    // ─────────────────────────────────────────────────────────────

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

            // 중력 적용
            _vel.y -= customGravity * Time.deltaTime;
            Vector2 moveDelta = _vel * Time.deltaTime;
            rb.MovePosition(rb.position + moveDelta);

            bool bounced = false;

            // 레이캐스트는 보스 "중심(transform.position)"에서 쏘기 때문에,
            // 콜라이더 절반 크기를 더해야 실제 바닥/벽 표면에 닿기 전에 감지할 수 있음.
            // (이걸 안 더하면 콜라이더가 이미 절반 넘게 파묻힌 뒤에야 감지되거나, 그 전에
            //  물리 엔진이 먼저 위치를 막아버려서 바운스가 거의 안 일어나는 것처럼 보임)
            Bounds colBounds = _col != null ? _col.bounds : new Bounds(transform.position, Vector3.zero);
            float halfHeight = colBounds.extents.y;
            float halfWidth  = colBounds.extents.x;

            // 바닥 감지 (한 프레임 이동 거리만큼 여유를 둬서 빠른 속도에서도 터널링 방지)
            float downDist = halfHeight + groundCheckDist + Mathf.Max(0f, -moveDelta.y);
            RaycastHit2D groundHit = Physics2D.Raycast(
                transform.position, Vector2.down, downDist, groundLayer);

            if (groundHit.collider != null && _vel.y < 0f)
            {
                // 바닥 바운스: groundBounceRestitution을 낮게 둬서 위로 크게 솟지 않고
                // 낮게 깔리듯 진행하게 함 (수평 속도는 일반 마찰만 적용)
                _vel.y = -_vel.y * groundBounceRestitution;
                _vel.x *= bounceFriction;
                bounced = true;
            }

            // 벽 감지 (좌우) — 탱탱볼처럼 벽(높게 쌓은 Ground 타일맵)에 세게 튕겨나가도록 처리
            if (Mathf.Abs(_vel.x) > 0.01f)
            {
                Vector2 xDir = new Vector2(Mathf.Sign(_vel.x), 0f);
                float sideDist = halfWidth + wallCheckDist + Mathf.Abs(moveDelta.x);
                RaycastHit2D wallHit = Physics2D.Raycast(
                    transform.position, xDir, sideDist, wallLayer);

                if (wallHit.collider != null)
                {
                    // 벽 바운스: wallBounceRestitution을 높게 둬서 플레이어 방향(벽 쪼)으로 강하게 튕겨나감
                    _vel.x = -_vel.x * wallBounceRestitution;
                    _vel.y *= bounceFriction;
                    bounced = true;
                }
            }

            if (bounced)
            {
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
    // 바닥/벽 구분 없이 충돌 표면 노멀을 기준으로 반사시켜 탱탱볼처럼 튕긴다.
    void OnCollisionEnter2D(Collision2D col)
    {
        if (!_isBouncing) return;

        int layerBit = 1 << col.gameObject.layer;
        bool isGround = (groundLayer.value & layerBit) != 0;
        bool isWall   = (wallLayer.value & layerBit) != 0;
        if (!isGround && !isWall) return;

        Vector2 n = col.GetContact(0).normal;
        Vector2 v = rb.linearVelocity;

        // 법선 성분(반사되는 축)엔 표면에 맞는 restitution을, 접선 성분엔 일반 마찰을 적용.
        // 법선이 더 수평이면 벽(wallBounceRestitution, 세게), 더 수직이면 바닥(groundBounceRestitution, 약하게).
        bool isWallNormal = Mathf.Abs(n.x) > Mathf.Abs(n.y);
        float restitution = isWallNormal ? wallBounceRestitution : groundBounceRestitution;

        Vector2 vAlongNormal = Vector2.Dot(v, n) * n;
        Vector2 vTangent = v - vAlongNormal;
        Vector2 reflected = -vAlongNormal * restitution + vTangent * bounceFriction;

        if (reflected.magnitude < BOUNCE_SETTLE_THRESHOLD)
        {
            rb.linearVelocity = Vector2.zero;
            _isBouncing = false;
            return;
        }

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
