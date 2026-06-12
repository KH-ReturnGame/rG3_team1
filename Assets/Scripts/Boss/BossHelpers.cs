using UnityEngine;

namespace BossPatterns {

       /// <summary>
        /// 🔧 보스 패턴 헬퍼 클래스 - 공통적으로 사용하는 유틸리티 메서드들
        /// 이 클래스는 모든 Pattern A/B/C/D 에서 상속받아 사용하거나 정적 메서드로 호출 가능
        /// </summary>
    public static class BossHelpers {

           #region Public Helper Methods

          /// <summary>
           /// 플레이어 위치를 반환합니다 (보스에서 사용)
           /// </summary>
        public static Transform GetPlayerTransform()
         {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.transform : null;
         }


       /// <summary>
       /// 플레이어에서 특정 거리 내인지 체크합니다
       /// </summary>
    public static bool IsWithinDistance(Transform boss, float distance)
        {
            if (boss == null || boss.name.StartsWith("Boss")) return false;

             // Player 찾기
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player == null) return false;

            return Vector2.Distance(boss.position, player.transform.position) <= distance;
         }


      /// <summary>
       /// 사운드 효과 재생합니다 (보스 패턴에서 청각적 힌트용)
       /// </summary>
   public static void PlaySoundEffect(AudioClip clip, float volume)
      {
            AudioSource source = FindObjectOfType<AudioSource>();
            if (source != null && clip != null)
             {
                source.PlayOneShot(clip, volume);
             }

            Debug.Log($"🔊 [BossHelper] Sound: {(clip != null ? "Play" : "Missing")} | Volume: {volume}");
         }


