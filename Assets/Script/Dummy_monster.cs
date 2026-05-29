using UnityEngine;
using System.Collections;

public class DummyMonster : MonoBehaviour
{
    [Header("Monster Stats")]
    public float maxHealth = 100f;    // [추가] 최대 체력
    private float currentHealth;       // [추가] 현재 체력
    public float attackInterval = 3f; 
    public float attackDamage = 10f;  
    public float attackRange = 2.5f;  

    [Header("References")]
    public Transform player; 
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    [Header("Groggy State")]
    public float groggyDuration = 2f; 
    private bool isGroggy = false;
    private float groggyTimer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color; 
        
        currentHealth = maxHealth; // [추가] 시작할 때 체력 만땅
        StartCoroutine(AttackRoutine());
    }

    void Update()
    {
        if (isGroggy)
        {
            groggyTimer -= Time.deltaTime;
            if (groggyTimer <= 0)
            {
                RecoverFromGroggy();
            }
        }
    }

    // [추가된 부분] 플레이어에게 공격받았을 때 호출되는 함수
    public void TakePlayerDamage(float damage)
    {
        // 만약 기절(그로기) 상태에서 맞으면 데미지 1.5배 뻥튀기! (로그라이크 연계기)
        if (isGroggy)
        {
            damage *= 1.5f;
            Debug.Log("💥 그로기 상태의 적에게 치명타(1.5배) 적용!");
        }

        currentHealth -= damage;
        Debug.Log($"⚔️ 허수아비가 {damage}의 데미지를 받음! 남은 체력: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // [추가된 부분] 허수아비 사망 처리 (테스트용이므로 파괴 후 2초 뒤 부활)
    private void Die()
    {
        Debug.Log("💀 허수아비가 파괴되었습니다! 2초 후 자동 부활합니다.");
        this.gameObject.SetActive(false);
        
        // 꼼수로 부활시키기 위해 Invoke 사용
        Invoke("Respawn", 2f);
    }

    private void Respawn()
    {
        this.gameObject.SetActive(true);
        currentHealth = maxHealth;
        spriteRenderer.color = originalColor;
        isGroggy = false;
        Debug.Log("♻️ 허수아비 부활 완료!");
    }

    IEnumerator AttackRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(attackInterval);
            if (!isGroggy && player != null && gameObject.activeSelf)
            {
                float distance = Vector2.Distance(transform.position, player.position);
                if (distance <= attackRange)
                {
                    Attack();
                }
            }
        }
    }

    private void Attack()
    {
        Debug.Log("💀 허수아비: 플레이어를 공격한다!");
        StartCoroutine(AttackVisual());

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(attackDamage, true, this); 
        }
    }

    IEnumerator AttackVisual()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        if (!isGroggy) spriteRenderer.color = originalColor;
    }

    public void ApplyGroggy()
    {
        isGroggy = true;
        groggyTimer = groggyDuration;
        spriteRenderer.color = Color.blue; 
        Debug.Log("🌀 허수아비: 패링 당해서 기절했다!");
    }

    private void RecoverFromGroggy()
    {
        isGroggy = false;
        spriteRenderer.color = originalColor;
        Debug.Log("허수아비: 기절에서 깨어났다.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}