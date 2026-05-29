using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    
    
    [Header("Component References")]
    private Rigidbody2D rb;
    private Animator anim; // [추가] 애니메이터를 부르기 위한 변수
    private SpriteRenderer sr;
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    private float currentMoveSpeed; 
    private float horizontalInput;

    [Header("Jump")]
    public float jumpForce = 10f;
    public int maxJumps = 1; 
    private int currentJumps;
    public RuleTile.TilingRuleOutput.Transform groundCheck;
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

    [Header("Guard & Parry")]
    public float parryWindow = 0.5f; 
    private float parryTimer;
    private bool isGuarding;
    private bool isParrying;

    [Header("Combat - Attack")]
    public RuleTile.TilingRuleOutput.Transform attackPoint;      
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
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>(); // [추가] 시작할 때 플레이어의 애니메이터를 찾아서 연결!
    }

    void Start()
    {
        currentMoveSpeed = moveSpeed;
        currentJumps = maxJumps;
        currentDashes = maxDashes;
    }

    void Update()
    {
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

        // [추가] 애니메이션 파라미터 실시간 업데이트
        UpdateAnimations(); 
    }

    void FixedUpdate()
    {
        if (isDashing) return;
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

        if (horizontalInput > 0) sr.flipX = false;
        else if (horizontalInput < 0) sr.flipX = true;
    }
 
    // [추가된 부분] 애니메이션 상태를 관리하는 함수
    private void UpdateAnimations()
    {
        if (anim == null) return;

        // 1. 달리기 애니메이션 (좌우 입력값이 조금이라도 있으면 true)
        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        anim.SetBool("isRunning", isMoving);

        // 2. 점프/추락 애니메이션 (땅에 닿아있는지 여부 전달)
        anim.SetBool("isGrounded", isGrounded);
        
    }

    private void NormalAttack()
    {
        Debug.Log("⚔️ 일반 공격!");
        if (anim != null) anim.SetTrigger("doAttack"); // [추가] 공격 애니메이션 실행 빵!

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
        
        // 스킬 전용 애니메이션이 있다면 여기에 넣으면 돼! (지금은 일반 공격 모션 임시 사용)
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
        
        if (anim != null) anim.SetTrigger("doJump"); // [추가] 점프 애니메이션 실행
    }

    private void StartDash()
    {
        isDashing = true;
        currentDashes--;
        dashTimer = dashDuration;
        rb.linearVelocity = Vector2.zero;
        float dashDirection = sr.flipX ? -1 : 1;
        rb.AddForce(transform.right * dashDirection * dashSpeed, ForceMode2D.Impulse);
        rb.gravityScale = 0;
        if (anim != null) anim.SetBool("isDashing", isDashing);
    
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 3; 
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private void CheckGrounded()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && rb.linearVelocity.y <= 0.1f) 
        {
            currentJumps = maxJumps;
            currentDashes = maxDashes;
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