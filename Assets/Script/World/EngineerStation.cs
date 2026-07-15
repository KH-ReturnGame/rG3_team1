        using UnityEngine;

// 엔지니어 NPC: F로 상호작용 → 후드(모듈·스탯) 업그레이드 창을 '업그레이드 모드'로 연다.
//  · 최초 상호작용 시 1회: 망토를 수리하며 '미니맵 모듈'을 무상 장착(획득 연출) — 그 뒤부터 업그레이드 창.
//  · C키로 직접 연 후드 창은 조회 전용 — 실제 업그레이드(+ 버튼)는 이 엔지니어에서만.
// 오브젝트에 Collider2D 필요(PlayerInteractor가 F 입력·프롬프트 처리).
public class EngineerStation : MonoBehaviour, IInteractable
{
    public string Prompt => "F: 엔지니어";

    public void Interact()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.ReportVisit("engineer");   // 마을 둘러보기 진행
        // ★대화 → 창 체인이 입력 타이밍에 따라 간헐적으로 깨지는 고질병 → 대화를 생략하고 즉시 강화창을 연다(축제 안정성).
        //   (구) npc.Run(OpenEngineer) 대사 경유 방식은 폐지 — NpcDialogue 대사는 남아있지만 사용 안 함.
        OpenEngineer();
    }

    private void OpenEngineer() { if (InventoryUI.Instance != null) InventoryUI.Instance.OpenEngineer(); }
}
