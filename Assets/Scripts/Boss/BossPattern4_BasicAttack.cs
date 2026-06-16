using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 4 — 기본 평타 (Basic Attack)
///
/// 달려와서 주먹으로 한 대 친다.
/// 선딜(예비동작) → 판정 → 후딜 의 표준 근접 공격.
///
/// [규칙] 조우 시 항상 첫 번째로 발동. Boss.cs 에서 강제 지정함.
///        이후에는 일반 패턴 풀에 포함되어 가중치에 따라 선택됨.
///
/// [패링 가능] isParryable = true (패링 학습용 기본 패턴)
///
/// [페이즈 차이] 없음.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPattern4_BasicAttack : BossPatternBase
{
    [Header("이동")]
    [Tooltip("플레이어에게 달려가는 속도")]
    public float chaseSpeed = 6f;
    [Tooltip("공격 시작 거리 (이 이하로 가까워지면 공격)")]
    public float attackRange = 1.5f;
    [Tooltip("플레이어를 따라가다가 포기하는 타임아웃 (초)")]
    public float chaseTimeout = 3f;

    [Header("공격 타이밍")]
    [Tooltip("예비동작 (선딜) 시간 — 플레이어가 패링을 노리는 구간")]
    public float windupDuration = 0.45f;
    [Tooltip("실제 타격 판정이 나가는 시간")]
    public float activeDuration = 0.12f;
    [Tooltip("공격 후 경직 시간")]
    public float recoverDuration = 0.5f;

    [Header("데미지 / 판정")]
    public float damage = 8f;
    [Tooltip("근접 판정 가로 거리")]
    public float hitRangeX = 1.6f;
    [Tooltip("근접 판정 세로 범위 (±)")]
    public float hitRangeY = 1.1f;

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        // 1. 플레이어에게 접근
        yield return StartCoroutine(ChasePlayer());

        // 2. 예비동작 (선딜)
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        yield return new WaitForSeconds(windupDuration);

        // 3. 타격 판정
        bool hit = false;
        float elapsed = 0f;
        while (elapsed < activeDuration)
        {
            elapsed += Time.deltaTime;

            if (!hit && player != null)
            {
                float dx = Mathf.Abs(player.position.x - transform.position.x);
                float dy = Mathf.Abs(player.position.y - transform.position.y);

                if (dx <= hitRangeX + 0.3f && dy <= hitRangeY)
                {
                    PlayerController pc = player.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        hit = true;
                        // isParryable = true → 패링 가능
                        pc.TakeDamage(damage, true, boss, transform.position);
                    }
                }
            }

            yield return null;
        }

        // 4. 후딜
        yield return new WaitForSeconds(recoverDuration);
    }

    IEnumerator ChasePlayer()
    {
        float elapsed = 0f;
        while (elapsed < chaseTimeout)
        {
            elapsed += Time.deltaTime;

            if (player == null) yield break;

            float dist = Mathf.Abs(player.position.x - transform.position.x);
            if (dist <= attackRange)
            {
                rb.linearVelocity = Vector2.zero;
                yield break;
            }

            float dir = player.position.x > transform.position.x ? 1f : -1f;
            rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);
            FacePlayer();

            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(hitRangeX * 2f, hitRangeY * 2f, 0f));
    }
}
