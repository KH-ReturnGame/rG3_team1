using UnityEngine;

// 제작대 오브젝트(F로 상호작용 → 제작 UI 열기). Collider2D 필요(PlayerInteractor가 감지).
public class CraftStation : MonoBehaviour, IInteractable
{
    public string Prompt => "F: 제작대";
    public void Interact() { if (CraftingUI.Instance != null) CraftingUI.Instance.Open(); }
}
