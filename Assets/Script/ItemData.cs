using UnityEngine;

// 아이템 정의(ScriptableObject). Project 창에서 우클릭 → Create → Inventory/Item 으로 에셋 생성.
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemKind { Material, Consumable, Equipment }

    public string id;               // 세이브/로드용 고유 id (비우면 에셋 파일 이름으로 식별)
    public string itemName = "아이템";
    public Sprite icon;             // 인벤토리/픽업에 보일 아이콘
    public int maxStack = 99;       // 한 칸에 쌓을 수 있는 최대 개수
    [TextArea] public string description;

    [Header("종류 / 사용 효과")]
    public ItemKind kind = ItemKind.Material;
    public int healHearts = 0;          // Consumable: 사용 시 회복할 하트
    public float restoreStamina = 0f;   // Consumable: 사용 시 회복할 기력

    // 소비 아이템 사용 → 효과 적용. 실제로 효과가 들어가 소모됐으면 true(가득 차면 낭비 안 함).
    public bool Use()
    {
        if (kind != ItemKind.Consumable || GameManager.Instance == null) return false;
        var gm = GameManager.Instance;
        bool did = false;
        if (healHearts > 0 && gm.CurrentHearts < gm.MaxHearts) { gm.Heal(healHearts); did = true; }
        if (restoreStamina > 0f && gm.CurrentStamina < gm.MaxStamina) { gm.ChangeStamina(restoreStamina); did = true; }
        return did;
    }
}
