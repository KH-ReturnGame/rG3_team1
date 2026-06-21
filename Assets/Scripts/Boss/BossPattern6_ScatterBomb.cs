using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 패턴 6 — 불규칙 폭탄 투척 (Scatter Bomb Toss)
///
/// 플레이어 방향으로 폭탄 여러 개를 랜덤 궤적으로 뿌린다.
/// 각 투사체는 각도/속도에 편차를 가지며, 바닥 착탄 후 폭발.
///
/// [패링 불가] isParryable = false
///
/// [CONFIRM] 착탄~폭발 딜레이: BossScatterBomb.explosionDelay 로 조정.
///           즉발(0)로 변경하려면 해당 값을 0으로 설정.
/// </summary>
public class BossPattern6_ScatterBomb : BossPatternBase
{
    [Header("투척 설정")]
    [Tooltip("투척할 폭탄 개수")]
    public int bombCount = 5;
    [Tooltip("투척 간격 (초). 0이면 동시 발사.")]
    public float throwInterval = 0.12f;
    [Tooltip("선딜레이 (투척 예비 동작)")]
    public float windupDuration = 0.6f;
    [Tooltip("패턴 종료 후딜레이 (초)")]
    public float recoverDuration = 0.5f;

    [Header("궤적 랜덤 범위")]
    [Tooltip("기본 발사 각도 (도, 0 = 오른쪽 수평)")]
    public float baseAngle = 60f;
    [Tooltip("기본 각도에서 랜덤으로 벗어나는 최대 편차 (도)")]
    public float angleVariance = 25f;
    [Tooltip("기본 발사 속도")]
    public float baseSpeed = 10f;
    [Tooltip("속도 랜덤 편차")]
    public float speedVariance = 3f;

    [Header("폭탄 프리팹")]
    [Tooltip("BossScatterBomb 컴포넌트가 달린 프리팹. 없으면 코드로 기본 생성.")]
    public GameObject bombPrefab;

    [Header("폭탄 설정 (프리팹 없을 때 적용)")]
    public float explosionDelay  = 0.5f;
    public float explosionRadius = 2f;
    public float damage          = 15f;

    public override IEnumerator Execute(bool isPhase2)
    {
        if (!TryRefreshPlayer()) yield break;

        FacePlayer();
        yield return new WaitForSeconds(windupDuration);

        int count = bombCount;
        float xDir = player != null && player.position.x > transform.position.x ? 1f : -1f;

        var spawnedBombs = new List<GameObject>(count);

        for (int i = 0; i < count; i++)
        {
            GameObject bomb = ThrowBomb(xDir);
            if (bomb != null) spawnedBombs.Add(bomb);

            if (throwInterval > 0f)
                yield return new WaitForSeconds(throwInterval);
        }

        // 던진 폭탄이 전부 터지거나(파괴되거나) 사라질 때까지 대기.
        // 이걸 안 기다리면 폭탄이 아직 날아다니거나 안 터진 채로 다음 패턴이 시작되어
        // 패턴들이 겹쳐 보이는 문제가 생김. (폭탄 쪽 안전 Destroy가 8초라 그보다 살짝 길게 캡)
        float waited = 0f;
        const float safetyTimeout = 9f;
        while (waited < safetyTimeout && spawnedBombs.Exists(b => b != null))
        {
            waited += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(recoverDuration);
    }

    GameObject ThrowBomb(float xDir)
    {
        // 랜덤 각도/속도 편차 적용
        float angle = baseAngle + Random.Range(-angleVariance, angleVariance);
        float speed = baseSpeed + Random.Range(-speedVariance, speedVariance);

        // 각도 → 방향 벡터 (xDir로 좌/우 반전)
        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(xDir * Mathf.Cos(rad), Mathf.Sin(rad));

        GameObject bomb;
        if (bombPrefab != null)
        {
            bomb = Instantiate(bombPrefab, transform.position + Vector3.up * 0.5f,
                               Quaternion.identity);
        }
        else
        {
            bomb = CreateDefaultBomb();
        }

        // BossScatterBomb 설정
        BossScatterBomb bombComp = bomb.GetComponent<BossScatterBomb>();
        if (bombComp != null)
        {
            bombComp.explosionDelay  = explosionDelay;
            bombComp.explosionRadius = explosionRadius;
            bombComp.damage          = damage;
            bombComp.owner           = null; // 패링 불가
        }

        // 물리 발사
        Rigidbody2D bombRb = bomb.GetComponent<Rigidbody2D>();
        if (bombRb != null)
        {
            bombRb.linearVelocity = dir * speed;
        }

        return bomb;
    }

    GameObject CreateDefaultBomb()
    {
        var go = new GameObject("ScatterBomb");
        go.transform.position = transform.position + Vector3.up * 0.5f;
        go.layer = LayerMask.NameToLayer("Default");

        // 시각 (플레이스홀더)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color  = new Color(1f, 0.6f, 0f);

        // 물리
        var rb  = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.25f;
        col.isTrigger = false;

        // 폭탄 컴포넌트
        go.AddComponent<BossScatterBomb>();

        // 안전 제거 (바닥에 안 닿았을 경우 대비)
        Object.Destroy(go, 8f);

        return go;
    }

    private static Sprite _circleSprite;
    static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        // 간단한 원형 텍스처 생성
        int res = 32;
        Texture2D tex = new Texture2D(res, res);
        float r = res * 0.5f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
            tex.SetPixel(x, y, d < r ? Color.white : Color.clear);
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _circleSprite;
    }
}
