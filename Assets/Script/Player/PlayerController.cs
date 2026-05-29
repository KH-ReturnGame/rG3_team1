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
    private float dashDir;

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
        // 🚨 [좌표 상승 버그 추적용 실시간 로그]
        // 아무 키도 안 눌렀을 때 캐릭터의 물리 상태를 콘솔창에 찍어줍니다.
        if (horizontalInput == 0 && !isDashing)
        {
            Debug.Log($"[버그추적] Y좌표: {transform.position.y:F2} | X속도: {rb.linearVelocity.x:F2} | Y속도: {rb.linearVelocity.y:F2} | 중력수치: {rb.gravityScale}");
        }

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

        
        
        if (horizontalInput > 0) sr.flipX = false;
        else if (horizontalInput < 0) sr.flipX = true;
    }
 
    private void UpdateAnimations()
    {
        if (anim == null) return;
        bool isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        anim.SetBool("isRunning", isMoving);
        anim.SetBool("isGrounded", isGrounded);
    }

    private void NormalAttack()
    {
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
        dashDir = sr.flipX ? -1f : 1f;
        rb.gravityScale = 0; 
        if (anim != null) anim.SetBool("isDashing", true);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = 3; 
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); 
        if (anim != null) anim.SetBool("isDashing", false); 
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