using UnityEngine;

// 잠긴 문: 지정한 '열쇠'(희귀도별 3종 중 하나)를 가지고 있어야 F로 열 수 있다.
//  - 열쇠가 있으면(consumeKey면 소모) 지정 동작 수행: 장벽 열기 / 씬 이동 / 다음 스테이지 진행.
//  - 진전을 막는 장벽(Collider2D + 스프라이트)과 함께 배치하면 '열쇠가 있어야 진행되는 스테이지'가 됨.
[RequireComponent(typeof(Collider2D))]
public class LockedDoor : MonoBehaviour, IInteractable
{
    public enum DoorAction { OpenBarrier, GoToScene, AdvanceRunStage }

    [Header("열쇠 요구")]
    public ItemData requiredKey;        // 필요한 열쇠(희귀도별 key_common/key_rare/key_epic) — 인스펙터에서 드래그
    public int requiredCount = 1;
    public bool consumeKey = true;      // 열면 열쇠 소모

    [Header("열렸을 때")]
    public DoorAction action = DoorAction.OpenBarrier;
    public GameObject barrier;          // OpenBarrier: 비활성화할 장벽(비우면 이 오브젝트 자신)
    public string targetScene = "";     // GoToScene 모드에서 이동할 씬

    public string Prompt
    {
        get { return "F: 잠긴 문 (" + (requiredKey != null ? requiredKey.itemName : "열쇠") + " 필요)"; }
    }

    public void Interact()
    {
        if (requiredKey != null)
        {
            var inv = Inventory.Instance;
            if (inv == null || inv.CountOf(requiredKey) < requiredCount)
            {
                Toast.Show(requiredKey.itemName + " " + requiredCount + "개가 필요합니다.", 2f);
                return;
            }
            if (consumeKey) inv.Remove(requiredKey, requiredCount);
            Toast.Show(requiredKey.itemName + "(으)로 문을 열었다!", 2f);
        }
        Open();
    }

    private void Open()
    {
        switch (action)
        {
            case DoorAction.GoToScene:
                if (GameFlow.Instance != null && !string.IsNullOrEmpty(targetScene)) GameFlow.Instance.GoToScene(targetScene);
                break;
            case DoorAction.AdvanceRunStage:
                if (GameFlow.Instance != null) GameFlow.Instance.AdvanceStage();
                break;
            default:
                if (barrier != null) barrier.SetActive(false);
                else gameObject.SetActive(false);
                break;
        }
    }
}
