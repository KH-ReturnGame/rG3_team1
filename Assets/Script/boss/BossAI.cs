using UnityEngine;
using System.Collections;

public class BossAI : MonoBehaviour
{
    public Transform player;          // 플레이어의 위치
    public float moveSpeed = 6.5f;    // 이동 속도
    public float detectionRange = 20f; // 추적을 시작할 탐지 범위
    public float attackDistance = 6f;  // 공격을 시작할 감지 범위
    public float attackCooldown = 1.5f;  // 공격 성공 후 다음 공격까지의 재사용 대기시간
    
    [Tooltip("플레이어와 너무 가까워졌을 때 멈출 거리 설정")]
    public float stopDistance = 3f;  // 추가: 너무 가까워지면 멈추는 거리

    [Header("공격 프리팹 설정")]
    public GameObject redAttackPrefab;  // 빨간 장판 프리팹 (플레이어 위치 생성용)
    public GameObject blueAttackPrefab; // 파란 장판 프리팹 (보스 위치 생성용)
    public GameObject homingMissilePrefab; // 추가: 유도 미사일 프리팹

    private bool isAttacking = false;   // 공격 애니메이션/정지 상태 체크
    private bool isCooldown = false;    // 쿨타임 체크

    void Start()
    {
        // 플레이어가 에디터에서 할당 안 되었을 경우 태그로 자동 검색
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        if (isAttacking || isCooldown) return;

        // 플레이어와의 거리 계산
        float distance = Vector2.Distance(transform.position, player.position);

        // 1. 플레이어와 너무 가까우면 이동하지 않고 대기하며 근접 공격
        if (distance <= stopDistance)
        {
            StartCoroutine(ChooseAndPerformAttack());
            return; 
        }

        // 2. 공격 범위 안에 들어왔을 때 -> 확률적 공격 시도 후 대기
        if (distance <= attackDistance)
        {
            StartCoroutine(ChooseAndPerformAttack());
        }
        // 3. 공격 범위 밖이고, 탐지 범위 안일 때 -> 플레이어 추적 (Y축 고정)
        else if (distance <= detectionRange)
        {
            MoveTowardsPlayer();
        }
        // 4. 탐지 범위를 완전히 벗어났을 때 -> 순간이동 기습 패턴 발동!
        else if (distance > detectionRange)
        {
            StartCoroutine(TeleportAndAttackPattern());
        }
    }

    // 플레이어를 추적하는 메소드 (Y축은 보스 자신의 Y로 고정)
    void MoveTowardsPlayer()
    {
        Vector2 targetPosition = new Vector2(player.position.x, transform.position.y);
        transform.position = Vector2.MoveTowards(
            transform.position, 
            targetPosition, 
            moveSpeed * Time.deltaTime
        );
    }

    // 확률(80:20)에 따라 공격을 선택하고 실행하는 루틴
    IEnumerator ChooseAndPerformAttack()
    {
        isAttacking = true; // 이동 차단

        // 0.0부터 100.0 사이의 랜덤 정밀도 숫자 생성
        float randomChance = Random.Range(0f, 100f);

        // 1. 파란색 공격 (20% 확률: 0 ~ 20 미만)
        if (randomChance < 20f)
        {            
            if (blueAttackPrefab != null)
            {
                GameObject attackInstance = Instantiate(blueAttackPrefab, transform.position, Quaternion.identity);
                blue_Attack attackScript = attackInstance.GetComponent<blue_Attack>();
                if (attackScript != null)
                {
                    attackScript.TriggerAttack();
                }
            }
            else
            {
                Debug.LogError("오류: Blue Attack Prefab이 인스펙터에 할당되지 않았습니다.");
            }

            yield return new WaitForSeconds(1.5f);
        }
        // 2. 빨간색 공격 (80% 확률: 20 이상 ~ 100)
        else
        {
            if (redAttackPrefab != null)
            {
                Instantiate(redAttackPrefab, player.position, Quaternion.identity);
            }
            else
            {
                Debug.LogError("오류: Red Attack Prefab이 인스펙터에 할당되지 않았습니다.");
            }

            yield return new WaitForSeconds(1.0f);
        }

        // 공격 액션 끝, 쿨타임 돌입
        isAttacking = false;
        StartCoroutine(CooldownRoutine());
    }

    // [추가 개발] 멀어진 플레이어에게 순간이동하고 유도 미사일 연계 공격을 가하는 패턴
    IEnumerator TeleportAndAttackPattern()
    {
        isAttacking = true; // 다른 행동 차단
        
        // 1. 플레이어 위치로 순간이동 (기존 보스 Y축 높이 유지)
        Vector2 teleportPosition = new Vector2(player.position.x, transform.position.y);
        transform.position = teleportPosition;

        // 2. 순간이동 직후 플레이어가 반응할 미세한 유예 시간 생성 (0.2초)
        yield return new WaitForSeconds(0.2f);

        // 3. 기습 공격 연계: 유도 미사일 생성 후 발사
        if (homingMissilePrefab != null)
        {
            Debug.Log("🚀 유도 미사일 발사!");
            // 보스 위치보다 약간 위쪽에서 미사일 생성
            Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
            GameObject missile = Instantiate(homingMissilePrefab, transform.position + spawnOffset, Quaternion.identity);
            
            // 미사일에 타겟(플레이어) 정보 넘겨주기
            HomingMissile missileScript = missile.GetComponent<HomingMissile>();
            if (missileScript != null)
            {
                missileScript.target = this.player;
            }
        }
        else
        {
            Debug.LogWarning("Homing Missile Prefab이 할당되지 않아 기본 장판 공격으로 대체합니다.");
            // 미사일이 없을 때를 대비한 안전 장치로 기존 확률 공격 실행
            yield return StartCoroutine(ChooseAndPerformAttack());
            yield break;
        }

        // 미사일 발사 후 보스가 취하는 후딜레이 시간 (1초 대기)
        yield return new WaitForSeconds(1.0f);

        // 공격 종료 및 쿨타임 시작
        isAttacking = false;
        StartCoroutine(CooldownRoutine());
    }

    // 공격 연속 사용을 방지하는 쿨타임 시스템
    IEnumerator CooldownRoutine()
    {
        isCooldown = true;
        yield return new WaitForSeconds(attackCooldown);
        isCooldown = false;
    }

    // 에디터 뷰 가이드라인 표시
    void OnDrawGizmosSelected()
    {
        // 탐지 범위 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // 공격 범위 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // 추가: 최소 유지 거리 표시 (하늘색)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}