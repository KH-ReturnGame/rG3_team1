using UnityEngine;
using System.Collections;

namespace BossPatterns {

     /// <summary>
     /// 🎵 Pattern B: 스텝 타이밍 발판 점프 (리듬 기반 체력 분할 점프)
     /// 핵심 기믹: 리듬에 맞춰 발판을 점프하고 체력이 분할되어 공격!
     /// 1 페이즈: 청각/시각 힌트 → 점프 타격 판정
     /// 2 페이즈: 체력 분할 (하이라이트) → 연속 점프 공격
     /// </summary>
    public class BossPatternB : MonoBehaviour {

          [Header("=== 기본 설정 ===")]
        public Transform bossBody;
        public GameObject playerPrefab;

          [Header("=== 패턴 B: 발판 점프 리듬 ===")]
          [Tooltip("1 페이즈 힌트 사운드 재생 (띵, 띵, 띵)")]
        public AudioClip[] jumpRhythmSounds; // [띵, 띵, 띵]

          [Tooltip("발판 객체들 - 점프 판정용 플랫폼")]
        public GameObject[] steppingPlatforms; // 6 개 발판 (0~5 번지수)

          [Tooltip("점프 간격 리듬 (0.4 초 × 3)]")]
        public float jumpInterval = 0.4f; // 리듬 간격

          [Tooltip("체력 분할 데미지 multiplier")]
        public float healthSplitMultiplier = 1.5f;

          [Header("=== 시각 효과 ===")]
          [Tooltip("점프 타격 판정 시 체력 분할 하이라이트 색상")]
        public Color[] splitDamageColors = new Color[] {
            Color.Red,     // 🔴 0 번지수 (최대치)
            Color.Orange,  // 🟠 1 번지수
            Color.Yellow,  // 🟡 2 번지수
            Color.Cyan,    // 🔵 3 번지수
            Color.Magenta, // 🟣 4 번지수
            Color.Green    // 🟢 5 번지수 (최소치)
         };

          [Header("=== 씬 설정 ===")]
        public GameObject impactPrefab;      // Impact 효과용 프리펙브
        public AudioClip[] sfxJumps = new AudioClip[6] { null, null, null, null, null, null }; // 점프 사운드

          // --- INTERNAL STATE ---
        private bool phaseTwoMode = false;
        private float currentRhythmTime = 0f;
        private int currentPlatformIndex = 0;
        private bool isPlayerJumping = false;
        private Vector3[] platformPositions;
        private bool[] platformActive = new bool[6];

          // --- HELPER FIELDS ---
        private Rigidbody2D rb;              // Physics component (Unity 2D)
        private AudioSource audioSource;     // Audio playback


         /// <summary>
         /// 🎵 Pattern B: 발판 점프 루틴 실행!

         /// 1 페이즈: 리듬 사운드 (띵, 띵, 띵) → 점프 판정 구간
         /// 2 페이즈: 체력 분할 → 하이라이트 공격 → 리듬 점프 연속
         /// </summary>
        public void ExecutePatternBJumpRoutine() {

            Debug.Log("🎵 [Pattern B] 발판 점프 루틴 시작!");

             // 초기화 (패턴 전환 시)
            ResetPlatformState();

            if (phaseTwoMode) {
                Debug.Log("⚠️ 2 페이즈: 체력 분할 하이라이트 후 연속 점프!");
              } else {
                Debug.Log("✅ 1 페이즈: 기본 리듬 점프 판정!");
                StartCoroutine(ExecutePhase1JumpSequence());
                return;
              }

            StartCoroutine(ExecutePhase2SplitAttack());
          }


        private void ResetPlatformState() {
            currentPlatformIndex = 0;
            currentRhythmTime = 0f;
            
            if (steppingPlatforms != null) {
                for (int i = 0; i < steppingPlatforms.Length; i++) {
                    steppingPlatforms[i].SetActive(false);
                  }
              }

             Debug.Log("🔄 [Pattern B] 플랫폼 초기화 완료!");
          }


