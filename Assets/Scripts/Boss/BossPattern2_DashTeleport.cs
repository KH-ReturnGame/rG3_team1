using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 2 — 돌진 / 순간이동 히트 (Dash / Teleport Strike)
///
/// [일반 돌진]  선딜 모션 → 이동 → 충돌 판정 (패링 가능, [CONFIRM])
/// [즉시 타격]  동일한 선딜 모션 → 플레이어 옆으로 텔레포트 → 즉시 데미지
///             (이동 보간 없이 위치 스냅. 산데비스탄 류 파츠 없으면 반응 불가.)
///
/// [CONFIRM] 일반 돌진 패링 가능 여부: 현재 true로 구현. 확정 후 normalDashParryable 수정.
/// [CONFIRM] 2페이즈 식별 신호(진입 사운드/이펙트) 필요 여부: audioClipPatternEntry 연결로 대응.
///
/// [페이즈 차이]
/// - 1페이즈: 선딜레이 길이/모션으로 일반 돌진 vs 즉시 타격 어느 정도 구분 가능.
/// - 2페이즈: 두 모션 차이 최소화 (teleportWindup ≈ normalWindup). 즉시 타격 빈도 증가.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPattern2_DashTeleport : BossPatternBase
{
    [Header("일반 돌진")]
    [Tooltip("일반 돌진 선딜레이 (초)")]
    public float normalWindup = 0.8f;
    [Tooltip("돌진 이동 속도")]
    public float dashSpeed = 18f;
    [Tooltip("돌진 최대 지속 시간 (초) — 안전장치. 플레이어 바로 옆에 도달하면 이 시간 전에 먼저 멈춤.")]
    public float dashHitDuration = 0.3f;
    [Tooltip("돌진을 멈추는 기준 거리 — 플레이어와 이 가로 거리 이하가 되면 바로 옆에서 정지.")]
    public float dashStopDistance = 1f;
    [Tooltip("[CONFIRM] 일반 돌진 패링 가능 여부 (현재 true)")]
    public bool normalDashParryable = true;

    [Header("즉시 타격 (텔레포트)")]
    [Tooltip("텔레포트 선딜레이 (초). 1페이즈에서는 normalWindup보다 짧게 → 구분 단서.")]
    public float teleportWindup = 0.4f;
    [Tooltip("텔레포트 후 플레이어 기준 X 오프셋 (부호는 방향에 따라 자동 결정)")]
    public float teleportXOffset = 0.8f;
    [Tooltip("텔레포트 즉시 타격 판정 반경")]
    public float teleportHitRadius = 1.2f;
    [Tooltip("텔레포트 타격 후 후딜레이 (초)")]
    public float teleportRecoverDuration = 0.5f;

    [Header("2페이즈")]
    [Tooltip("2페이즈에서 즉시 타격이 선택될 확률 (0~1)")]
    [Range(0f, 1f)] public float phase2TeleportChance = 0.65f;
    [Tooltip("1페이즈에서 즉시 타격이 선택될 확률 (0~1)")]
    [Range(0f, 1f)] public float phase1TeleportChance = 0.3f;
    [Tooltip("2페이즈에서 normalWindup ↔ teleportWindup 차이를 좁히는 배율 (1이면 차이 없음)")]
    [Range(0f, 1f)] public float phase2WindupDiffFactor = 0.15f;

    [Header("사운드")]
    [Tooltip("패턴 진입 사운드 (특히 2페이즈에서 식별 신호로 사용 권장)")]
    public AudioClip audioClipPatternEntry;
    [Tooltip("텔레포트 순간 사운드")]
    public AudioClip audioClipTeleport;

    [Header("데미지")]
    public float dashDamage     = 10f;
    public float teleportDamage = 15f;

    private AudioSource _audio;

    protected override void Awake()
    {
        base.Awake();
        _audio = GetComponent<AudioSource>();
    }

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        // 진입 사운드 재생 (2페이즈 식별 신호로도 사용)
        PlayClip(audioClipPatternEntry);

        // 이번 회에 텔레포트 타격 사용 여부 결정
        float chance = isPhase2 ? phase2TeleportChance : phase1TeleportChance;
        bool useTeleport = Random.value < chance;

        if (useTeleport)
            yield return StartCoroutine(TeleportStrike(isPhase2));
        else
            yield return StartCoroutine(NormalDash(isPhase2));
    }

    // ── 일반 돌진 ────────────────────────────────────────────
    IEnumerator NormalDash(bool isPhase2)
    {
        FacePlayer();

        float windup = isPhase2
            ? Mathf.Lerp(teleportWindup, normalWindup, phase2WindupDiffFactor)
            : normalWindup;

        yield return new WaitForSeconds(windup);

        if (player == null) yield break;

        // 돌진 방향
        Vector2 dir = DirToPlayer();
        float elapsed = 0f;
        bool hitRegistered = false;

        // 플레이어 바로 옆(dashStopDistance)까지 도달하면 멈춤.
        // dashHitDuration은 안전장치(최대 지속 시간)로만 사용 — 플레이어가 멀어서 못 따라잡는 경우 대비.
        while (elapsed < dashHitDuration)
        {
            elapsed += Time.deltaTime;

            float distX = player != null ? Mathf.Abs(player.position.x - transform.position.x) : Mathf.Infinity;
            if (distX <= dashStopDistance) break;

            rb.linearVelocity = dir * dashSpeed;

            // 판정 (OverlapCircle)
            if (!hitRegistered)
            {
                Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.8f,
                                    LayerMask.GetMask("Player"));
                if (hit != null)
                {
                    PlayerController pc = hit.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        hitRegistered = true;
                        pc.TakeDamage(dashDamage, normalDashParryable,
                                      normalDashParryable ? (IParryable)boss : null,
                                      transform.position);
                    }
                }
            }

            yield return null;
        }

        // 돌진 멈춤
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.4f); // 후딜레이
    }

    // ── 즉시 타격 (텔레포트) ─────────────────────────────────
    IEnumerator TeleportStrike(bool isPhase2)
    {
        FacePlayer();

        // 2페이즈: 선딜레이가 일반 돌진과 거의 동일하게 수렴 → 구분 곤란
        float windup = isPhase2
            ? Mathf.Lerp(normalWindup, teleportWindup, 1f - phase2WindupDiffFactor)
            : teleportWindup;

        yield return new WaitForSeconds(windup);

        if (player == null) yield break;

        // 텔레포트: 이동 보간 없이 위치 스냅
        float side = player.position.x > transform.position.x ? -1f : 1f;
        Vector3 snapPos = player.position + new Vector3(side * teleportXOffset, 0f, 0f);
        transform.position = snapPos;

        PlayClip(audioClipTeleport);

        // 즉시 데미지 판정 (isParryable = false — 반응 불가 설계)
        Collider2D hit = Physics2D.OverlapCircle(transform.position, teleportHitRadius,
                            LayerMask.GetMask("Player"));
        if (hit != null)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            pc?.TakeDamage(teleportDamage, false, null, transform.position);
        }

        yield return new WaitForSeconds(teleportRecoverDuration);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_audio != null) _audio.PlayOneShot(clip);
        else AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, teleportHitRadius);
    }
}
