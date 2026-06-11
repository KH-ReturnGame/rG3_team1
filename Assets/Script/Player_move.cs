using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class NewPlayerMovement : MonoBehaviour
{
    // --- 설정 값들 (변수명 정리 및 직관성 향상) ---
    [Header("Movement Settings")]
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float walkSpeedInGuard = 2.5f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private float dashStaminaCost = 20f;

    [Header("Guard & Parry Settings")]
    [SerializeField] private float guardStaminaDrainPerSec = 5f;
    [SerializeField] private float parryWindowDuration = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float guardDamageReduction = 0.5f; // 0은 모두 막음, 1은 감소 없음
    [SerializeField] private float parryStaminaRegen = 30f;

    [Header("Physics & Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Player Status")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private int maxHP = 100;

    // --- 컴포넌트 레퍼런스 ---
    private Rigidbody2D rb;

    // --- 내부 상태 변수 ---
    private float horizontalInput;
    private int facingDirection = 1; // 1: 오른쪽, -1: 왼쪽
    private bool isGrounded;
    
    // 입력 버퍼 (Update -> FixedUpdate로 플래그 전달)
    private bool bufferedJump;
    private bool bufferedDash;

    // 상태 플래그
    private bool isDashing;
    private bool canDash = true;
    private bool isGuarding;
    private bool isParryWindowActive;

    // 실시간 스탯
    [Header("Debug Status (Read Only)")]
    [SerializeField] private float currentStamina;
    [SerializeField] private int currentHP;

    // --- [최적화] 코루틴 가비지 제거용 캐싱 변수들 ---
    private WaitForSeconds dashDurationWait;
    private WaitForSeconds dashCooldownWait;
    private WaitForSeconds parryWindowWait;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        currentStamina = maxStamina;

        // [최적화] Awake에서 딱 한 번만 생성하여 메모리 낭비 제거
        dashDurationWait = new WaitForSeconds(dashDuration);
        dashCooldownWait = new WaitForSeconds(dashCooldown);
        parryWindowWait = new WaitForSeconds(parryWindowDuration);
    }

    private void Update()
    {
        // 1. 입력 수집
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 입력 "누름" 감지 (매 프레임 체크해야 함)
        if (Input.GetKeyDown(KeyCode.Space) && !bufferedJump) bufferedJump = true;
        if (Input.GetKeyDown(KeyCode.LeftShift) && !bufferedDash) bufferedDash = true;

        if (Input.GetMouseButtonDown(1)) StartGuard();
        if (Input.GetMouseButtonUp(1)) EndGuard();

        // 2. 비물리 로직 처리
        //[최적화] 실제 방향 전환시에만 변경하도록 개선
        UpdateSpriteFlip(); 
        
        //[최적화] 코루틴 대신 Update에서 프레임 기반 드레인 처리 (더 효율적)
        UpdateStaminaInGuard();

        if (!isGuarding && !isDashing && currentStamina < maxStamina)
        {
            currentStamina += 3f * Time.deltaTime; if (currentStamina > maxStamina) currentStamina = maxStamina;
        }
    }

    private void FixedUpdate()
    {
        // 3. 물리 연산 처리
        CheckGroundStatus(); // 땅 체크는 물리 프레임마다

        if (!isDashing)
        {
            ApplyHorizontalMovement();
            ApplyJump();
        }

        // 대시 시도
        if (bufferedDash)
        {
            TryDash();
            bufferedDash = false; // 입력 소비
        }
    }

    // --- 이동 관련 (최적화 버전) ---
    private void ApplyHorizontalMovement()
    {
        float targetSpeed = isGuarding ? walkSpeedInGuard : runSpeed;
        
        // 현재 속도의 x값만 변경, y값은 유지하여 자연스러운 낙하 구현
        rb.linearVelocity = new Vector2(horizontalInput * targetSpeed, rb.linearVelocity.y);
    }

    private void UpdateSpriteFlip()
    {
        // 입력이 있고, 현재 바라보는 방향과 다를 때만 Scale 변경
        if (horizontalInput != 0 && (int)Mathf.Sign(horizontalInput) != facingDirection)
        {
            facingDirection = (int)Mathf.Sign(horizontalInput);
            
            // Vector3.one을 활용하여 임시 생성 제거
            Vector3 newScale = Vector3.one;
            newScale.x = facingDirection;
            transform.localScale = newScale;
        }
    }

    private void ApplyJump()
    {
        if (bufferedJump)
        {
            bufferedJump = false; // 입력 소비

            if (isGrounded && !isGuarding)
            {
                // 점프는 순간적인 힘이므로 ForceMode2D.Impulse 권장 (혹은 velocity 직접 대입)
                // 기존 코드의 velocity 대입 방식 유지
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
        }
    }

    // --- 대시 관련 (최적화 버전) ---
    private void TryDash()
    {
        if (isGuarding || isDashing || !canDash || currentStamina < dashStaminaCost) return;

        StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;
        currentStamina -= dashStaminaCost;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f; // 대시 중 무중력

        // 대시 방향 설정
        int inputDir = horizontalInput != 0 ? (int)Mathf.Sign(horizontalInput) : facingDirection;
        rb.linearVelocity = new Vector2(inputDir * dashSpeed, 0f); // y속도 제거로 칼같은 대시

        // [최적화] 캐싱된 WaitForSeconds 사용 (가비지 Zero)
        yield return dashDurationWait;

        rb.gravityScale = originalGravity;
        
        // 대시 종료 후 관성 제거 (필요에 따라 주석 처리 가능)
        rb.linearVelocity = new Vector2(inputDir * runSpeed, 0f);
        
        isDashing = false;

        yield return dashCooldownWait;
        canDash = true;
    }

    // --- 가드 및 패링 관련 (최적화 버전) ---
    private void StartGuard()
    {
        if (isDashing || isGuarding || currentStamina <= 0f) return;

        isGuarding = true;
        
        // 속도 급감 (가드 시 정지 느낌)
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        
        StartCoroutine(ParryWindowRoutine());
    }

    private void EndGuard()
    {
        isGuarding = false;
        isParryWindowActive = false; // 패링 윈도우도 같이 종료
    }

    private IEnumerator ParryWindowRoutine()
    {
        isParryWindowActive = true;
        // [최적화] 캐싱된 WaitForSeconds 사용
        yield return parryWindowWait;
        isParryWindowActive = false;
    }

    private void UpdateStaminaInGuard()
    {
        if (!isGuarding) return;

        currentStamina -= guardStaminaDrainPerSec * Time.deltaTime;

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            EndGuard(); // 스태미나 고갈 시 가드 강제 해제
        }
    }

    // --- 유틸리티 및 외부 API ---
    private void CheckGroundStatus()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    public void TakeDamage(int damage)
    {
        // 1. 무적 상태 (대시 중)
        if (isDashing) return;

        // 2. 가드 중 피해
        if (isGuarding)
        {
            if (isParryWindowActive)
            {
                // 패링 성공
                Debug.Log("Parry Success!");
                currentStamina = Mathf.Min(maxStamina, currentStamina + parryStaminaRegen);
                // 여기에 패링 효과음/이펙트 추가
                return;
            }
            else
            {
                // 일반 가드
                int reducedDamage = Mathf.CeilToInt(damage * guardDamageReduction);
                currentHP -= reducedDamage;
                Debug.Log($"Guarded. Damage: {reducedDamage}. HP: {currentHP}");
            }
        }
        else
        {
            // 3. 무방비 피해
            currentHP -= damage;
            Debug.Log($"Hit. Damage: {damage}. HP: {currentHP}");
        }

        if (currentHP <= 0)
        {
            currentHP = 0;
            // 죽음 처리 로직 추가
        }
    }

    // --- 디버그 ---
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
#endif
}