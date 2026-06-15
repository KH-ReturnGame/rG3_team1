using UnityEngine;

// 월드에 떨어져 있거나 채집 가능한 아이템. 가까이서 F로 주우면 인벤토리로 들어가고 사라짐.
// 필요: 이 오브젝트에 Collider2D(트리거든 아니든 OK) — PlayerInteractor가 범위로 감지함.
// hover=true 면 중력 없이 공중에 둥둥 떠 있음(보물상자 드랍 등).
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemData item;
    public int count = 1;

    [Header("공중 부유 (상자 드랍 등)")]
    public bool hover = false;          // true면 중력 없이 둥둥
    public float bobAmplitude = 0.22f;  // 위아래 폭
    public float bobSpeed = 2.2f;       // 흔들리는 속도
    private float baseY;
    private float bobPhase;

    public string Prompt => item != null ? $"F: {item.itemName} 줍기" : "F: 줍기";

    void Start()
    {
        baseY = transform.position.y;
        bobPhase = Random.value * 6.2831853f;
    }

    void Update()
    {
        if (!hover) return;
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude;   // 둥둥
        transform.position = p;
    }

    public void Interact()
    {
        if (item == null || Inventory.Instance == null) return;

        int left = Inventory.Instance.Add(item, count);
        int picked = count - left;
        if (picked > 0 && QuestManager.Instance != null) QuestManager.Instance.ReportGather(item.id, picked);   // 채집 퀘스트 진행
        if (left <= 0)
        {
            Destroy(gameObject);   // 전부 주웠으면 제거
        }
        else
        {
            count = left;          // 인벤이 꽉 차서 일부만 주움 → 남은 만큼 남겨둠
            Debug.Log("[ItemPickup] 인벤토리가 꽉 차서 일부만 주웠습니다.");
        }
    }

    // 주울 수 있는 월드 아이템을 생성하는 공용 메서드 (인벤토리 버리기 / 적 드랍 / 보물상자 등에서 사용).
    // hover=true 면 중력 없이 공중에 둥둥(상자 드랍). false면 중력 받아 바닥에 떨어짐(기존 적 드랍).
    public static GameObject SpawnWorld(ItemData item, int count, Vector3 pos, float worldSize = 0.5f, bool hover = false)
    {
        if (item == null) return null;

        GameObject go = new GameObject("Dropped_" + item.itemName);
        go.transform.position = pos;

        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer >= 0) go.layer = itemLayer;
        else Debug.LogWarning("[ItemPickup] 'Item' 레이어가 없습니다. Tags & Layers에서 만들어주세요.");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;              // 인벤토리 아이콘과 같은 스프라이트
        sr.sortingOrder = 1;

        // 스프라이트 원본 크기(PPU)와 무관하게 목표 월드 크기로 맞춤
        if (item.icon != null)
        {
            Vector2 sz = item.icon.bounds.size;
            float maxDim = Mathf.Max(sz.x, sz.y);
            float scale = maxDim > 0.0001f ? worldSize / maxDim : 1f;
            go.transform.localScale = Vector3.one * scale;
        }

        var box = go.AddComponent<BoxCollider2D>();   // 줍기 감지(+바닥 충돌)

        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;
        pickup.count = count;

        if (hover)
        {
            box.isTrigger = true;          // 통과 가능(플레이어 안 막음), F로 줍기
            pickup.hover = true;
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
