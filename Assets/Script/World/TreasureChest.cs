using System.Collections.Generic;
using UnityEngine;

// 보물상자: F로 열어 보상(아이템/골드)을 받는다. 탐험의 핵심 발견 대상.
//  - PlayerInteractor가 반경 안의 IInteractable을 F로 실행(Collider2D 필요 → 트리거 박스 사용)
//  - TreasureDetector(Z)가 All 리스트에서 가장 가까운 '안 연' 상자를 찾아 방향을 알려줌
//  - 연 상자는 세션 동안 닫힌 채 유지(씬 재진입에도). 영구 저장은 추후 SaveData 연동.
[RequireComponent(typeof(Collider2D))]
public class TreasureChest : MonoBehaviour, IInteractable
{
    public static readonly List<TreasureChest> All = new List<TreasureChest>();
    private static readonly HashSet<string> openedKeys = new HashSet<string>();   // 연 상자 기억(세션)

    [Header("식별 (비우면 씬+위치로 자동 생성)")]
    public string chestId = "";

    [Header("보상")]
    public string lootItemId = "";   // Resources/Items 의 아이템 id (비우면 아이템 없음)
    public int lootCount = 1;
    public int lootGold = 0;

    [Header("드랍 연출 (몬스터 처치와 동일)")]
    public Sprite goldSprite;        // 코인 스프라이트(비우면 노란 원 자동)
    public int coinMin = 5;          // 골드를 흩뿌릴 코인 개수 범위
    public int coinMax = 9;
    public float dropSize = 0.5f;    // 떨군 아이템 월드 크기
    public float dropScatter = 0.4f; // 흩어지는 정도

    [Header("연출 (선택)")]
    public Sprite openedSprite;      // 열렸을 때 스프라이트(있으면 교체, 없으면 어둡게)
    public string prompt = "F: 상자 열기";

    private bool isOpen;
    private SpriteRenderer sr;
    private Collider2D col;

    private string Key => string.IsNullOrEmpty(chestId)
        ? gameObject.scene.name + ":" + transform.position.x.ToString("0.0") + "," + transform.position.y.ToString("0.0")
        : chestId;

    public string Prompt => isOpen ? "" : prompt;
    public bool IsOpened => isOpen;
    public Vector3 Position => transform.position;

    void Awake() { sr = GetComponent<SpriteRenderer>(); col = GetComponent<Collider2D>(); }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (openedKeys.Contains(Key)) ApplyOpenedVisual();   // 이미 연 상자면 닫힌 상태 복원
    }
    void OnDisable() { All.Remove(this); }

    public void Interact()
    {
        if (isOpen) return;
        openedKeys.Add(Key);

        Vector3 origin = transform.position + Vector3.up * 0.3f;
        Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;

        // 아이템: 바닥에 떨궈 플레이어가 F로 줍게(몬스터 드랍과 동일)
        if (!string.IsNullOrEmpty(lootItemId))
        {
            ItemData item = ItemDatabase.Get(lootItemId);
            if (item != null)
            {
                Vector3 pos = origin + (Vector3)(Random.insideUnitCircle * dropScatter);
                ItemPickup.SpawnWorld(item, Mathf.Max(1, lootCount), pos, dropSize);
            }
        }

        // 골드: 코인 여러 개로 흩뿌려 플레이어에게 빨려가게(도착 시 적립)
        if (lootGold > 0)
        {
            int coins = Mathf.Clamp(Random.Range(coinMin, coinMax + 1), 1, lootGold);
            int per = lootGold / coins, rem = lootGold % coins;
            for (int i = 0; i < coins; i++)
                GoldCoin.Spawn(origin, per + (i < rem ? 1 : 0), goldSprite, player);
        }

        Toast.Show("보물 상자를 열었다!", 2f);
        ApplyOpenedVisual();
    }

    private void ApplyOpenedVisual()
    {
        isOpen = true;
        if (sr != null)
        {
            if (openedSprite != null) sr.sprite = openedSprite;
            else { Color c = sr.color; sr.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f); }   // 어둡게 = 열림 표시
        }
        if (col != null) col.enabled = false;   // 더는 상호작용/감지 대상 아님
    }
}
