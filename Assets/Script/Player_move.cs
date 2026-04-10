using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float dashCost = 20f;

    [Header("Ground")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundRadius = 0.2f;

    private Rigidbody2D rb;

    private float moveInput;
    private bool isGrounded;

    private bool jumpPressed;
    private bool dashPressed;

    private bool isDashing;
    private bool canDash = true;

    private float stamina = 100f;

    // 바라보는 방향: 오른쪽 1, 왼쪽 -1
    private int facingDirection = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashPressed = true;

        CheckGround();
        Flip();
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
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    void Jump()
    {
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        jumpPressed = false;
    }

    void TryDash()
    {
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
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
    }
}