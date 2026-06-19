using UnityEngine;

// 엔지니어 NPC: F로 상호작용 → 후드(모듈·스탯) 업그레이드 창을 '업그레이드 모드'로 연다.
//  · C키로 직접 연 후드 창은 조회 전용 — 실제 업그레이드(+ 버튼)는 이 엔지니어에서만.
// 오브젝트에 Collider2D 필요(PlayerInteractor가 F 입력·프롬프트 처리).
public class EngineerStation : MonoBehaviour, IInteractable
{
    public string Prompt => "F: 엔지니어 (모듈 업그레이드)";

    public void Interact()
    {
        if (InventoryUI.Instance != null) InventoryUI.Instance.OpenEngineer();
    }
}
