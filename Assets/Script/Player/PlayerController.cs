using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Component References")]
    private Rigidbody2D rb;
    private Animator anim; 
    private SpriteRenderer sr;
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    private float currentMoveSpeed; 
    private float horizontalInput;

    [Header("Jump")]
    public float jumpForce = 10f;
    public int maxJumps = 1; 
    private int currentJumps;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Dash")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public int maxDashes = 1; 
    private int currentDashes;
    private bool isDashing;
    private float dashTimer; 
    private float dashDir; // [추가] 대시 방향을 기억하기 위한 변수

    [Header("Guard & Parry")]
    public float parryWindow = 0.5f; 
    private float parryTimer;
    private bool isGuarding;
    private bool isParrying;

    [Header("Combat - Attack")]
    public Transform attackPoint;      
    public float attackRadius = 0.6f;  
    public float attackDamage = 10f;   
    public LayerMask enemyLayer;       

    [Header("Combat - Skill (Q)")]
    public Vector2 skillBoxSize = new Vector2(2.5f, 1.2f); 
    public float skillDamage = 25f;    
    public float skillCooldown = 3f;   
    private float skillCooldownTimer;  

    void Awake()
    {
        // 컴포넌트 자동 연결
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); 
    }

    void Start()
    {
        currentMoveSpeed = moveSpeed;
        currentJumps = maxJumps;
        currentDashes = maxDashes;
    }

    void Update()
    {
        // 대시 중일 때는 타이머만 깎고 다른 입력(공격, 점프 등)을 무시합니다.
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) EndDash();
            return; 
        }

        CheckInput();
        CheckGrounded(); 
        
        if (isParrying)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0) isParrying = false;
        }

        if (skillCooldownTimer > 0)
        {
            skillCooldownTimer -= Time.deltaTime;
        }

        UpdateAnimations(); 
    }

    void FixedUpdate()
    {
        // [핵심] 대시 중일 때는 바닥 마찰력을 씹고 설정한 대시 속도로 묵직하게 밀어붙입니다.
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            return;
        }

        Move();
    }

    private void CheckInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetMouseButtonDown(1)) StartGuard();
        if (Input.GetMouseButtonUp(1)) EndGuard();

        if (Input.GetMouseButtonDown(0) && !isGuarding && !isDashing)
        {
            NormalAttack();
        }

        if (Input.GetKeyDown(KeyCode.Q) && !isGuarding && !isDashing && skillCooldownTimer <= 0)
        {
            UseSkill();
        }

        if (Input.GetKeyDown(KeyCode.Space) && currentJumps > 0 && !isGuarding)
        {
            Jump();
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashes > 0 && !isGuarding)
        {
            StartDash();
        }
    }

    private void Move()
    {
        currentMoveSpeed = isGuarding ? moveSpeed * 0.2f : moveSpeed;
        rb.linearVelocity = new Vector2(horizontalInput * currentMoveSpeed, rb.linearVelocity.y);

        // [요청 반영] SpriteRenderer의 flipX를 사용하는 좌우 반전 메커니즘
        if (horizontalInput > 0) sr.flipX = false;
        else if (horizontalInput < 0) sr.flipX = true;
    }
 
    private void UpdateAnimations()
    {
        if (anim == null) return;

        // 1. 달리기 애니메이션
        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        anim.SetBool("isRunning", isMoving);

        // 2. 점프/추락 애니메이션
        anim.SetBool("isGrounded", isGrounded);
    }

    private void NormalAttack()
    {
        Debug.Log("⚔️ 일반 공격!");
        if (anim != null) anim.SetTrigger("doAttack"); 

        if (attackPoint == null) return;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            DummyMonster monster = enemy.GetComponent<DummyMonster>();
            if (monster != null) monster.TakePlayerDamage(attackDamage);
        }
    }

    private void UseSkill()
    {
        Debug.Log("🔥 스킬 공격 (횡베기)!");
        skillCooldownTimer = skillCooldown; 
        
        if (anim != null) anim.SetTrigger("doAttack"); 

        if (attackPoint == null) return;
        Collider2D[] hitEnemies = Physics2D.OverlapBoxAll(attackPoint.position, skillBoxSize, 0f, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            DummyMonster monster = enemy.GetComponent<DummyMonster>();
            if (monster != null) monster.TakePlayerDamage(skillDamage);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); 
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        currentJumps--;
        isGrounded = false;
        
        if (anim != null) anim.SetTrigger("doJump"); 
    }

    private void StartDash()
    {
        isDashing = true;
        currentDashes--;
        dashTimer = dashDuration;

        // SpriteRenderer가 뒤집혔는지(왼쪽인지) 체크해서 대시 방향 결정
        dashDir = sr.flipX ? -1f : 1f;

        rb.gravityScale = 0; // 대시 중에는 중력 0으로 고정해서 아래로 쳐지지 않게 방지

        if (anim != null) anim.SetBool("isDashing", true);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 3; // 원래 쓰던 중력 수치로 복귀
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // 대시가 끝나는 순간 X축 급정거

        if (anim != null) anim.SetBool("isDashing", false); // 대시 애니메이션 끄기
    }

    private void CheckGrounded()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && rb.linearVelocity.y <= 0.1f) 
        {
            currentJumps = maxJumps;
            currentDashes = maxDashes; // 땅에 닿으면 대시 횟수 초기화
        }
    }

    private void StartGuard()
    {
        isGuarding = true;
        isParrying = true;
        parryTimer = parryWindow;
    }

    private void EndGuard()
    {
        isGuarding = false;
        isParrying = false;
    }

    public void TakeDamage(float damage, bool isMeleeAttacker, DummyMonster attacker = null)
    {
        if (isParrying)
        {
            if (isMeleeAttacker && attacker != null) attacker.ApplyGroggy(); 
            return; 
        }
        else if (isGuarding) damage *= 0.5f; 
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (attackPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(attackPoint.position, skillBoxSize);
        }
    }
}