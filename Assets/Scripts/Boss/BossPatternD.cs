using UnityEngine;
using System.Collections;

namespace BossPatterns {

     /// <summary>
     /// 🌟 Pattern D: 바이너리 사운드 스윕 (마인크래프트 로컬 좌표 기준)
     /// 핵심 기믹: 분신을 `^ ^ ^1`, `^ ^ ^2` 처럼 보스 시선 방향 기준으로 전방으로 펼치며 회전 공격
     /// 5 번째 칸만 판정 비어있는 '똥' (낮은 피치)
     /// </summary>
    public class BossPatternD : MonoBehaviour {

          [Header("=== BASIC CONFIG ===")]
          [Tooltip("보스 본체 - 회전 중심점 (오차 적용됨)")]
        public Transform bossBodyCenter;

          [Tooltip("분신들 - 로컬 좌표 기준으로 전방에 펼쳐지는 '투사체' 형태")]
        public GameObject[] cloneProjectiles; // 7 개 분신 (0~6 번지수, 5 번째 비어있음)

          [Header("=== 패턴 D 설정 ===")]
          [Tooltip("클론 사출 속도 (초당 단위)")]
        public float cloneEjectionSpeed = 30f;

          [Tooltip("회전 속도 (1 페이즈: EaseInExpo, 2 페이즈: Linear)")]
        public float rotationSpeedBase = 180f; // deg/second

          [Tooltip("공격 범위 (월드 좌표 기준)")]
        public Vector3 attackRangeRadius = new Vector3(6f, 5f, 6f);

          [Header("=== 오차 메커니즘 설정 ===")]
          [Tooltip("위치 선점 오차 (-1.5 ~ 1.5 단위)")]
        public float positionErrorMin = -1.5f;
        public float positionErrorMax = 1.5f;

          [Header("=== 클론 생성 로직 ===")]
          [Tooltip("보스 시선 방향 (로컬 forward)")]
        public Vector3 bossLocalForward; // (0, 1, 0) or custom

          [Header("=== 판정 설정 ===")]
          [Tooltip("똥 (5 번지수) 판정 거리 - 비어있음 (0)")]
        public float emptyCloneDist = 0f;

          [Header("=== VISUAL FEEDBACK ===")]
          [Tooltip("펼치기 애니메이션 지속 시간")]
        public float unfoldDuration = 2.5f; // seconds

        private bool phaseTwoMode = false;
        private int currentPhase = 0; // 0 = Phase 1 (EaseInExpo), 1 = Phase 2 (Linear)

          // --- INTERNAL LOGIC ONLY ---

        private float rotationAccumulator = 0f;
        private Vector3 positionError;
        private bool hasDeployedClones = false;
        private Transform[] cloneTransforms;


        public enum PatternDState {
            WAITING,               // 청각 힌트 준비 (🔊띵, 띵, 띵, 띵)
            DEPLOYING_CLONES,      // 🔊똥(0), 🔵, 🔴, 🟢, ⚫ 순서로 7 칸 펼치기
            ROTATING_SWEEP,        // 분신 펼치고 회전 스윕 (EaseInExpo → Linear)
            ENDING_SPIN            // 회전이 끝나고 재진입 대기
          }


        public void InitializeStateMachine() {
              // [WAITING] -> [DEPLOYING_CLONES] (🔊띵, 띵, 띵, 띵)
             Debug.Log("[Pattern D] State Transition: WAITING → DEPLOYING_CLONES");
              // [DEPLOYING_CLONES] -> [ROTATING_SWEEP] (🔊똥(0), 🔵, 🔴, 🟢, ⚫ - 5 번칸 비어있음)
             Debug.Log("[Pattern D] State Transition: DEPLOYING_CLONES → ROTATING_SWEEP");
              // [ROTATING_SWEEP] -> [ENDING_SPIN] (회전 완료, 3 회 도면 완전 클린)
             Debug.Log("[Pattern D] State Transition: ROTATING_SWEEP → IDLE");
        }


