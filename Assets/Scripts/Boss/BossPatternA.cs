using UnityEngine;
using System.Collections;

namespace BossPatterns {

      /// <summary>
      /// 🏀 Pattern A: 박치기 핑퐁 난무 (리듬 기반 돌진 패턴)
      /// 1 페이즈: 🔊청각 힌트 '띵' 후 0.8 초 대기 → 리듬 공격
      /// 2 페이즈: 사운드 제거, 시각적 웅크림 힌트 → 즉시 돌진
      /// </summary>
    public class BossPatternA : MonoBehaviour {

          [Header("=== 기본 설정 ===")]
        public Transform bossBody;
        public GameObject playerPrefab;
        private Vector3 lastAttackPosition;

          [Header("=== 패턴 A: 박치기 핑퐁 난무 ===")]
          [Tooltip("1 페이즈 청각 힌트 '띵' 후 대기 시간 (0.8 초)")]
        public float phase1HintDelay = 0.8f;

          [Tooltip("2 페이즈 시각적 웅크림 힌트 시간 (0.3 초)")]
        public float phase2SlowMoTime = 0.3f;

          [Tooltip("돌진 속도 (힘 '4', 땅 미끄러지듯 거칠게)")]
        public float thrustSpeed = 15f;

          [Tooltip("리듬 간격 (정박/엇박)")]
        public float rhythmQuarterNote = 0.5f; // 정박
        public float rhythmEighthNote = 0.25f;     // 엇박


          // --- INTERNAL STATE ---
        private bool phaseTwoMode = false;
        private bool isHiding = false;
        private PlayerInputManager playerInputManager = null;

          /// <summary>
          /// 🏀 Pattern A: 박치기 핑퐁 난무 - 리듬 루틴 실행!

          /// 1 페이즈: 🔊'띵' 사운드 → 0.8 초 대기 → [정박]-[정박]-[엇박 (0.4s×2)]-[정바 마무리]
          /// 2 페이즈: 사운드 힌트 제거 → 0.3 초 시각적 웅크림 → 즉시 리듬 돌진 (다시 시작)
          /// </summary>
        public void ExecutePatternARhythmRoutine() {

            Debug.Log("🏀 [Pattern A] 박치기 핑퐁 난무 시작! 🥊");

              // 초기화 (패턴 전환 시)
            ResetToIdle();

            if (phaseTwoMode) {
                Debug.Log("⚠️ 2 페이즈: 사운드 힌트 제거, 시각적 웅크림 후 즉시 돌진!");
             }

            StartCoroutine(RhythmThrustRoutine());
          }


          /// <summary>
          /// 🎵 리듬 루틴 기반 돌진 패턴 (1 페이즈 vs 2 페이즈)
          /// 정박: [정박] - [정박] - [엇박 0.4s×2] - [정바 마무리]
          /// </summary>
        private IEnumerator RhythmThrustRoutine() {

              // 🔊 사운드 힌트 (1 페이즈만!)
            if (!phaseTwoMode) {
                Debug.Log("🔊 [Pattern A] 청각 힌트: '띵!' - 0.8 초 대기!");

                  // 🎵 사운드 효과 재생
                PlaySoundEffect("pattern_a_ding.wav", 1f); // 🔊 띵

                yield return new WaitForSeconds(phase1HintDelay);

                Debug.Log("👀 [Pattern A] 시각적 웅크림 힌트 시작 (0.3 초)!");

                  // 👀 시각적 웅크림 힌트: 보스가 몸을 웅거며 공격 준비 (0.3 초)
                StartCoroutine(SlowMoPreAttackAnimation());

             } else {
                 // 2 페이즈: 시각적 웅크림 힌트만! (사운드 없앰!)
                Debug.Log("👀 [Pattern A] 2 페이즈: 시각적 웅크림 힌트만 (사운드 없이)!");

                  // 👀 시각적 웅크림 힌트 (0.3 초)
                StartCoroutine(SlowMoPreAttackAnimation());
             }

            yield return new WaitForSeconds(phase2SlowMoTime);

              // 💥 리듬 돌진 공격 시작!
            Debug.Log("🥊 [Pattern A] 리듬 돌진 공격!");

              // 🎵 리듬 루틴: [정박] - [정박] - [엇박 0.4s×2] - [정바 마무리]
            float[] rhythmSequence = {
                 1f,     // 정박 (1st)
                 1f,     // 정박 (2nd)
                 0.4f,  // 엇박 (3rd)
                 0.4f,  // 엇박 (4th)
                 1f      // 정바 (마무리!)
             };

            float cumulativeTime = 0f;

            for (int i = 0; i < rhythmSequence.Length; i++) {

                if (cumulativeTime >= rhythmSequence[i]) {
                     // 🔥 현재 박자 돌진! (보스는 뒤로 밀림, 플레이어는 힘 '4' 로 튕겨나감)
                    AttackPlayerWithPowerBoost(4f, thrustSpeed);

                    Debug.Log("💥 [Pattern A] 리듬 " + i + " 박자: 돌진 공격! 보스 넉백 발생!");

                      // 🔔 네트 작업 후 다음 박자로 (정박 0.5s / 엇박 0.4s)
                    float nextRhythm = i < rhythmSequence.Length - 1 ? rhythmSequence[i + 1] : cumulativeTime;
                    yield return new WaitForSeconds(nextRhythm);
                 }

                cumulativeTime += rhythmSequence[i];
             }

            Debug.Log("✅ [Pattern A] 리듬 돌진 완료! 플레이어는 가드 반동으로 밀려남!");
          }


