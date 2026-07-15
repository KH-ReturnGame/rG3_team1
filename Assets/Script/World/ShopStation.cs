using UnityEngine;

// 상점 오브젝트(F로 상호작용). NpcDialogue가 붙어 있으면 F → 대화 → (끝나면) 상점 UI. 없으면 바로 상점.
// Collider2D 필요(PlayerInteractor가 감지).
public class ShopStation : MonoBehaviour, IInteractable
{
    public int merchant = 0;   // 0=재료 1=포션 2=탐험가
    public string Prompt => merchant == 0 ? "F: 재료 상인" : (merchant == 1 ? "F: 포션 상인" : "F: 탐험가");

    private static bool ropeGiven;   // 세션당 1회(재지급은 로프를 다 썼을 때만)

    public void Interact()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.ReportVisit("shop");   // 마을 둘러보기 진행
        GiveStarterRopes();   // 재료 상인: 동굴탈출로프 기본 5개 지급(미보유 시)
        var npc = GetComponent<NpcDialogue>();
        if (npc != null && npc.HasLines) npc.Run(OpenShop);
        else OpenShop();
    }

    // 재료 상인이 동굴탈출로프 5개를 무상 지급 — 세션당 1회, 이미 갖고 있으면 생략(중복 방지)
    private void GiveStarterRopes()
    {
        if (merchant != 0 || ropeGiven || Inventory.Instance == null) return;
        var rope = ItemDatabase.Get("escape_rope");
        if (rope == null) return;
        if (Inventory.Instance.CountOf(rope) > 0) { ropeGiven = true; return; }   // 이미 보유 → 지급 안 함
        ropeGiven = true;
        Inventory.Instance.Add(rope, 5);
        AcquireBanner.Show("동굴탈출로프 x5", "지하 어디서든 사용하면 마을로 돌아온다.", rope.icon != null ? rope.icon.texture : null, "재료 상인 — 챙겨가게, 초행이잖나");
    }

    private void OpenShop() { if (ShopUI.Instance != null) ShopUI.Instance.Open(merchant); }
}