          /// <summary>
          /// 🎵 청각 힌트 준비 (🔊띵, 띵, 띵, 띵) - 패턴 시작 전 사운드 효과
          /// </summary>
        public void OnPrepareHintsToDeployClones() {
            Debug.Log("🔊 [Pattern D] 사운드 힌트 시작! 🔊띵, 띵, 띵, 띵");

              // 🎵 사운드 시퀀스 (4 박자)
            PlaySoundEffect("ding_long_1.wav", 0f);     // 🔊띵 (장음)
            yield return new WaitForSeconds(0.3f);
            PlaySoundEffect("ding_med_2.wav", 0f);     // 🔵 (중음)
            yield return new WaitForSeconds(0.3f);
            PlaySoundEffect("ding_med_3.wav", 0f);     // 🟣 (중음)
            yield return new WaitForSeconds(0.4f);
            PlaySoundEffect("ding_short_5.wav", 0f);   // ⚫ (단음)

              // 👀 시각적 웅크림: 보스 본체가 살짝 웅거며 분신 사출 준비 모션 시작
            StartCoroutine(DeployClonesWithEjectMotion());
           }


          /// <summary>
          /// 🚀 클론 사출 및 위치 선점 (오차 매커니즘 적용)
          /// 마인크래프트 `tp ^ ^ ^1` 방식: 보스 시선 방향 기준 로컬 좌표로 분신 사출
          /// </summary>
        private IEnumerator DeployClonesWithEjectMotion() {

              // 1️⃣ 위치 선점 (오차 매커니즘) - Random.Range(-1.5, 1.5) 불완전 정렬
            float xError = positionErrorMin + UnityEngine.Random.Range(0f, positionErrorMax - positionErrorMin);
            float zError = positionErrorMin + UnityEngine.Random.Range(0f, positionErrorMax - positionErrorMin);

              // 보스 현재 위치 + 오차 (완전히 정확한 자리에 안 서도록!)
            Vector3 targetPosition = bossBodyCenter.position;
              // 보스 시선 방향을 기준으로 Y 축 위에서만 이동 (2D 게임 가정)
            Vector2 bossDirection = bossBodyCenter.transform.forward; // (0, 1) if facing forward
            targetPosition.x += bossDirection.x * xError;
            targetPosition.z += bossDirection.z * zError;

            Debug.Log($"📍 [Pattern D] Position Error Applied: X={xError:F2}, Z={zError:F2}");
            positionError = new Vector3(targetPosition.x, 0, targetPosition.z);

              // 2️⃣ 클론 분신 펼치기 모션 (보스 정면 축 기준 `^ ^ ^1`, `^ ^ ^2`, ... 로 사출)
            if (cloneProjectiles != null && cloneProjectiles.Length >= 7) {
                CloneSpawnData[] spawnData = GenerateCloneSpawnSequence(bossBodyCenter.position, bossDirection);

                  // 🎬 펼치기 애니메이션: 첫 칸부터 7 번째까지 순차적 사출
                for (int i = 0; i < spawnData.Length; i++) {
                    if (i >= cloneProjectiles.Length) break;

                    float pitchOffset = (float)(Mathf.RandomFloat(5.0f)); // 5 번지수만 낮음 (-3 semitone)
                    if (i == 4 && !phaseTwoMode) {
                        pitchOffset -= 3f; // "똥" 소리 - 피치 낮춤
                      }

                    cloneProjectiles[i].SetActive(true);
                    cloneTransforms = GameObject.FindObjectsOfType<CloneProjectile>();

                      // 사출! 로컬 좌표 기준으로 보스 정면 방향으로 쏨 (tp ^ ^ ^1 스타일)
                    Vector3 ejectDir = bossDirection.normalized * spawnData[i].distance;
                    cloneTransforms[i].position = new Vector3(
                        targetPosition.x + bossDirection.x * spawnData[i].distance,
                        targetPosition.y + 2f, // 약간 띄워서 튀게
                        targetPosition.z + bossDirection.z * spawnData[i].distance
                      );

                      // 🎭 사운드 피치 효과 (5 번지수만 낮음)
                    if (i == 4) {
                        PlaySoundEffect($"clone_pitch_{i}.wav", pitchOffset);
                        cloneTransforms[i].enabled = false; // 판정 비어있게 설정
                        Debug.Log("🕳️ [Pattern D] 클론 " + i + " 은 '똥' 판정 비어있음 (비어있는 5 번지수)!");
                      } else {
                        PlaySoundEffect($"clone_pitch_{i}.wav", pitchOffset);
                      }

                      // 🎬 펼치기 애니메이션: 순차적 등장 지속 시간
                    if (i < spawnData.Length - 1) {
                        float delay = Mathf.Lerp(0.1f, 0.4f, i / (float)spawnData.Length);
                        yield return new WaitForSeconds(delay * 0.5f);
                      }

                    hasDeployedClones = true;
                  }

                Debug.Log("✅ [Pattern D] 분신 모두 펼침! 🎵 사운드: 🔊띵, 띵, 띵, 똥(5 번째 비어있음), 띵, 띵");

              } else {
                yield break; // 클론이 없으면 스킵 (디버그)
              }

              // 3️⃣ 회전 스윕 시작! 분신 펼친 상태 그대로, 보스 본체를 중심으로 회전
            StartCoroutine(RotateSweepAttack());
           }


