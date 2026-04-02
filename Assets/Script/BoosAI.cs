using UnityEngine;
using System.Collections;

public class BossAI : MonoBehaviour
{
    [Header("탐지 설정")]
    public float detectionRange = 3f; // 플레이어 감지 범위
    public LayerMask playerLayer;      // 플레이어 레이어 선택

    [Header("공격 설정")]
    public GameObject attackPrefab;    // 아까 만든 빨간 장판 프리팹
    public float attackCooldown = 3f;  // 공격 간격 (초)
    
    private bool isAttacking = false;
    private Transform player;

    

    void Start()
    {
        // "Player" 태그를 가진 오브젝트를 찾습니다.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (player == null || isAttacking) return;

        // 1. 플레이어와의 거리 계산
        float distance = Vector2.Distance(transform.position, player.position);

        // 2. 범위 안에 들어오면 공격 실행
        if (distance <= detectionRange)
        {
            StartCoroutine(PerformAttack());
        }
    }

    IEnumerator PerformAttack()
    {
        isAttacking = true;

        // 장판 생성 (플레이어 발밑 또는 보스 앞 등 원하는 위치)
        // 여기서는 플레이어의 현재 위치에 장판을 생성합니다.
        Instantiate(attackPrefab, player.position, Quaternion.identity);

        // 공격 쿨타임 대기
        yield return new WaitForSeconds(attackCooldown);

        isAttacking = false;
    }

    // 에디터 뷰에서 감지 범위를 시각적으로 확인하기 위함
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}