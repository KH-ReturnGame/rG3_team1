using UnityEngine;

// F로 상호작용하는 문/출구. PlayerInteractor가 F 입력·프롬프트 처리. 오브젝트에 Collider2D 필요.
//  - GoToScene      : 지정한 고정 씬으로 이동(튜토리얼→마을, 집/상점 입구 등)
//  - AdvanceRunStage: 다음 스테이지/보스/결과로 진행. 목표 씬은 GameFlow가 런 진행도로 결정하므로
//                     모든 스테이지가 '같은 chunkend 프리팹'을 공유해도 Stage1→2→3 진행이 됨.
public class SceneDoor : MonoBehaviour, IInteractable
{
    public enum DoorAction { GoToScene, AdvanceRunStage, ClearRun }

    public DoorAction action = DoorAction.GoToScene;
    public string targetScene = "StartingArea";   // GoToScene 모드일 때만 사용
    public string prompt = "F: 이동";

    public string Prompt => prompt;

    public void Interact()
    {
        if (GameFlow.Instance == null) return;
        if (action == DoorAction.AdvanceRunStage)
            GameFlow.Instance.AdvanceStage();      // 다음 스테이지/보스/결과
        else if (action == DoorAction.ClearRun)
            GameFlow.Instance.ClearRun();          // 보스 클리어 포탈 → 클리어 결과창
        else
            GameFlow.Instance.GoToScene(targetScene);
    }
}