          /// <summary>
          /// 🌪️ 회전 스윕 공격 (분신이 펼쳐진 상태에서 중심 축 기준 빙글 도는 공격)
          /// 1 페이즈: EaseInExpo - 매우 느릿 → 가속 → 폭발
          /// 2 페이즈: Linear - 처음부터 끝까지 정속 고속 회전
          /// </summary>
        private IEnumerator RotateSweepAttack() {

              // 🔄 회전 스윕 시작 (분신 펼친 상태 그대로)
            rotationAccumulator = 0f;

            while (rotationAccumulator < 720f) { // 360° * 2 회 = 720° 완전 클린

                float currentRotation = rotationAccumulator;

                  // 1 페이즈 vs 2 페이즈 로직 전환
                if (currentRotation < rotationSpeedBase / 5f) {
                       // 🔥 초기 매우 느릿하게 돕음 (EaseInExpo: 처음 거의 정지, 점점 가속)
                    float progress = currentRotation / (rotationSpeedBase / 5f);
                    float speed = Mathf.Pow(progress, 2f) * rotationSpeedBase * 10f; // expo 가속

                        // 🎮 회전 적용 (보스 본체 중심 + 분신들과 함께 회전)
                    RotateBossAndClones(speed, bossBodyCenter.rotation, out Vector3 currentFacingDir);

                       // 🔦 공격 판정: 분신들이 플레이어 닿는지 확인 (회전하며)
                    if (GetCloneHit(playerPosition, cloneTransforms, currentFacingDir)) {
                        ApplyCloneDamage(playerPosition, 0.1f); // 회전 속도에 따른 데미지
                        InvokeDamageFeedback(player.transform.position, GetClonesActiveCount(cloneTransforms));
                      }

                    yield return null;

                  } else {
                      // 💥 폭발적 가속 (회전이 끝나갈수록)
                    float progress = (rotationAccumulator - (rotationSpeedBase / 5f)) / ((360 * 2) - (rotationSpeedBase / 5f));
                    float speed = Mathf.Pow(progress, 4f) * rotationSpeedBase; // 더 급격한 expo

                    RotateBossAndClones(speed, bossBodyCenter.rotation, out Vector3 currentFacingDir);

                    if (GetCloneHit(playerPosition, cloneTransforms, currentFacingDir)) {
                        ApplyCloneDamage(playerPosition, 0.15f);
                        InvokeDamageFeedback(player.transform.position, GetClonesActiveCount(cloneTransforms));
                      }

                    yield return null;
                  }

                rotationAccumulator += rotationSpeedBase * 0.33f; // 프레임 기준 속도 조정
              }

            Debug.Log("🌪️ [Pattern D] 회전 스윕 완료! 방 전체를 청소했음 (360° x 2 = " + rotationAccumulator + "°)");

              // 🏁 회전 종료 후 재진입 대기
            yield return new WaitForSeconds(1.5f);

              // 패턴 전환 (Phase 2 로 진입)
            if (!phaseTwoMode) {
                phaseTwoMode = true;
                InvokeSoundEffect("pattern_d_phase_2_start.wav", 0f);
                Debug.Log("⚠️ [Pattern D] Phase 2 진입! 이제 Linear 정속 회전!");

                  // 🔄 다시 회전 (2 번째 바퀴 - Linear)
                yield return StartCoroutine(RotateSweepAttack_LinearMode());

              } else {
                  // 2 페이즈: Linear 로 즉시 재회전 (뜸 들이지 않음!)
                yield return StartCoroutine(RotateSweepAttack_LinearMode());
              }

          }


