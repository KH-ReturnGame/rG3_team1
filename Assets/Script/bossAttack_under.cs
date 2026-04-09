using UnityEngine;
using System.Collections;

public class bossAttack_under : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Collider2D attackCollider;

    [Header("설정")]
    public float delayTime = 2.0f; // 대기 시간
    public Color warningColor = new Color(1f, 0f, 0f, 0.5f); // 반투명 빨간색
    public Color attackColor = new Color(1f, 0f, 0f, 1f);   // 불투명 빨간색

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        attackCollider = GetComponent<Collider2D>();
        
        // 생성 직후에는 충돌 판정을 꺼둡니다.
        attackCollider.enabled = false;
    }

    void Start()
    {
        // 오브젝트가 생성(활성화)되면 코루틴 시작
        StartCoroutine(AttackProcess());//신호 받고 쓰는걸로 ㄱㄱ
        
    }

    IEnumerator AttackProcess()
    {
        // 1. 반투명 상태로 표시
        spriteRenderer.color = warningColor;
        
        // 2. 2초 대기
        yield return new WaitForSeconds(delayTime);

        // 3. 불투명 상태로 전환 및 데미지 판정 활성화
        spriteRenderer.color = attackColor;
        attackCollider.enabled = true;

        // 4. 아주 짧은 시간 동안만 판정을 유지 (내려찍는 순간)
        yield return new WaitForSeconds(0.2f);
        
        // 5. 오브젝트 삭제 (또는 풀링 회수)
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // PlayerHealth 스크립트의 TakeDamage 함수 호출 (예시)
            // other.GetComponent<PlayerHealth>().TakeDamage(10);
            Debug.Log("빨간 공격 맞음");
        }
    }
}