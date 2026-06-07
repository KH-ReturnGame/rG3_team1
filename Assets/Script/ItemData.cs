using UnityEngine;

// 아이템 정의(ScriptableObject). Project 창에서 우클릭 → Create → Inventory/Item 으로 에셋 생성.
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName = "아이템";
    public Sprite icon;             // 인벤토리/픽업에 보일 아이콘
    public int maxStack = 99;       // 한 칸에 쌓을 수 있는 최대 개수
    [TextArea] public string description;
}
