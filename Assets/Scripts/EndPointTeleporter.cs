using UnityEngine;

// 스테이지 출구 트리거. 다음 스테이지/보스/결과 진행은 GameFlow가 관리(currentStage 증가 포함).
public class EndPointTeleporter : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            GameFlow.Instance?.AdvanceStage();   // 스테이지 출구 → 다음 또는 결과
    }
}
