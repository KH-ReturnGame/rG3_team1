using UnityEngine;

// 적 처치 시 튀어나와 플레이어에게 빨려들어가는 골드 코인. 도착하면 골드가 적립된다.
// 스프라이트가 없으면 노란 원을 자동 생성해 사용(플레이스홀더).
public class GoldCoin : MonoBehaviour
{
    private int value = 1;
    private Transform target;       // 빨려갈 목표(보통 플레이어)
    private float popTimer;
    private Vector2 popVel;
    private float homeSpeed;

    private const float PopTime = 0.3f;        // 처음 튀어나오는 시간
    private const float PopSpeed = 4.5f;
    private const float HomeSpeedStart = 3f;   // 빨려가기 시작 속도
    private const float HomeAccel = 30f;       // 가속(점점 빠르게)
    private const float ArriveDist = 0.4f;     // 도착 판정 거리

    // 적 드랍 등에서 호출: 코인 1개 생성(target = 빨려갈 대상, 보통 플레이어)
    public static GoldCoin Spawn(Vector3 pos, int value, Sprite sprite, Transform target)
    {
        var go = new GameObject("GoldCoin");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : CoinSprite();
        sr.sortingOrder = 5;
        go.transform.localScale = Vector3.one * 0.35f;
        var c = go.AddComponent<GoldCoin>();
        c.value = Mathf.Max(1, value);
        c.target = target;
        return c;
    }

    void Start()
    {
        if (target == null)   // 폴백: 타겟 안 받았으면 직접 플레이어 탐색
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        float ang = Random.Range(55f, 125f) * Mathf.Deg2Rad;            // 위쪽 부채꼴로 튀어나옴
        float sx = Random.value < 0.5f ? -1f : 1f;
        popVel = new Vector2(Mathf.Cos(ang) * sx, Mathf.Sin(ang)) * PopSpeed * Random.Range(0.7f, 1.2f);
        popTimer = PopTime;
        homeSpeed = HomeSpeedStart;
    }

    void Update()
    {
        if (popTimer > 0f)                       // 잠깐 튀어나오는 단계
        {
            popTimer -= Time.deltaTime;
            transform.position += (Vector3)popVel * Time.deltaTime;
            popVel *= 0.9f;
            return;
        }

        Vector3 tgt = TargetPos();               // 플레이어에게 빨려감
        homeSpeed += HomeAccel * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, tgt, homeSpeed * Time.deltaTime);
        transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one * 0.18f, Time.deltaTime * 0.4f);

        if (Vector3.Distance(transform.position, tgt) <= ArriveDist)
        {
            if (GameManager.Instance != null) GameManager.Instance.AddGold(value);   // 도착 → 적립
            Destroy(gameObject);
        }
    }

    private Vector3 TargetPos()
    {
        if (target == null) return transform.position;    // 타겟 없으면 제자리 → 즉시 적립
        Vector3 p = target.position + Vector3.up * 0.3f;  // 발밑이 아니라 몸 쪽으로
        p.z = transform.position.z;
        return p;
    }

    // ── 코인 스프라이트 (없을 때 노란 원 자동 생성, 캐시) ──
    private static Sprite _sprite;
    public static Sprite CoinSprite()
    {
        if (_sprite != null) return _sprite;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 ctr = new Vector2((S - 1) * 0.5f, (S - 1) * 0.5f);
        float rad = S * 0.46f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), ctr);
                if (d <= rad)
                {
                    float t = 1f - d / rad;   // 가장자리는 어둡게
                    tex.SetPixel(x, y, Color.Lerp(new Color(0.8f, 0.55f, 0.1f), new Color(1f, 0.92f, 0.35f), t));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        tex.Apply();
        _sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _sprite;
    }
}
