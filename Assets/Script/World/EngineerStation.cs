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
        var npc = GetComponent<NpcDialogue>();
        Sprite face = npc != null ? npc.portrait : null;

        if (gm != null && gm.moduleMinimap == 0)   // 최초 1회: 망토 수리 대화 → 미니맵 모듈 지급
        {
            string[] first = {
                "오, 자네가 그 빨간 망토로군. ...꼴이 영 말이 아닌데?",
                "이리 줘 보게. 수리하는 김에 — 옜다, 미니맵 모듈도 하나 박아줬네.",
                "이제 지나온 구역은 지도에 남을 걸세. [,]로 켜고 끄고, 지도는 [M]으로 보게나."
            };
            DialogueUI.Show("엔지니어", face, first, () => { if (GameManager.Instance != null) GameManager.Instance.GrantMinimap(); });
            return;
        }

        if (npc != null && npc.HasLines) npc.Run(OpenEngineer);
        else OpenEngineer();
    }

    private void OpenEngineer() { if (InventoryUI.Instance != null) InventoryUI.Instance.OpenEngineer(); }
}
