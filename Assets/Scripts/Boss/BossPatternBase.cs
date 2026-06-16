using UnityEngine;
using System.Collections;

/// <summary>
/// 보스 패턴 공통 베이스.
/// Boss.cs와 같은 GameObject에 컴포넌트로 부착하여 사용.
/// 각 패턴 스크립트는 이 클래스를 상속하고 Execute() 코루틴을 구현한다.
/// </summary>
public abstract class BossPatternBase : MonoBehaviour
{
    protected Boss         boss;
    protected Transform    player;
    protected Rigidbody2D  rb;

    protected virtual void Awake()
    {
        boss = GetComponent<Boss>();
        rb   = GetComponent<Rigidbody2D>();
    }

    protected virtual void Start()
    {
        if (boss != null) player = boss.GetPlayerTransform();
    }

    /// <summary>
    /// Boss.cs가 호출하는 패턴 실행 코루틴.
    /// </summary>
    /// <param name="isPhase2">현재 보스 페이즈가 2페이즈인지 여부.</param>
    public abstract IEnumerator Execute(bool isPhase2);

    // ── 공통 유틸 ─────────────────────────────────────────────

    /// <summary>플레이어가 현재 유효한지 확인. Start 이후 갱신이 필요하면 호출.</summary>
    protected bool TryRefreshPlayer()
    {
        if (player != null) return true;
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return false;
        player = pc.transform;
        return true;
    }

    /// <summary>보스에서 플레이어 방향 단위 벡터.</summary>
    protected Vector2 DirToPlayer()
    {
        if (player == null) return Vector2.right;
        return ((Vector2)(player.position - transform.position)).normalized;
    }

    /// <summary>플레이어까지 거리.</summary>
    protected float DistToPlayer()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    /// <summary>보스 스프라이트를 플레이어 방향으로 뒤집는다.</summary>
    protected void FacePlayer()
    {
        if (player == null) return;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = player.position.x < transform.position.x;
    }
}
