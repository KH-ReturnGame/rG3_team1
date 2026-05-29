using UnityEngine;
using System.Collections;

public class blue_Attack : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Collider2D attackCollider;

    [Header("설정")]
    public float delayTime = 0.75f; // 대기 시간
    public Color warningColor = new Color(0f, 0f, 1f, 0.5f);
    public Color attackColor = new Color(0f, 0f, 1f, 1f);

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        attackCollider = GetComponent<Collider2D>();
        
        // 생성 직후에는 충돌 판정을 꺼둡니다.
        attackCollider.enabled = false;
    }

    // 보스가 이 함수를 호출하여 공격을 발동시킵니다.
    public void TriggerAttack()
    {
        StartCoroutine(AttackProcess());
    }

    IEnumerator AttackProcess()
    {
        // 1. 반투명 상태로 표시
        spriteRenderer.color = warningColor;
        
        // 2. 대기
        yield return new WaitForSeconds(delayTime);

        // 3. 불투명 상태로 전환 및 데미지 판정 활성화
        spriteRenderer.color = attackColor;
        attackCollider.enabled = true;

        // 4. 아주 짧은 시간 동안만 판정을 유지
        yield return new WaitForSeconds(0.2f);
        
        // 5. 오브젝트 삭제
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("파란 공격 맞음");
        }
    }
}