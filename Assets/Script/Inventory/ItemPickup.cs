using UnityEngine;

// 월드에 떨어져 있거나 채집 가능한 아이템. 가까이서 F로 주우면 인벤토리로 들어가고 사라짐.
// 필요: 이 오브젝트에 Collider2D(트리거든 아니든 OK) — PlayerInteractor가 범위로 감지함.
// hover=true 면 중력 없이 공중에 둥둥(상자 드랍 등) + 지면 그림자. fromPos 지정 시 상자에서 튀어나오는 팝 연출.
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemData item;
    public int count = 1;

    [Header("공중 부유 (상자 드랍 등)")]
    public bool hover = false;          // true면 중력 없이 둥둥 + 그림자
    public float bobAmplitude = 0.30f;  // 위아래 폭(강화)
    public float bobSpeed = 2.6f;       // 흔들리는 속도(강화)
    private float baseY, bobPhase, baseScale = 1f;

    // 스폰 팝(상자에서 튀어나오기)
    private bool spawning;
    private float spawnDelay, spawnT;
    private Vector3 spawnFrom, spawnTarget;
    private const float SpawnDur = 0.40f;

    // 그림자
    private Transform shadow;
    private SpriteRenderer shadowSr;
    private float shadowBaseScale = 0.5f;
    private const float ShadowDrop = 0.5f;
    private static Sprite _shadowSprite;

    public string Prompt => item != null ? $"F: {item.itemName} 줍기" : "F: 줍기";

    void Start()
    {
        if (!spawning) baseY = transform.position.y;
        bobPhase = Random.value * 6.2831853f;
    }

    void Update()
    {
        if (spawning) { AnimateSpawn(); return; }
        if (!hover) return;

        float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase);
        Vector3 p = transform.position;
        p.y = baseY + bob * bobAmplitude;
        transform.position = p;
        transform.localScale = Vector3.one * (baseScale * (1f + bob * 0.05f));   // 미세 스케일 펄스
        UpdateShadow();
    }

    // 상자 안 → 제자리로 튀어나오는 애니메이션
    private void AnimateSpawn()
    {
        if (spawnDelay > 0f)
        {
            spawnDelay -= Time.deltaTime;
            transform.position = spawnFrom;
            transform.localScale = Vector3.zero;                 // 나오기 전 숨김
            if (shadowSr != null) shadowSr.enabled = false;
            return;
        }
        if (shadowSr != null) shadowSr.enabled = true;

        spawnT += Time.deltaTime / SpawnDur;
        float t = Mathf.Clamp01(spawnT);
        float posE = 1f - (1f - t) * (1f - t);                   // ease-out(자리 안착)
        Vector3 p = Vector3.Lerp(spawnFrom, spawnTarget, posE);
        p.y += Mathf.Sin(t * Mathf.PI) * 0.4f;                    // 포물선 아치(튀어오름)
        transform.position = p;
        transform.localScale = Vector3.one * Mathf.Max(0.001f, baseScale * EaseOutBack(t));   // 작게→풀(통통)
        UpdateShadow();

        if (spawnT >= 1f)
        {
            spawning = false;
            baseY = spawnTarget.y;
            transform.position = spawnTarget;
            transform.localScale = Vector3.one * baseScale;
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private void EnsureShadow()
    {
        if (shadow != null || !hover) return;
        var go = new GameObject("ItemShadow");
        go.transform.position = new Vector3(transform.position.x, baseY - ShadowDrop, 0f);
        shadowSr = go.AddComponent<SpriteRenderer>();
        shadowSr.sprite = ShadowSprite();
        shadowSr.color = new Color(0f, 0f, 0f, 0.4f);
        shadowSr.sortingOrder = 9;                               // 아이템(10) 아래, 상자(5) 위
        shadow = go.transform;
        shadow.localScale = Vector3.one * shadowBaseScale;
    }

    // 아이템 높이에 따라 그림자 크기·농도 반응(높을수록 작고 옅게)
    private void UpdateShadow()
    {
        if (shadow == null) return;
        float refY = spawning ? spawnTarget.y : baseY;
        shadow.position = new Vector3(transform.position.x, refY - ShadowDrop, 0f);
        float k = Mathf.InverseLerp(-1f, 1.7f, (transform.position.y - refY) / Mathf.Max(0.01f, bobAmplitude));
        shadow.localScale = Vector3.one * (shadowBaseScale * Mathf.Lerp(1.12f, 0.66f, k));
        shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.46f, 0.16f, k));
    }

    private static Sprite ShadowSprite()
    {
        if (_shadowSprite != null) return _shadowSprite;
        int w = 64, h = 26;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        _shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);   // ppu64 → scale1에서 ~1 월드 폭
        return _shadowSprite;
    }

    private static bool charmHelpShown;   // 장신구 카드(세션 1회)

    public void Interact()
    {
        if (item == null || Inventory.Instance == null) return;

        int left = Inventory.Instance.Add(item, count);
        int picked = count - left;
        if (picked > 0)
        {
            AudioManager.Sfx("pickup", 0.9f, 0.08f);
            AcquireFeed.Notify(item, picked);   // 획득 알림 연출
            TutorialFlow.OnItemAcquired();        // 온보딩: 첫 아이템 → 배낭 안내

            // 첫 장신구 획득: 착용법 카드(세션 1회)
            if (item.kind == ItemData.ItemKind.Equipment && !charmHelpShown && HelpPopupUI.Instance != null)
            {
                charmHelpShown = true;
                HelpPopupUI.Instance.Show("charm", "장신구",
                    "*장신구*는 몸에 지니는 것만으로 힘이 되는 보물입니다.\n" +
                    "배낭[B]을 열고 왼쪽 *착용칸(3×3)*에 직접 배치하면 효과가 적용됩니다.\n" +
                    "장신구마다 차지하는 칸 크기가 달라, 어떻게 조합하느냐가 곧 전략입니다. [R]로 회전할 수 있습니다.");
            }
            if (QuestManager.Instance != null) QuestManager.Instance.ReportGather(item.id, picked);   // 채집 퀘스트 진행
        }
        if (left <= 0) Destroy(gameObject);       // 전부 주웠으면 제거
        else { count = left; Debug.Log("[ItemPickup] 인벤토리가 꽉 차서 일부만 주웠습니다."); }
    }

    void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }

    // 주울 수 있는 월드 아이템 생성. hover=true면 공중 둥둥+그림자. fromPos 지정 시 그 위치에서 튀어나오는 팝(상자용). spawnDelay로 순차.
    public static GameObject SpawnWorld(ItemData item, int count, Vector3 pos, float worldSize = 0.5f, bool hover = false, Vector3? fromPos = null, float spawnDelay = 0f)
    {
        if (item == null) return null;

        GameObject go = new GameObject("Dropped_" + item.itemName);
        go.transform.position = pos;

        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer >= 0) go.layer = itemLayer;
        else Debug.LogWarning("[ItemPickup] 'Item' 레이어가 없습니다. Tags & Layers에서 만들어주세요.");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;
        sr.sortingOrder = 10;   // 상자(5) 등 프롭보다 앞 — 드랍이 상자 뒤로 숨지 않게

        float scale = 1f;
        if (item.icon != null)
        {
            Vector2 sz = item.icon.bounds.size;
            float maxDim = Mathf.Max(sz.x, sz.y);
            scale = maxDim > 0.0001f ? worldSize / maxDim : 1f;
        }
        go.transform.localScale = Vector3.one * scale;

        var box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one * (worldSize / Mathf.Max(0.0001f, scale));   // 아이콘 없어도 줍기 가능하게 크기 보장

        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;
        pickup.count = count;
        pickup.baseScale = scale;
        pickup.baseY = pos.y;
        pickup.shadowBaseScale = worldSize * 0.9f;

        if (hover)
        {
            box.isTrigger = true;              // 통과 가능, F로 줍기
            pickup.hover = true;
            pickup.EnsureShadow();
            if (fromPos.HasValue)
            {
                // 상자 드랍: 상자 높이가 아니라 '아래 지면' 기준으로 둥둥 뜨게 목표점을 바닥 위로 스냅
                int mask = LayerMask.GetMask("Ground");
                var hit = Physics2D.Raycast(pos + Vector3.up * 0.1f, Vector2.down, 12f, mask);
                if (hit.collider != null) pos.y = hit.point.y + 0.55f;
                pickup.baseY = pos.y;

                pickup.spawning = true;
                pickup.spawnFrom = fromPos.Value;
                pickup.spawnTarget = pos;
                pickup.spawnDelay = spawnDelay;
                go.transform.position = fromPos.Value;
                go.transform.localScale = Vector3.zero;
            }
        }
        else
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        return go;
    }
}
