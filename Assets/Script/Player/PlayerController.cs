using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Component References")]
    private Rigidbody2D rb;
    
    [Header("Movement")]
    public float moveSpeed = 5f;
    private float currentMoveSpeed; // 가드 등 이동속도 변화를 위해 사용
    private float horizontalInput;

    [Header("Jump")]
    public float jumpForce = 10f;
    public int maxJumps = 1; // 아이템 획득 시 이 값을 올려주면 다중 점프 가능
    private int currentJumps;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Dash")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public int maxDashes = 1; // 아이템 획득 시 수정 가능
    private int currentDashes;
    private bool isDashing;
    private float dashTimeLeft;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentMoveSpeed = moveSpeed;
    }

    void Update()
    {
        // 대시 중일 때는 다른 입력을 받지 않음
        if (isDashing) return;

        CheckInput();
        CheckGrounded();
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        Move();
    }

    private void CheckInput()
    {
        // 1. 좌우 이동 입력
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 2. 점프 입력
        if (Input.GetButtonDown("Jump") && currentJumps > 0)
        {
            Jump();
        }

        // 3. 대시 입력 (예: Left Shift 키)
        if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashes > 0)
        {
            StartDash();
        }
    }

    private void Move()
    {
        rb.linearVelocity = new Vector2(horizontalInput * currentMoveSpeed, rb.linearVelocity.y);
        
        // 캐릭터 방향 뒤집기
        if (horizontalInput > 0) transform.localScale = new Vector3(1, 1, 1);
        else if (horizontalInput < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    private void Jump()
    {
        // 다중 점프 시 y축 속도를 초기화하여 일정한 점프력을 보장
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); 
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        currentJumps--;
    }

    private void StartDash()
    {
        isDashing = true;
        currentDashes--;
        dashTimeLeft = dashDuration;
        
        // 대시 방향 설정 (현재 바라보고 있는 방향)
        float dashDirection = transform.localScale.x;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0); // 대시 중 y축 이동 방지

        // 대시 종료 타이머 실행
        Invoke("EndDash", dashDuration);
    }

    private void EndDash()
    {
        isDashing = false;
        // 대시 종료 시 속도 초기화
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private void CheckGrounded()
    {
        // 바닥 충돌 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded)
        {
            // 바닥에 닿으면 점프와 대시 횟수 초기화
            currentJumps = maxJumps;
            currentDashes = maxDashes;
        }
    }

    private void OnDrawGizmos()
    {
        // 에디터에서 GroundCheck 위치를 시각적으로 확인하기 위함
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}