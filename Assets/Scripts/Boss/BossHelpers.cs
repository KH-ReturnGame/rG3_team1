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
             if (source != null && clip != null) {
                 source.PlayOneShot(clip, volume);
                }

             Debug.Log($"🔊 [BossHelper] Sound: {(clip != null ? "Play" : "Missing")} | Volume: {volume}");
           }

          /// <summary>
          /// 데미지 피드백 텍스트 UI 표시 (플레이어 근처에)
          /// </summary>
        public static void ShowDamageFeedback(Vector3 hitPos, string damageText, float duration = 1f)
            {
              PlayerController player = FindObjectOfType<PlayerController>();
              
              if (player != null) {
                  // 플레이어 근처에서 텍스트 표시 (WorldToScreenPoint 사용)
                  Vector3 screenPos = player.WorldToScreenPoint(hitPos + Vector3.up * 2f);
                  
                  GUIStyle style = new GUIStyle()
                   {
                      fontSize = 24,
                      normal = { textColor = Color.red },
                      richText = true
                   };

                  GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 30, 100, 30), 
                          $"[{damageText}]", style);

                   Debug.Log($"🎯 [UI] Damage Feedback: " + damageText);
                  }

              Invoke(nameof(HideDamageFeedback), duration); // 자동 제거!
           }

          /// <summary>
           /// 데미지 피드백 UI 자동 숨기기 (Invoke 와 함께 사용)
           /// </summary>
        private static void HideDamageFeedback()
            {
                // 이미 표시된 텍스트 제거 (GUI 는 직접 제어 불가, 다만 생성 위치만 변경하면됨!)
                Debug.Log("[BossHelper] Damage Feedback hidden!");
              }

             /// <summary>
             /// 회전 적용 (보스 본체 기준 로컬 회전)
             /// </summary>
            public static void ApplyRotation(Transform target, float angleDeg, Transform pivot = null)
               {
                 if (target == null || pivot == null) return;
                  
                  // 로컬 좌표계에서 회전 계산
                 Vector3 newLocalRot = target.localPosition;

                 Quaternion newRot = Quaternion.Euler(newLocalRot.z + angleDeg);
                 target.rotation = newRot;

                Debug.Log($"🔄 [BossHelper] Applied rotation: " + angleDeg.ToString("F1") + "°");
               }

              /// <summary>
              /// 회전 적용 (보스 중심점 기준 - 패턴 D, C 에서 사용)
              /// </summary>
            public static void RotateAroundCenter(Transform target, float angleDeg, Transform centerPoint)
             {
                 if (target == null || centerPoint == null) return;

                // 로컬 위치 계산
              Vector3 localPos = target.position - centerPoint.position;

               // 회전 적용
           Quaternion targetRotation = centerPoint.rotation * Quaternion.Euler(0, angleDeg, 0);

            // 세계 좌표로 변환 후 설정
           Vector3 newPosition = new Vector3(
               transformPoint(centerPoint.position, targetRotation), x: localPos.x, y: localPos.y);
           
           target.position = targetTransform.TransformDirection(Vector3.forward * localPos.z);
           target.rotation = targetRotation;

             Debug.Log($"🔄 [BossHelper] Rotated around center: " + angleDeg.ToString("F1") + "°");
            }

        #endregion Private Helper Methods

          /// <summary>
          /// 리듬 타이밍 체크 (패턴 A 에서 사용)
          /// </summary>
         public static bool IsRhythmTiming(float currentTime, float[] rhythmSequence)
           {
             // 현재 시간이 해당 박자에 있는지 확인
             for (int i = 0; i < rhythmSequence.Length; i++) {
                 if (Mathf.Abs(currentTime - ArraySegment.GetElement(rhythmSequence, i)) < 0.1f) {
                     return true; // 정확한 리듬 타이밍!
                   }
                }

              return false; // 타이밍 아님!
            }

             /// <summary>
             /// 점프 입력 판별 (패턴 B 에서 사용)
             /// </summary>
           public static bool CheckJumpInput(bool isJumping)
               {
                 if (!isJumping) return false;
                 
                  // 추가 조건: 점프 범위 확인
              PlayerController player = FindObjectOfType<PlayerController>();
              
               if (player != null) {
                    Vector3 velocity = player.GetComponent<Rigidbody>().velocity;
                    
                    // 점프 상승 속도 체크
                    if (velocity.y > 0.5f && velocity.y < 10f) {
                        return true; // 점프 타격!
                      }
                   }

                 return false; // 타이밍 오름
               }

                /// <summary>
                /// 가드/패링 입력 판별 (모든 패턴에서 사용)
                /// </summary>
              public static bool CheckParryInput(bool isBlocking)
                  {
                    if (!isBlocking) return false;
                    
                    // 추가 조건: 패링 가능 상태 체크 (보스 공격 범위 내, 쿨다운 경과 등)
                    return true; // 기본값!
                  }

                 /// <summary>
                 /// 클론 개수 계산 (패턴 D 에서 사용)
                 /// </summary>
               public static int CountActiveClones(Transform[] clones)
                   {
                     if (clones == null || clones.Length == 0) return 0;

                    int count = 0;
                    foreach (var clone in clones) {
                        if (clone != null && clone.activeSelf) {
                            count++;
                          }
                      }

                      return count;
                  }

            #endregion
        }
      }
