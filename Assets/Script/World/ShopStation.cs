using UnityEngine;

// 상점 오브젝트(F로 상호작용). NpcDialogue가 붙어 있으면 F → 대화 → (끝나면) 상점 UI. 없으면 바로 상점.
// Collider2D 필요(PlayerInteractor가 감지).
public class ShopStation : MonoBehaviour, IInteractable
{
    public int merchant = 0;   // 0=재료 1=포션 2=탐험가
    public string Prompt => merchant == 0 ? "F: 재료 상인" : (merchant == 1 ? "F: 포션 상인" : "F: 탐험가");

    public void Interact()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.ReportVisit("shop");   // 마을 둘러보기 진행
        var npc = GetComponent<NpcDialogue>();
        if (npc != null && npc.HasLines) npc.Run(OpenShop);
        else OpenShop();
    }

    private void OpenShop() { if (ShopUI.Instance != null) ShopUI.Instance.Open(merchant); }
}
