using UnityEngine;

namespace BossPatterns {
    
    /// <summary>
    /// 🎮 플레이어 입력管理系统 (싱글톤 패턴)
    /// 모든 보스 패턴에서 공유하는 입력 플래그들을 관리
    /// </summary>
    public class PlayerInputManager : MonoBehaviour
    {
        // Singleton 인스턴스
        public static PlayerInputManager instance;

        [Header("=== 이동 ===")]
        [Range(0f, 1f)] public float moveSpeed = 1f;           // 플레이어 이동 속도 (기본값: 8)
        public Vector2 moveVector = new Vector2(0, 0);         // 현재 입력 벡터

        [Header("=== 동작 ===")]
        public bool jumpPressed = false;                       // 점프 입력 플래그
        public bool attackPressed = false;                     // 공격 입력 플래그
        public bool blockPressed = false;                      // 가드/패링 입력 플래그

        private void Awake()
        {
            // Singleton 생성 (다중 인스턴스 방지)
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);  // 씬 변경 시에도 유지
                Debug.Log("[PlayerInputManager] Singleton initialized!");
            }
            else
            {
                Destroy(gameObject);  // 이미 생성된 인스턴스면 제거
            }

            // 입력 시스템 초기화
            moveVector = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }

        private void Update()
        {
            // 현재 입력 상태 업데이트
            moveVector = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).normalized;

            jumpPressed = Input.GetButtonDown("Jump");  // 점프는 버퍼링 X (즉시 처리)
            
            // 공격/가드: 한 번만 트리거되도록 Normalize
            if (Input.GetButtonDown("Attack")) attackPressed = true;
            if (Input.GetButtonDown("Block")) blockPressed = true;
            
            // 입력이 없으면 false 로 리셋
            if (!Input.GetButton("Jump")) jumpPressed = false;
        }

        /// <summary>
        /// 이동 속도 반환 (패턴별 설정을 위해)
        /// </summary>
        public float GetMoveSpeed() => moveSpeed;

        /// <summary>
        /// 점프 입력이 된 상태인지 (즉시 처리용)
        /// </summary>
        public bool IsJumpingInputTriggered() => jumpPressed;

        /// <summary>
        /// 공격 입력이 된 상태인지 (즉시 처리용)
        /// </summary>
        public bool IsAttackInputTriggered() => attackPressed;

        /// <summary>
        /// 가드/패링 입력이 된 상태인지 (즉시 처리용)
        /// </summary>
        public bool IsParryInputTriggered() => blockPressed;

        /// <summary>
        /// 현재 이동 벡터 반환
        /// </summary>
        public Vector2 GetMoveVector() => moveVector.normalized;

        /// <summary>
        /// 디버깅을 위한 로그 출력 (디버그 모드 활성화 시)
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            GUIStyle style = new GUIStyle()
            {
                fontSize = 20,
                normal = { textColor = Color.white },
                richText = true
            };

            string info = $"[PlayerInput]\n" +
                         $"Move: ({moveVector.x:F2}, {moveVector.y:F2})\n" +
                         $"Jump: {(jumpPressed ? "✓" : "✗")}\n" +
                         $"Attack: {(attackPressed ? "✓" : "✗")}\n" +
                         $"Block: {(blockPressed ? "✓" : "✗")}";

            GUI.Label(new Rect(10, 10, 300, 150), info, style);
        }
#endif
    }
}