          /// <summary>
          /// 🎬 시각적 웅크림 힌트 (패턴 전환 전 0.3 초)
          /// </summary>
        private IEnumerator SlowMoPreAttackAnimation() {

            Debug.Log("👀 [Pattern A] 시각적 웅크림 힌트! (보스 애니메이션 속도 느려짐)");

              // 보스가 몸을 웅거며 공격 준비 (0.3 초간 속도 감소)
            Vector3 originalVelocity = bossBody.velocity;

            float animationDuration = 0.3f;
            float currentTime = 0f;

            while (currentTime < animationDuration) {

                 // 애니메이션 진행도
                float progress = currentTime / animationDuration;

                  // 🎬 웅크림 모션: 속도 줄임! (보체만, 플레이어는 영향 없음!)
                bossBody.velocity *= Vector3.Lerp(1f, 0.7f, progress);

                Debug.Log($"👀 [Pattern A] Animation Slow Mo: " + progress * 100f + "%");

                yield return null;
                currentTime += Time.deltaTime;
             }

              // 웅크림 종료 - 원래 속도 복원 (하지만 이미 공격 준비 상태)
            bossBody.velocity = originalVelocity;
          }


          /// <summary>
          /// 💥 플레이어와 맞부딪기 (돌진攻击) - 힘 '4' 로 땅 미끄러지듯 거칠게 튕겨나감!
          /// 기본 원칙: 패링 성공 시 플레이어는 뒤로 '1'만큼만 밀리고, 보스는 3~4 배 먼 거리 (힘 '4') 로 거칠게 튕김!
          /// </summary>
        private void AttackPlayerWithPowerBoost(float powerMultiplier, float baseSpeed) {

            Debug.Log("💥 [Pattern A] 돌진 공격 (힘: " + (powerMultiplier * baseSpeed).ToString("F1") + ")");

              // 🎯 플레이어 위치와 충돌 확인
            Collider playerCollider = GetPlayerCollider();

            if (playerCollider != null) {

                Vector3 knockbackDir = Vector3.Project(player.transform.position - bossBody.position, Vector3.up).normalized;

                  // 💥 넉백 적용: 플레이어는 '1'만큼만, 보스는 3~4 배 먼 거리 로 거칠게!
                float playerKnockbackDist = 1f;
                float bossKnockbackDist = powerMultiplier * baseSpeed;

                Vector3 playerPush = knockbackDir * playerKnockbackDist;
                Vector3 bossRecoil = -knockbackDir * bossKnockbackDist;

                  // 플레이어 밀기 (패링 반동)
                Rigidbody rb = playerCollider.GetComponent<Rigidbody>();
                if (rb != null) {
                    rb.velocity = playerPush;
                    Debug.Log($"👤 [Pattern A] Player knocked back by " + playerPush.magnitude + " units!");
                  }

                  // 보스 넉백 적용 (땅 미끄러지듯 거칠게 튕겨나감!)
                Rigidbody bossRb = bossBody.GetComponent<Rigidbody>();
                if (bossRb != null) {
                    bossRb.velocity = Vector3.ClampMagnitude(bossRecoil, powerMultiplier * baseSpeed);
                    Debug.Log($"🦸 [Pattern A] Boss recoiled by " + bossRb.velocity.magnitude + " units!");
                  }

                  // 🔔 넉백 후 재진입 대기 (쿨타임 적용)
                Invoke("EnablePlayerCollision", 0.5f); // 넉백 쿨타임
               }
           }


