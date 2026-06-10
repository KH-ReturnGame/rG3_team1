using UnityEngine;

// 마을(허브)의 우물 등 '런 진입' 트리거. 실제 전환/진행은 GameFlow가 관리.
public class StartAreaTeleporter : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            GameFlow.Instance?.EnterRun();   // 우물 진입 → 런 시작(스테이지1)
    }
}
