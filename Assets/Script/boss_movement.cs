using UnityEngine;
using System.Collections;

public class boss_movement : MonoBehaviour
{
    public Transform player;      // 플레이어의 위치
    public float moveSpeed = 3f;  // 이동 속도
    public float detectionRange = 20f; // 탐지 범위
    public float Attack_distance = 3f; // 플레이어 감지 범위
    private bool isPaused = false; // 이동 가능 여부 체크

    void Update()
    {
        if (player == null) return;

        // 보스와 플레이어 사이의 거리 계산
        float distance = Vector2.Distance(transform.position, player.position);
        
        // 거리가 탐지 범위 안에 들어오면 이동
        if (distance <= detectionRange && distance >= Attack_distance)
        {
            MoveTowardsPlayer();
        }
        else if (distance <= Attack_distance)
        {
            if (!isPaused) // 이미 멈춘 상태가 아닐 때만 실행
            {
                StartCoroutine(PauseRoutine());
            }
        }
    }

    void MoveTowardsPlayer()
    {
        // 현재 위치에서 플레이어 위치로 일정한 속도로 이동
        transform.position = Vector2.MoveTowards(
            transform.position, 
            player.position, 
            moveSpeed * Time.deltaTime
        );
    }

    // 에디터 뷰에서 탐지 범위를 시각적으로 확인 (선택 사항)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
    IEnumerator PauseRoutine()
    {
        isPaused = true;        // 1. 이동 플래그 끄기
        Debug.Log("ㄴㄷㅌ");
        yield return new WaitForSeconds(1.0f); // 2. 1초 대기
        isPaused = false;       // 3. 이동 플래그 다시 켜기
    }

    
}