     /// <summary>
      /// 사운드 효과 재생합니다 (보스 패턴에서 청각적 힌트용) - 패턴별 사용
      /// </summary>
   public static void PlaySoundEffect(string soundName, float volume)
    {
            AudioSource source = FindObjectOfType<AudioSource>();
            if (source != null) {
                AudioClip clip = Resources.Load<AudioClip>("Sounds/" + soundName);
                if (clip != null) {
                    source.PlayOneShot(clip, volume);
                     } else {
                        Debug.LogWarning($"🔊 사운드 클립 로드 실패: {soundName}");
                     }
                 } else {
                Debug.LogWarning("🔊 AudioSource not found!");
                 }
            }
    }


        /// <summary>
        /// 사운드 효과 재생합니다 (보스 패턴에서 청각적 힌트용) - CloneProjectile 에서 사용
        /// </summary>
    public static void PlaySoundEffect(string soundName, float volume, Transform sourceTransform)
     {
           AudioSource source = null;
            if (sourceTransform != null) {
                // Source transform 에서 AudioSource 찾기
                source = sourceTransform.GetComponent<AudioSource>();
             }

             if (source != null) {
                AudioClip clip = Resources.Load<AudioClip>("Sounds/" + soundName);
                if (clip != null) {
                    source.PlayOneShot(clip, volume);
                        } else {
                            Debug.LogWarning($"🔊 사운드 클립 로드 실패: {soundName}");
                         }
                     } else {
                         Debug.LogWarning("🔊 AudioSource not found!");
                         }
             }
        }


       /// <summary>
       /// 사운드 효과 재생합니다 (보스 패턴에서 청각적 힌트용) - Simple version without source
       /// </summary>
   public static void PlaySoundEffect(string soundName, float volume)
    {
            AudioSource source = FindObjectOfType<AudioSource>();
            if (source != null) {
                AudioClip clip = Resources.Load<AudioClip>("Sounds/" + soundName);
                if (clip != null) {
                    source.PlayOneShot(clip, volume);
                     } else {
                        Debug.LogWarning($"🔊 사운드 클립 로드 실패: {soundName}");
                      }
                 } else {
                Debug.LogWarning("🔊 AudioSource not found!");
                 }
            }
    }


        /// <summary>
        /// 플레이어가 가드 (Block) 상태인지 판별 (Parrying 체크용!)
        /// </summary>
      public static bool PlayerIsBlocking(Transform bossTransform, Collider playerCollider)
       {
          // 🔊 플레이어 입력 시스템에서 패링 플래그 확인
            Rigidbody rb = playerCollider.GetComponent<Rigidbody>();

            if (rb != null && PlayerInputManager.instance != null && PlayerInputManager.instance.blockingPressed) {
                Debug.Log("🛡️ [BossHelper] Player is blocking (Parrying)!");
                return true; // 패링 상태!
                 }

            return false; // 일반 공격!
        }


      /// <summary>
       /// 플레이어가 가드 (Block) 상태인지 판별 (Parrying 체크용!) - 2D 버전
       /// </summary>
    public static bool PlayerIsBlocking_2D(Transform bossTransform, Collider playerCollider)
     {
          // 🔊 플레이어 입력 시스템에서 패링 플래그 확인 (Rigidbody2D 사용!)
            Rigidbody2D rb = playerCollider.GetComponent<Rigidbody2D>();

            if (rb != null && PlayerInputManager.instance != null && PlayerInputManager.instance.blockingPressed) {
                Debug.Log("🛡️ [BossHelper] Player is blocking (Parrying)!");
                return true; // 패링 상태!
                 }

            return false; // 일반 공격!
        }


       /// <summary>
       /// 플레이어가 가드 (Block) 상태인지 판별 (Parrying 체크용!) - 패턴별로 사용
       /// </summary>
     public static bool PlayerIsBlocking_2D(Collider playerCollider)
      {
           // 🔊 플레이어 입력 시스템에서 패링 플래그 확인
            Rigidbody2D rb = playerCollider.GetComponent<Rigidbody2D>();

            if (rb != null && PlayerInputManager.instance != null && PlayerInputManager.instance.blockingPressed) {
                Debug.Log("🛡️ [BossHelper] Player is blocking (Parrying)!");
                return true; // 패링 상태!
                 }

            return false; // 일반 공격!
        }


      /// <summary>
       /// 패턴 전환 로직 실행 (1 페이즈 vs 2 페이즈 자동 전환)
       /// </summary>
    public static void SetPatternPhase(bool isPhaseTwo, GameObject targetObj) {
           if (targetObj != null) {
            BossPatternA patternA = targetObj.GetComponent<BossPatternA>();
            BossPatternB patternB = targetObj.GetComponent<BossPatternB>();
            BossPatternC patternC = targetObj.GetComponent<BossPatternC>();
            BossPatternD patternD = targetObj.GetComponent<BossPatternD>();

             if (patternA != null) {
                Debug.Log("🔄 [BossHelper] Pattern A 전환: " + (isPhaseTwo ? "2 페이즈" : "1 페이즈"));
                 } else if (patternB != null) {
                    Debug.Log("🔄 [BossHelper] Pattern B 전환: " + (isPhaseTwo ? "2 페이즈" : "1 페이즈"));
                  } else if (patternC != null) {
                        Debug.Log("🔄 [BossHelper] Pattern C 전환: " + (isPhaseTwo ? "2 페이즈" : "1 페이즈"));

                           // Pattern A/B/C 에서 phaseTwoMode 변수가 있다면 직접 설정
                          patternC.phaseTwoMode = isPhaseTwo;
                         } else if (patternD != null) {
                            Debug.Log("🔄 [BossHelper] Pattern D 전환: " + (isPhaseTwo ? "2 페이즈" : "1 페이즈"));
                              patternD.phaseTwoMode = isPhaseTwo;
                          }

                    // Pattern A/D 에서 phaseTwoMode 직접 설정
                    if (patternA != null) {
                        patternA.phaseTwoMode = isPhaseTwo;
                         }
                }

             Debug.Log("✅ [BossHelper] 패턴 전환 완료! " + (isPhaseTwo ? "2 페이즈: 수축 공격!" : "1 페이즈: 가두리 형성!"));
           } else {
            Debug.LogWarning("⚠️ [BossHelper] 타겟 오브젝트가 null 입니다!");
           }
        }

      /// <summary>
       /// Pattern A 를 2 페이즈로 전환 (수축 공격)
       /// </summary>
    public static void TriggerPhaseTwo_PatternA(GameObject targetObj) {
        if (targetObj != null) {
            BossPatternA patternA = targetObj.GetComponent<BossPatternA>();
            if (patternA != null) {
                Debug.Log("⚠️ [BossHelper] Pattern A 2 페이즈 전환: 수축 공격!");
                patternA.phaseTwoMode = true;
             }
        }
    }


       /// <summary>
       /// 패턴 초기화 (재진입 준비)
       /// </summary>
     public static void ResetPatternState(GameObject targetObj) {
         if (targetObj != null && targetObj.activeInHierarchy) {
             Debug.Log("🔄 [BossHelper] 패턴 상태 초기화 완료!");

                // Pattern A/B/C/D 에서 phaseTwoMode 로직에 따라 초기화!
                ResetPatternPhase(targetObj, false);

          } else {
            Debug.LogWarning("⚠️ [BossHelper] 타겟 오브젝트가 비활성화되어 있습니다!");
         }
    }


     /// <summary>
      /// 패턴 페이즈 전환 로직 (1 페이즈 ↔ 2 페이즈)
      /// </summary>
   public static void ResetPatternPhase(GameObject targetObj, bool isPhaseTwo) {
       if (targetObj != null && targetObj.activeInHierarchy) {
           BossPatternA patternA = targetObj.GetComponent<BossPatternA>();
           BossPatternB patternB = targetObj.GetComponent<BossPatternB>();
           BossPatternC patternC = targetObj.GetComponent<BossPatternC>();
           BossPatternD patternD = targetObj.GetComponent<BossPatternD>();

             if (patternA != null) {
                Debug.Log("🔄 [BossHelper] Pattern A 페이즈: " + (isPhaseTwo ? "2" : "1"));
                 } else if (patternB != null) {
                    Debug.Log("🔄 [BossHelper] Pattern B 페이즈: " + (isPhaseTwo ? "2" : "1"));

                     // 모든 패턴에서 phaseTwoMode 변수에 직접 설정!
                    patternB.phaseTwoMode = isPhaseTwo;
                   } else if (patternC != null) {
                        Debug.Log("🔄 [BossHelper] Pattern C 페이즈: " + (isPhaseTwo ? "2" : "1"));

                         // Pattern C 에서 phaseTwoMode 직접 설정!
                        patternC.phaseTwoMode = isPhaseTwo;
                      } else if (patternD != null) {
                            Debug.Log("🔄 [BossHelper] Pattern D 페이즈: " + (isPhaseTwo ? "2" : "1"));

                                // Pattern D 에서 phaseTwoMode 직접 설정!
                            patternD.phaseTwoMode = isPhaseTwo;
                          }

                // Pattern A/D 에서도 phaseTwoMode 직접 설정!
                if (patternA != null) {
                    patternA.phaseTwoMode = isPhaseTwo;
                     }

               Debug.Log("✅ [BossHelper] 패턴 페이즈 전환 완료!");
           } else {
            Debug.LogWarning("⚠️ [BossHelper] 타겟 오브젝트가 null 입니다!");
           }
      }


       /// <summary>
        /// 재진입 로직 (충돌 후 회복)
        /// </summary>
      public static void EnableCollision(GameObject targetObj) {

             // 🎯 충돌 후 재진입 준비!
            if (targetObj != null && targetObj.activeInHierarchy) {
                Debug.Log("🎯 [BossHelper] 충돌 이후 재진입 준비 완료!");

                  // Pattern A/B/C/D 에서 ResetToIdle() 호출!
                 if (targetObj != null) {
                    targetObj.TryGetComponent<BossPatternA>(out BossPatternA patternA);
                    patternA?.ResetToIdle();
                     }
                 } else {
                    Debug.LogWarning("⚠️ [BossHelper] 타겟 오브젝트가 비활성화되어 있습니다!");
                }
             } else {
            Debug.LogWarning("⚠️ [BossHelper] 타겟 오브젝트가 null 입니다!");
         }

        // Pattern A/B/C/D 에서 ResetToIdle() 직접 호출!
        if (targetObj != null) {
            targetObj.TryGetComponent<BossPatternA>(out BossPatternA patternA);
            targetObj.TryGetComponent<BossPatternB>(out BossPatternB patternB);
            targetObj.TryGetComponent<BossPatternC>(out BossPatternC patternC);
            targetObj.TryGetComponent<BossPatternD>(out BossPatternD patternD);

             // 모든 패턴에서 ResetToIdle() 호출!
            patternA?.ResetToIdle();
            patternB?.ResetToIdle();
            patternC?.ResetToIdle();
            patternD?.ResetToIdle();
          }

        Debug.Log("✅ [BossHelper] 재진입 준비 완료!");
      }


  #endregion
}