          /// <summary>
          /// 🔊 사운드 효과 재생 (청각적 힌트)
          /// </summary>
        private void PlaySoundEffect(string soundName, float volume) {
            Debug.Log("🔊 [Pattern A] Sound: " + soundName + " | Volume: " + volume);

            AudioSource source = GetComponent<AudioSource>();
            if (source != null) {
                AudioClip clip = LoadAudioClip(soundName);
                if (clip != null) {
                    source.PlayOneShot(clip, volume);
                  } else {
                    Debug.LogWarning($"🔊 사운드 클립 로드 실패: {soundName}");
                  }
               } else {
                Debug.LogWarning("🔊 [Pattern A] AudioSource not found!");
                }
           }

        private AudioClip LoadAudioClip(string soundName) {
              // 오디오 클립 로딩 (Assets/Sounds/ 폴더 기준)
            return Resources.Load<AudioClip>("Sounds/" + soundName);
          }


          /// <summary>
          /// 💥 일반 돌진 공격 (기본 패턴) - 장전 → 돌진 → 재진입 대기
          /// 보스 본체에서 뒤로 살짝 웅거림 (장전), 플레이어 방향으로 빠르게 달려옴.
          /// </summary>
        public void ExecuteBasicThrustAttack() {

            Debug.Log("🔥 [Pattern Basic] 단순 돌진 공격 시작!");

              // 1️⃣ 장전: 보스 본체에서 뒤로 살짝 웅거림 (0.5 초)
            StartCoroutine(CrouchChargingAnimation());

            yield return new WaitForSeconds(0.5f);

              // 2️⃣ 돌진: 플레이어 방향으로 빠르게 달려오기 (힘 '3')
            AttackPlayerWithThrust(3f, player.transform.position - bossBody.position).normalized;

            Debug.Log("💥 [Pattern Basic] 돌진 공격 완료!");

              // 3️⃣ 재진입 대기 (돌진 후 브레이크 잡듯 밀려나며 멈춤)
            Invoke("SelfRecoil", 0.2f);
           }


          /// <summary>
          /// 🤸 보스 자체 넉백 (돌진 후 반대 방향으로 살짝 밀려남)
          /// </summary>
        private void SelfRecoil() {

            Debug.Log("🤸 [Pattern Basic] 보스 자체 넉백 (반대 방향)!");

            Vector3 recoilDir = transform.forward; // 돌진했던 방향의 반대

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = recoilDir * 2f; // 살짝 밀려남
               }

              // 🏁 재진입 준비
            Invoke("ResetToIdle", 0.3f);
           }


          /// <summary>
          /// 🎭 시각적 웅크림 애니메이션 (장전 모션) - 보스 본체만, 플레이어 영향 없음!
          /// </summary>
        private IEnumerator CrouchChargingAnimation() {

            Debug.Log("👀 [Pattern Basic] 장전 중... (보스가 몸을 웅거줌)!");

            Vector3 originalPos = bossBody.position;

              // 보스 몸 웅기기 (0.5 초간 천천히 하향 이동)
            float duration = 0.5f;
            int frames = (int)(duration / Time.fixedDeltaTime);

            for (int i = 0; i < frames; i++) {

                 // 점진적 웅크림 모션 (보스 위치만, 플레이어는 영향 없음!)
                Vector2 posChange = new Vector2(0, -0.5f * ((float)i / frames));
                bossBody.position += posChange;

                Debug.Log("👀 [Pattern Basic] Crouch charge: " + Mathf.Round(100f * ((float)i / frames)) + "%");

                  // 🎭 장전 애니메이션 완료 후 재진입 대기
                if (i >= frames - 1) {
                    yield break;
                  }

                yield return null;
             }

            Debug.Log("✅ [Pattern Basic] 장전 완료!");
           }


