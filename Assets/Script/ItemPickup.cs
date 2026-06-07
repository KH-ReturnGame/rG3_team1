using UnityEngine;

// 월드에 떨어져 있거나 채집 가능한 아이템. 가까이서 F로 주우면 인벤토리로 들어가고 사라짐.
// 필요: 이 오브젝트에 Collider2D(트리거든 아니든 OK) — PlayerInteractor가 범위로 감지함.
public class ItemPickup : MonoBehaviour, IInteractable
{
    public ItemData item;
    public int count = 1;

    public string Prompt => item != null ? $"F: {item.itemName} 줍기" : "F: 줍기";

    public void Interact()
    {
        if (item == null || Inventory.Instance == null) return;

        int left = Inventory.Instance.Add(item, count);
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
}
