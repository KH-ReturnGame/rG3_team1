using UnityEngine;

namespace BossPatterns {

      /// <summary>
       /// 🌟 클론 분신 (마인크래프트 스타일)
       /// 패턴 D 와 C 에서 사용하는 홀로그램 클론 객체들
       /// </summary>
    public class CloneProjectile : MonoBehaviour
      {

          [Header("=== 기본 설정 ===")]
        public float lifeTime = 5f;                             // 클존 수명 (초)
        public Rigidbody2D rb;                                 // 물리 컴포넌트 자동 생성

            [Header("=== 시각 효과 ===")]
        public new Renderer renderer;                            // 렌더러 자동 생성
        public Color cloneColor = new Color(0f, 1f, 1f);      // 기본 색상 (시안)
        
         private float spawnTime = 0f;                           // 스폰 타임 (수명 계산용)
        
            [Header("=== 공격 설정 ===")]
        public float damage = 5f;                              // 데미지 값
        public int cloneIndex = 0;                             // 클론 인덱스 (0-6 번)

        private void Awake()
           {
               // 자동 컴포넌트 생성
            rb = GetComponent<Rigidbody2D>();
            renderer = GetComponent<Renderer>();

               // 수명 제한용 콜백 등록
            if (rb != null) {
                rb.freezeRotation = true;     // 회전 방지
                rb.gravityScale = 0f;         // 중력 무시 (비행체!)
                 }
           }

        private void Start()
           {
             if (renderer != null) {
                 renderer.material.color = cloneColor;
                  }
             
             Debug.Log($"[CloneProjectile #{cloneIndex}] 생성됨! 수명: {lifeTime}s, 색: {cloneColor}");
           }

        private void Update()
           {
                // 수명이 다하면 자동 제거 (패턴별 로직이 있다면 수정 필요)
            if (renderer != null)
               {
                if (Time.time > spawnTime + lifeTime) {
                     Destroy(gameObject);
                      }
                  }
              }

        private void OnCollisionEnter2D(Collision2D other)
           {
               // 플레이어 충돌 시 데미지 적용
            if (other.gameObject.CompareTag("Player"))
               {
                ApplyDamageToPlayer(other.transform.position, damage);
                   // 충돌 후 제거 또는 계속 비행? 패턴 로직에 따라 수정!
                Destroy(gameObject, 1f);
                 }

                // 다른 객체 충돌 시에도 제거할 수 있음
            if (other.gameObject.CompareTag("Ground")) {
                Destroy(gameObject);
                 }
             }

          /// <summary>
          /// 플레이어에게 데미지 적용 (IDamageable 인터페이스 확인)
          /// </summary>
        private void ApplyDamageToPlayer(Vector3 hitPos, float damageValue)
           {
             PlayerController player = FindObjectOfType<PlayerController>();
             if (player != null) {
                 player.TakeDamage(damageValue, false, this, hitPos); // 패링=false (일반 데미지)
                  }

              Debug.Log($"💥 [클론 #{cloneIndex}] 플레이어 데미지! " + damageValue.ToString("F1"));
             }

        private void OnDestroy()
           {
             Debug.Log($"🗑️ [CloneProjectile #{cloneIndex}] 소멸됨");
           }

          /// <summary>
          /// 수명 시간 설정 (패턴별로 다르게)
          /// </summary>
        public void SetLifeTime(float seconds)
           {
            lifeTime = seconds;
            
               // 기존 스폰시간 재설정 (필요시)
            if (renderer != null && spawnTime >= 0f) {
                renderer.material.color = UnityEngine.Color.Lerp(spawnColor, cloneColor, Mathf.Clamp01((Time.time - spawnTime) / lifeTime));
                 }
           }

          /// <summary>
          /// 즉시 비활성화 (패턴 전환 시 클론 숨김용)
          /// </summary>
        public void SetInactive(bool isActive)
           {
            if (gameObject != null) gameObject.SetActive(isActive);
           }

      private Color spawnColor = new Color(0f, 1f, 1f, 0.3f); // 시안색 (반투명 홀로그램)
     }
}