          /// <summary>
          /// 🔄 회전 적용 로직 - 보스 본체 + 분신들을 함께 회전
          /// 마인크래프트-style: `^ ^ ^1` (로컬 좌표 기준 시선 방향) 을 축으로
          /// </summary>
        private void RotateBossAndClones(float speedDeg, Quaternion targetRotation, out Vector3 localFacingDir) {
              // 보스 회전 적용
            float rotationChange = speedDeg * 0.05f; // 프레임당 속도 조정

            Debug.Log($"🔄 [Pattern D] Rotation Update: speed={speedDeg:F1} deg/frame");

            RotateAroundCenter(bossBodyCenter, rotationChange);

              // 분신들도 회전 적용 (보스 중심 기준)
            if (cloneTransforms != null) {
                foreach (var clone in cloneTransforms) {
                    if (clone != null && clone.activeSelf) {
                        Quaternion newRot = Quaternion.Euler(0, targetRotation.eulerAngles.y * (rotationChange / 360f), 0); // 로컬 시계 방향 회전

                        Vector3 localPos = GetLocalPositionRelative(clone, bossBodyCenter);

                          // 분신 위치를 보스 기준 로컬 좌표로 변환 후 적용
                        clone.position = CalculateWorldPositionFromLocal(targetRotation, localPos, rotationChange * (1f / 360f));
                      }
                  }
              }

            localFacingDir = transform.forward;
           }


          /// <summary>
          /// 🎯 패턴 D: 회전 스윕 2 페이즈 (Linear 정속) - 처음부터 끝까지 같은 속도로 돌림!
          /// 뜸 들이지 않고 칼 같이 고속으로 방 청소
          /// </summary>
        private IEnumerator RotateSweepAttack_LinearMode() {
            float startRot = rotationAccumulator;
            float endRot = 720f; // 360° * 2 회 = 720°

            Debug.Log("⚡ [Pattern D Phase 2] Linear 회전 시작! (처음부터 끝까지 정속)");

            while (rotationAccumulator < endRot) {
                  // 🔊 사운드 힌트 (2 페이즈: 뜸 들이지 않음, 계속 돌림!)
                if (rotationAccumulator % 60f < 3f) {
                    float pitchOffset = rotationAccumulator % 7 == 4 ? -3f : 0f; // 5 번지수만 낮음
                    int cloneIndex = (int)(rotationAccumulator / 60f);

                    if (cloneIndex >= 0 && cloneIndex < cloneProjectiles.Length) {
                        PlaySoundEffect($"clone_pitch_{cloneIndex}.wav", pitchOffset);
                        if (cloneIndex == 4) {
                              // 🕳️ 똥 (5 번칸 비어있음!) - 판정 거리는 0
                            Debug.Log("🕳️ [Pattern D Phase 2] Clone " + cloneIndex + " 은 '똥'(비어있는 5 번지수)");
                          }
                      }
                  }

                  // 🔥 정속 Linear 회전 (처음부터 끝까지 같은 속도!)
                float rotationSpeed = rotationSpeedBase; // Linear! 일정 속도 유지

                RotateBossAndClones(rotationSpeed, transform.rotation, out Vector3 currentFacingDir);

                   // 판정: 분신들이 플레이어 닿는지 (회전하는 각도마다 공격 범위 이동)
                if (GetCloneHit(playerPosition, cloneTransforms, currentFacingDir)) {
                    ApplyCloneDamage(playerPosition, 0.2f); // 고정 고속 데미지
                    InvokeDamageFeedback(player.transform.position, 10);
                  }

                yield return null;
              }

            Debug.Log("✅ [Pattern D Phase 2] Linear 회전 완료! 360° 한 바퀴를 칼같이 돌았음");
           }


          /// <summary>
          /// 🎮 입력 시스템 연결 및 패턴 실행
          /// </summary>
        private void OnPatternTriggered() {
            if (phaseTwoMode) {
                Debug.Log("[Pattern D] 2 페이즈 모드에서 회전 스윕 시작!");
                StartCoroutine(RotateSweepAttack_LinearMode());
             } else {
                Debug.Log("[Pattern D] 1 페이즈에서 클론 분신 전개 및 회전 스윕 준비!");
                ExecuteFullRotationSequence();
             }
          }

        private void ExecuteFullRotationSequence() {
             // 초기화 후 패턴 실행
            phaseTwoMode = false;
            StartCoroutine(RotateSweepAttack());
           }


      }
}
