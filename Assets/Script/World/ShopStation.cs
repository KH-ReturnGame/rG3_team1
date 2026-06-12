using UnityEngine;

// 상점 오브젝트(F로 상호작용 → 상점 UI 열기). Collider2D 필요(PlayerInteractor가 감지).
public class ShopStation : MonoBehaviour, IInteractable
{
    public string Prompt => "F: 상점";
    public void Interact() { if (ShopUI.Instance != null) ShopUI.Instance.Open(); }
}
