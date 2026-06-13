using UnityEngine;

// 상점 오브젝트(F로 상호작용 → 상점 UI 열기). Collider2D 필요(PlayerInteractor가 감지).
public class ShopStation : MonoBehaviour, IInteractable
{
    public int merchant = 0;   // 0=재료 1=포션 2=탐험가
    public string Prompt => merchant == 0 ? "F: 재료 상인" : (merchant == 1 ? "F: 포션 상인" : "F: 탐험가");
    public void Interact() { if (ShopUI.Instance != null) ShopUI.Instance.Open(merchant); }
}
