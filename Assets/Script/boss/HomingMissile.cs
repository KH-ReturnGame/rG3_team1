using UnityEngine;

public class HomingMissile : MonoBehaviour
{
    [Header("타겟 설정")]
    public Transform target;
    public string targetTag = "Player"; // 타겟이 없을 경우 자동으로 찾을 태그

    [Header("미사일 속성")]
    public float speed = 12f;         // 미사일 이동 속도
    public float rotateSpeed = 250f;  // 미사일 회전 속도(유도성능)
    public float lifeTime = 5f;       // 미사일 자연 소멸 시간

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // 미사일이 아래로 떨어지지 않게 중력 끄기

        // 혹시나 타겟이 지정되지 않았다면 태그로 플레이어를 자동 검색
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(targetTag);
            if (playerObj != null) target = playerObj.transform;
        }

        // 5초 뒤에 자동으로 미사일 삭제 (맵 밖에 무한히 나가는 것 방지)
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        // 타겟이 없으면 그냥 직진합니다.
        if (target == null)
        {
            rb.velocity = transform.up * speed;
            return;
        }
        
        Vector2 direction = (Vector2)target.position - rb.position;
        direction.Normalize(); // 방향만 남기기 위해 크기를 1로 조절

        // 2. 외적(Cross Product)을 이용해 현재 미사일의 앞방향(transform.up)과 타겟 방향 사이의 회전각 계산
        float rotateAmount = Vector3.Cross(transform.up, direction).z;

        // 3. 회전 속도(angularVelocity)를 부여해 타겟 쪽으로 꺾기
        rb.angularVelocity = -rotateAmount * rotateSpeed;

        // --- 이동 로직 ---
        // 4. 미asile이 바라보는 앞방향(transform.up)으로 속도 고정
        rb.velocity = transform.up * speed;
    }

    // 플레이어나 벽에 부딪혔을 때 처리
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 태그와 부딪혔는지 확인
        if (other.CompareTag(targetTag))
        {
            Debug.Log("미사일 맞음");
            
            //데미지 입히기
            Explode();
        }
        // 벽이나 장애물에 부딪혀도 터지게 하고 싶다면 추가
        else if (other.CompareTag("Obstacle")) 
        {
            Explode();
        }
    }

    void Explode()
    {
        ////////////// 폭발 이펙트, 사운드 넣기
        Destroy(gameObject); // 미사일 파괴
    }
}