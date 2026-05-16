using UnityEngine;
using System.Collections;

public class boss_movement : MonoBehaviour
{
    public Transform player;      // 플레이어의 위치
    public float moveSpeed = 6.5f;  // 이동 속도
    public float detectionRange = 20f; // 탐지 범위
    public float Attack_distance = 3f; // 플레이어 감지 범위
    
    // 상태를 명확하게 하기 위해 변수명을 바꿨습니다.
    private bool isAttacking = false; 

    [Header("공격 설정")]
    public GameObject blueAttackPrefab; // 유니티 에디터에서 꼭 할당해주세요!

    void Update()
    {
        if (player == null) return;

        // 공격 중일 때는 이동 로직을 아예 실행하지 않고 즉시 리턴(보스 정지)
        if (isAttacking) return;

        float distance = Vector2.Distance(transform.position, player.position);
        
        // 1. 공격 범위 안에 들어왔을 때
        if (distance <= Attack_distance)
        {
            StartCoroutine(AttackRoutine());
        }
        // 2. 공격 범위 밖이고, 탐지 범위 안일 때만 이동
        else if (distance <= detectionRange)
        {
            MoveTowardsPlayer();
        }
    }

    void MoveTowardsPlayer()
    {
        Vector2 targetPosition = new Vector2(player.position.x, transform.position.y);
        transform.position = Vector2.MoveTowards(
            transform.position, 
            targetPosition, 
            moveSpeed * Time.deltaTime
        );
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true; // 이동 차단 시작

        if (blueAttackPrefab != null)
        {
            // 보스의 위치에 공격 프리팹 생성
            GameObject attackInstance = Instantiate(blueAttackPrefab, transform.position, Quaternion.identity);
            Debug.Log($"공격 생성 성공: {attackInstance.name}이 {transform.position} 위치에 생성됨.");
            
            // 컴포넌트를 가져와 작동 신호 전달
            blue_Attack attackScript = attackInstance.GetComponent<blue_Attack>();
            if (attackScript != null)
            {
                attackScript.TriggerAttack();
            }
            else
            {
                Debug.LogError("오류: 생성된 프리팹에 'blue_Attack' 스크립트가 없습니다!");
            }
        }
        else
        {
            Debug.LogError("오류: 인스펙터 창에서 'Blue Attack Prefab'이 비어있습니다!");
        }

        // 공격 후 대기 시간 (예: 2초 동안 멈춰있음)
        // blue_Attack 장판이 터지는 시간(delayTime) 등을 고려해서 조절하세요.
        yield return new WaitForSeconds(2.0f); 

        isAttacking = false; // 이동 차단 해제 (다시 추적 시작)
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, Attack_distance);
    }
}