         /// <summary>
         /// 1 페이즈 점프 판정 루틴 실행
         /// 리듬 기반 점프 타이밍에 맞춰 데미지!
         /// </summary>
        private IEnumerator ExecutePhase1JumpSequence() {

             // 🔊 사운드 힌트 재생 (띵, 띵, 띵)
            Debug.Log("🔊 [Pattern B] 리듬 사운드 시작!");

            if (jumpRhythmSounds != null && jumpRhythmSounds.Length >= 3) {
                PlaySoundEffect(jumpRhythmSounds[0], 1f); // 🔊띵
                yield return new WaitForSeconds(0.3f); yield;

                PlaySoundEffect(jumpRhythmSounds[1], 1f); // 🟣락
                yield return new WaitForSeconds(0.3f); yield;

                PlaySoundEffect(jumpRhythmSounds[2], 1f); // 🟢락
             }

             // 👀 시각적 힌트: 발판 활성화 (각자 점프 판정)
            Debug.Log("👀 [Pattern B] 발판 점프 판정 시작!");

            platformPositions = new Vector3[steppingPlatforms.Length];
            for (int i = 0; i < steppingPlatforms.Length; i++) {
                platformPositions[i] = steppingPlatforms[i].transform.position;
                // 각 발판을 순차적 활성화
                if (i == 0) steppingPlatforms[i].SetActive(true);
             }

             // 🎮 점프 판정 루틴 (리듬 × 3)
            float sequenceTime = 0f;
            int jumpCount = 0;

            for (int i = 0; i < 6 && sequenceTime < 2.5f; i++) {
                if (i % 2 == 0) { // 정박 타이밍 (0s, 0.8s, 1.6s...)
                     Debug.Log($"👟 [Pattern B] 점프 판정 " + (i + 1) + " 번! 플레이어 점프 대기!");

                    if (CheckPlayerJumpTiming(platformPositions[i], i)) {
                        ApplyJumpDamage(player.transform.position, i);
                        Invoke(nameof(FlashColorFeedback), 0.5f, Color.red); // 타격 효과!
                     } else {
                        Debug.Log("⚠️ [Pattern B] 점프 타이밍 놓침!");
                     }
                 } else {
                     // 엇박 (0.4s 간격) - 리듬 유지
                    yield return new WaitForSeconds(jumpInterval);
                 }

                sequenceTime += jumpInterval;
            }

            Debug.Log("✅ [Pattern B Phase 1] 점프 판정 완료!");
            phaseTwoMode = true; // 자동 2 페이즈 전환!

             // 재진입 대기 (0.5 초)
            yield return new WaitForSeconds(0.5f);

            StartCoroutine(ExecutePhase2SplitAttack()); // 2 페이즈로 자동 진입!
          }


         /// <summary>
         /// 2 페이즈: 체력 분할 공격 (연속 점프!)
         /// 각 지수마다 색상이 바뀜 (하이라이트 효과)
         /// </summary>
        private IEnumerator ExecutePhase2SplitAttack() {

             // 🎨 체력 분할 하이라이트 시작 (각색 표시)
            Debug.Log("🎨 [Pattern B Phase 2] 체력 분할 하이라이트 시작!");

            if (player != null && splitDamageColors.Length == 6) {
                for (int i = 0; i < splitDamageColors.Length; i++) {
                    FlashColorFeedback(player.transform.position, splitDamageColors[i]);
                    Debug.Log($"🎨 [Pattern B] 차원 " + (i + 1) + "색상: " + SplitDamageColorDescription(i));
                    yield return new WaitForSeconds(0.4f); // 색상 변경 간격
                 }
             }

             // 💥 체력 분할 연속 점프 공격!
            Debug.Log("💥 [Pattern B Phase 2] 체력 분할 연속 공격!");

            currentPlatformIndex = 0;
            float splitSequenceTime = 0f;

            for (int i = 0; i < 6 && splitSequenceTime < 3.0f; i++) {
                if (i % 2 == 0) { // 정박 타격 판정
                     Debug.log($"⚡ [Pattern B Phase 2] 차원 공격 " + (i + 1));

                    if (CheckPlayerJumpTiming(platformPositions[currentPlatformIndex], currentPlatformIndex, true)) {
                        ApplySplitDamage(player.transform.position, currentPlatformIndex, splitDamageColors[currentPlatformIndex]);
                        Invoke(nameof(FlashColorFeedback), 0.5f, splitDamageColors[currentPlatformIndex]);
                     }
                 } else {
                    yield return new WaitForSeconds(jumpInterval);
                 }

                splitSequenceTime += jumpInterval;
                currentPlatformIndex++;
             }

            Debug.Log("✅ [Pattern B] 모든 패턴 완료! 체력 분할 공격 종료!");

              // 재진입 대기
            Invoke(nameof(ResetToIdle), 1.0f);
            yield break;
          }


         /// <summary>
         /// 점프 타이밍 판별 (리듬 기반)
         /// 플레이어 위치와 발판 위치를 비교하여 점프 타격 판정!
         /// </summary>
        private bool CheckPlayerJumpTiming(Vector3 platformPos, int platformIndex, bool isPhase2 = false) {

            if (player == null || !isInputTriggered()) return false;

             // 플레이어 - 발판 거리 체크 (점프 범위 내)
            float distToPlatform = Vector3.Distance(player.transform.position, platformPos);

            // 점프 판정 거리 (예: 5 단위 이내)
            if (distToPlatform > 5f) {
                Debug.Log($"⚠️ [Pattern B]太远: 플레이어 - 발판 거리 {distToPlatform:F1}");
                return false;
              }

             // 점프 동작 감지 (플레이어 가드 상태 해제 + 점프 입력)
            if (!PlayerInputManager.instance.blockingPressed && PlayerInputManager.instance.isJumpingPressed) {
                Debug.Log("👟 [Pattern B] 점프 동작 감지! 타격 판정!");

                 // 추가 체크: 플레이어 수직 속도 (점프 상승 중인지?)
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb != null) {
                    Vector2 velocity = playerRb.velocity;
                    if (velocity.y > 0.5f && velocity.y < 10f) {
                        return true; // 점프 공격 타격!
                       }
                   }

                  return false; // 타이밍 아님!
                 }

            return false; // 타이밍 오름
           }


