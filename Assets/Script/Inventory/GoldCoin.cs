using UnityEngine;

// 적 처치 / 보물상자 / 상자에서 튀어나와 플레이어에게 빨려들어가는 골드 코인. 도착하면 골드 적립.
// 외형: Resources/GoldCoin.prefab 이 있으면 그것을 인스턴스화(원하는 골드 에셋으로 교체 가능).
//       없으면 전달된 sprite, 그것도 없으면 노란 원을 자동 생성(플레이스홀더).
public class GoldCoin : MonoBehaviour
{
    // 코인 개수 = 골드량에 비례 (SpawnGold에서 사용)
    private const int GoldPerCoin = 5;   // 코인 1개당 대략 골드(작을수록 코인 많이 떨어짐)
    private const int MaxCoins = 20;     // 코인 최대 개수(너무 많아지지 않게 상한)

    private int value = 1;
    private Transform target;            // 빨려갈 목표(보통 플레이어)
    private float popTimer;
    private Vector2 popVel;
    private float homeSpeed;
    private Vector3 startScale = Vector3.one * 0.35f;

    private const float PopTime = 0.3f;        // 처음 튀어나오는 시간
    private const float PopSpeed = 4.5f;
    private const float HomeSpeedStart = 3f;   // 빨려가기 시작 속도
    private const float HomeAccel = 30f;       // 가속(점점 빠르게)
    private const float ArriveDist = 0.4f;     // 도착 판정 거리

    private static GameObject _prefab;
    private static bool _prefabLoaded;

    // 총 골드량에 비례해 코인 여러 개로 흩뿌림(적·보물상자 공용 진입점).
    // 골드가 많을수록 코인이 많이 떨어진다(GoldPerCoin당 1개, 최대 MaxCoins).
    public static void SpawnGold(int totalGold, Vector3 pos, Transform target, Sprite fallbackSprite = null)
    {
        if (totalGold <= 0) return;
        int coins = Mathf.Clamp(Mathf.CeilToInt(totalGold / (float)GoldPerCoin), 1, MaxCoins);
        int per = totalGold / coins, rem = totalGold % coins;
        for (int i = 0; i < coins; i++)
            Spawn(pos, per + (i < rem ? 1 : 0), fallbackSprite, target);
    }

    // 코인 1개 생성(기존 시그니처 유지 — OpenCrate 등 호출부 호환).
    // Resources/GoldCoin.prefab 이 있으면 인스턴스화(에셋 사용), 없으면 sprite/노란원으로 폴백.
    public static GoldCoin Spawn(Vector3 pos, int value, Sprite sprite, Transform target)
    {
        GameObject go;
        GameObject prefab = CoinPrefab();
        if (prefab != null)
        {
            go = Instantiate(prefab, pos, Quaternion.identity);
        }
        else
        {
            go = new GameObject("GoldCoin");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : CoinSprite();
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * 0.35f;
        }

        var c = go.GetComponent<GoldCoin>();
        if (c == null) c = go.AddComponent<GoldCoin>();   // 프리팹에 컴포넌트가 없어도 자동 부착
        c.value = Mathf.Max(1, value);
        c.target = target;
        return c;
    }

    // Resources/GoldCoin.prefab 로드(1회 캐시). 없으면 null → 폴백 사용.
    private static GameObject CoinPrefab()
    {
        if (!_prefabLoaded) { _prefab = Resources.Load<GameObject>("GoldCoin"); _prefabLoaded = true; }
        return _prefab;
    }

    void Start()
    {
        startScale = transform.localScale;
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
        transform.localScale = Vector3.MoveTowards(transform.localScale, startScale * 0.5f, Time.deltaTime * 0.4f);

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

    // ── 폴백 코인 스프라이트 (없을 때 노란 원 자동 생성, 캐시) ──
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
