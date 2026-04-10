using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float guardMoveSpeed = 2.5f;
    public float jumpForce = 12f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float dashCost = 20f;

    [Header("Guard")]
    public float guardCostPerSecond = 5f;
    public float parryWindow = 0.5f;
    public float damageReductionRate = 0.5f; // 50% 감소
    public float parryStaminaRecover = 30f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundRadius = 0.2f;

    [Header("Status")]
    public float maxStamina = 100f;
    public float stamina = 100f;
    public int maxHP = 100;
    public int currentHP = 100;

    private Rigidbody2D rb;

    private float moveInput;
    private bool isGrounded;
    private bool jumpPressed;
    private bool dashPressed;

    private bool isDashing;
    private bool canDash = true;

    private bool isGuarding;
    private bool isParryWindow;
    private int facingDirection = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        stamina = maxStamina;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashPressed = true;

        if (Input.GetMouseButtonDown(1))
            StartGuard();

        if (Input.GetMouseButtonUp(1))
            EndGuard();

        CheckGround();
        Flip();
        UpdateGuardDrain();
    }

    void FixedUpdate()
    {
        if (!isDashing)
        {
            Move();
            Jump();
        }

        TryDash();
    }

    void Move()
    {
        float currentSpeed = isGuarding ? guardMoveSpeed : moveSpeed;
        rb.linearVelocity = new Vector2(moveInput * currentSpeed, rb.linearVelocity.y);
    }

    void Jump()
    {
        if (jumpPressed && isGrounded && !isGuarding)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        jumpPressed = false;
    }

    void TryDash()
    {
        if (isGuarding) return;

        if (dashPressed && canDash && stamina >= dashCost)
        {
            dashPressed = false;
            StartCoroutine(Dash());
        }
    }

    IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;

        stamina -= dashCost;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(facingDirection * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void StartGuard()
    {
        if (isDashing) return;
        if (stamina <= 0f) return;
        if (isGuarding) return;

        isGuarding = true;
        StartCoroutine(ParryWindowCoroutine());
    }

    void EndGuard()
    {
        isGuarding = false;
        isParryWindow = false;
    }

    IEnumerator ParryWindowCoroutine()
    {
        isParryWindow = true;
        yield return new WaitForSeconds(parryWindow);
        isParryWindow = false;
    }

    void UpdateGuardDrain()
    {
        if (!isGuarding) return;

        stamina -= guardCostPerSecond * Time.deltaTime;

        if (stamina <= 0f)
        {
            stamina = 0f;
            EndGuard();
        }
    }

    void Flip()
    {
        if (moveInput > 0)
        {
            facingDirection = 1;
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveInput < 0)
        {
            facingDirection = -1;
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    void CheckGround()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
    }

    public void TakeDamage(int damage)
    {
        if (isDashing)
        {
            Debug.Log("Dash invincible");
            return;
        }

        if (isGuarding)
        {
            if (isParryWindow)
            {
                Debug.Log("Parry success!");
                RecoverStamina(parryStaminaRecover);
                return;
            }
            else
            {
                int reducedDamage = Mathf.CeilToInt(damage * damageReductionRate);
                currentHP -= reducedDamage;
                Debug.Log($"Guarded! Damage Reduced: {reducedDamage}, Current HP: {currentHP}");
                return;
            }
        }

        currentHP -= damage;
        Debug.Log($"Hit! Damage: {damage}, Current HP: {currentHP}");
    }

    public void RecoverStamina(float amount)
    {
        stamina += amount;
        if (stamina > maxStamina)
            stamina = maxStamina;
    }

    public bool IsGuarding()
    {
        return isGuarding;
    }

    public bool IsParryWindow()
    {
        return isParryWindow;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}