         /// <summary>
         /// 점프 타격 데미지 적용 (패턴 1 번지수)
         /// </summary>
        private void ApplyJumpDamage(Vector3 hitPos, int platformIndex) {

             // 체력 분할 값 계산 (0=최대치, 5=최소치)
            float damage = GetHealthSplitValue(platformIndex);

             Debug.log($"💥 [Pattern B] 점프 데미지! " + hitPos + " - 데미지: " + damage.ToString("F1"));

             // 플레이어 데미지 적용
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) {
                player.TakeDamage(damage, false, this, hitPos); // 패링=false (일반 데미지)
                }
         }


         /// <summary>
         /// 체력 분할 데미지 계산 (각 지수별 다름!)
         /// 0: 최대치, 1-4: 중간치, 5: 최소치 (체력의 20%)
         /// </summary>
        private float GetHealthSplitValue(int platformIndex) {

             // 체력 분할 값 계산 (0=최대, 5=최소)
            float[] splitValues = {1.0f, 0.75f, 0.5f, 0.35f, 0.2f, 0.15f};

            if (platformIndex >= 0 && platformIndex < splitValues.Length) {
                return splitValues[platformIndex] * maxHealth; // 보스 최대 체력에 곱해서 데미지!
               }

            return 0f; // 안전영역!
           }


         /// <summary>
         /// 차원 색상 설명 (UI/디버깅용)
         /// </summary>
        private string SplitDamageColorDescription(int colorIndex) {

             switch (colorIndex) {
                 case 0: return "🔴 Maximum Damage";
                 case 1: return "🟠 High Damage";
                 case 2: return "🟡 Medium-High";
                 case 3: return "🔵 Medium-Low";
                 case 4: return "🟣 Low-Medium";
                 default: return "🟢 Minimum Damage";
             }

            // 반환 타입이 void 인데 string 을 리턴! 수정!
        }


         /// <summary>
         /// 색상 피드백 효과 (플레이어 캐릭터색 변경)
         /// </summary>
        private void FlashColorFeedback(Vector3 hitPos, Color targetColor) {

             Debug.Log("🎨 [Pattern B] Color Feedback: " + hitPos);

             PlayerController player = FindObjectOfType<PlayerController>();
             if (player != null && player.GetComponent<Renderer>() != null) {
                 float flashDuration = 0.5f;
                 GameObject originalColor = player.gameObject;

                 // 색상 변경 애니메이션 (유니티 코루틴이 더 깔끔할까?)
                StartCoroutine(ColorFlashAnimation(player, targetColor, flashDuration));
               }
           }


         private IEnumerator ColorFlashAnimation(GameObject targetObj, Color targetColor, float duration) {

             Render rend = targetObj.GetComponent<Renderer>();

             if (rend == null) yield break;

              // 원래 색상 저장
            Color originalColor = rend.material.color.Copy();

            yield return new WaitForSeconds(0.1f);

             // 타격 효과!
            rend.material.color = targetColor;

             // 재원복구 (타격 지속 시간 후 원래 색으로)
            yield return new WaitForSeconds(duration);
            rend.material.color = originalColor;

             // Visual Feedback: Damage number or particles? (추가 구현 필요!)
           }


          /// <summary>
           /// 분할 데미지 적용 (2 페이즈 전용)
           /// </summary>
        private void ApplySplitDamage(Vector3 hitPos, int damageIndex, Color damageColor) {

             // 체력 분할 데미지 계산
            float damage = GetHealthSplitValue(damageIndex);

            Debug.Log($"💥 [Pattern B] Split Damage: " + damage.ToString("F1") + ", 색상: " + damageColor);

              // 플레이어 데미지 적용 (2 페이즈 전용)
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) {
                player.TakeDamage(damage, false, this, hitPos);
                 }
         }


          /// <summary>
          /// 재진입 대기 (충돌 후 회복)
          /// </summary>
        public void ResetToIdle() {

            Debug.Log("🎯 [Pattern B] 보스 재진입 준비 (Idle 상태 복원)!");

              // 초기화 (컴포넌트 타입에 따라 Rigidbody/Rigidbody2D 차이 있음!)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.zero;
            Rigidbody2D rb2 = GetComponent<Rigidbody2D>();
            if (rb2 != null) rb2.linearVelocity = Vector2.zero;

              // 상태 복원
            this.enabled = true; // 재진입 가능!
             }


          /// <summary>
          /// 🎮 입력 시스템 연결 및 패턴 실행
          /// </summary>
        private void OnPatternTriggered() {

             // 초기화 (패턴 전환 시)
            ResetPlatformState();

            if (phaseTwoMode) {
                Debug.Log("[Pattern B] 2 페이즈 모드에서 체력 분할 공격!");
                StartCoroutine(ExecutePhase2SplitAttack());
              } else {
                Debug.log("[Pattern B] 1 페이즈에서 기본 점프 판정!");
                StartCoroutine(ExecutePhase1JumpSequence());
              }
           }

        private bool isInputTriggered() {
             // 패턴 전환 체크 (충돌 등)
            return true; // 현재 상태가 패턴 1 인지 확인!
           }

    }
}