          /// <summary>
          /// 💥 플레이어 충돌 및 공격 로직 (기본 물리 적용!)
          /// - 일반 공격: 서로 경직되며 밀림 (보스 힘 '2', 플레이어 힘 '1.5')
          /// - 패링 성공 시 핑퐁 넉백: 플레이어 1, 보스 4 배 거름으로 거칠게 튕김!
          /// </summary>
        private void OnCollisionEnter(Collision other) {

            if (other.collider.CompareTag("Player")) {

                  // 🎮 충돌 발생 - 공격 유형 판별
                Debug.Log("💥 [Pattern A] Player collision detected!");

                  // 💥 보스 넉백 적용 (일반: 힘 2, 패링 시 힘 4)
                Vector3 pushDirection = other.transform.position - transform.position;
                float forceMagnitude = GetAttackForce(other);

                Rigidbody bossRb = GetComponent<Rigidbody>();
                if (bossRb != null) {
                    bossRb.AddExplosionForce(forceMagnitude * 10f, other.transform.position, 3f);
                     Debug.Log($"🔥 [Pattern A] Boss pushed player with force {forceMagnitude}!");

                         // 🔔 넉백 후 재진입 대기 (회복 시간)
                    Invoke("ResetToIdle", 0.5f);
                   }
               }
           }


          /// <summary>
          /// 💪 공격력 계산 (일반: 힘 '2', 패링 시: 힘 '4')
          /// </summary>
        private float GetAttackForce(Collision other) {

            bool isParrying = false;

              // 패링 판별 (플레이어 가드 동작)
            if (PlayerIsHoldingBlock(other.collider)) {
                isParrying = true;
                Debug.Log("🛡️ [Pattern A] Player is blocking (Parrying)!");
               }

              // 💥 힘 계산: 일반 '2' / 패링 시 '4 배 더 강하게!'
            return isParrying ? 4f : 2f;
          }


          /// <summary>
          /// 🎯 플레이어 가드 판별 (패링 상태 체크)
          /// </summary>
        private bool PlayerIsHoldingBlock(Collider playerCollider) {

              // 🔊 플레이어 입력 시스템에서 패링 플래그 확인
            Rigidbody rb = playerCollider.GetComponent<Rigidbody>();

            if (rb != null && PlayerInputManager.instance != null && PlayerInputManager.instance.blockingPressed) {
                Debug.Log("🛡️ [Pattern A] Player is blocking (Parrying)!");
                return true; // 패링 상태!
               }

            return false; // 일반 공격!
          }


          /// <summary>
          /// 🔔 넉백 후 재진입 대기 (쿨타임 적용)
          /// </summary>
        public void ResetToIdle() {

            Debug.Log("🎯 [Pattern A] 보스 재진입 준비 ( Idle 상태 복원)!");

              // 🎭 Idle 로직: 보스 본체 위치를 현재 위치로 리셋 (충돌 후 밀려난 자리)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = Vector3.zero; // 속도 0 으로!
               }

              // 🔔 공격 가능한 상태 ( Idle 상태로!)
            this.enabled = true; // 재진입 가능!
           }


          /// <summary>
          /// 🎮 입력 시스템 연결 (플레이어 충돌 및 가드 판별)
          /// </summary>
        private void Update() {

              // 플레이어 입력 매니저 초기화
            if (playerInputManager == null) {
                playerInputManager = FindObjectOfType<PlayerInputManager>();
             }

              // 🎮 플레이어 입력 확인 (충돌 판별용!)
            if (playerInputManager != null) {
                Debug.Log("🎮 [Pattern A] Player Input: " +
                       $"Move:{Vector2}(left,right), Block:{PlayerInputManager.instance.blockingPressed}, Attack:{PlayerInputManager.instance.attackPressed}");

                  // 🏀 패턴 판별 및 실행 (충돌 시!)
                if (collisionWithPlayer) {
                    ExecutePatternARhythmRoutine();
                   }
               }
          }


        private bool collisionWithPlayer = false;

      }
}
