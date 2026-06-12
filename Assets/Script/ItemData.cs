using UnityEngine;

// 아이템 정의(ScriptableObject). Project 창에서 우클릭 → Create → Inventory/Item 으로도 생성 가능.
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemKind { Material, Consumable, Equipment, Valuable }

    public string id;               // 세이브/로드용 고유 id (비우면 에셋 파일 이름)
    public string itemName = "아이템";
    public Sprite icon;
    public int maxStack = 99;
    [TextArea] public string description;

    [Header("종류")]
    public ItemKind kind = ItemKind.Material;

    [Header("소비 효과 (Consumable)")]
    public int healHearts = 0;
    public float restoreStamina = 0f;
    public float tempAttackMult = 0f;        // 전투 포션: 공격력 +배수 (0.5 = +50%)
    public float tempDamageReduction = 0f;   // 방어력 포션: 피해 감량 (0~1, 0.5 = 50%)
    public float buffDuration = 0f;          // 위 버프 지속(초)

    [Header("장신구 (Equipment) — 착용 시 보너스 (착용칸은 추후)")]
    public int maxJumpBonus = 0;
    public int maxHeartBonus = 0;
    public float maxStaminaBonus = 0f;
    public float staminaRegenBonus = 0f;
    public float attackBonus = 0f;

    [Header("판매 (골동품 등)")]
    public int sellValue = 0;                // 상점 판매가(골드)

    // 소비 아이템 사용 → 효과 적용. 실제로 효과가 들어가 소모됐으면 true(가득 차면 낭비 안 함).
    public bool Use()
    {
        if (kind != ItemKind.Consumable || GameManager.Instance == null) return false;
        var gm = GameManager.Instance;
        bool did = false;
        if (healHearts > 0 && gm.CurrentHearts < gm.MaxHearts) { gm.Heal(healHearts); did = true; }
        if (restoreStamina > 0f && gm.CurrentStamina < gm.MaxStamina) { gm.ChangeStamina(restoreStamina); did = true; }
        if (tempAttackMult > 0f && buffDuration > 0f) { gm.ApplyAttackBuff(tempAttackMult, buffDuration); did = true; }
        if (tempDamageReduction > 0f && buffDuration > 0f) { gm.ApplyDefenseBuff(tempDamageReduction, buffDuration); did = true; }
        return did;
    }
}
