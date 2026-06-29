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
        var gm = GameManager.Instance;
        if (gm != null && gm.moduleMinimap == 0)   // 최초 1회: 망토 수리 + 미니맵 모듈 지급
        {
            Toast.Show("엔지니어: 망토 꼴이 말이 아니군... 수리하는 김에 미니맵 모듈도 달아줬다.\n다시 [F]를 눌러 업그레이드 메뉴를 열 수 있다.", 4.5f);
            gm.GrantMinimap();
            return;
        }
        if (InventoryUI.Instance != null) InventoryUI.Instance.OpenEngineer();
    }
}
