using UnityEngine;
using System.Collections;

namespace BossPatterns {

     /// <summary>
      /// 🕸️ Pattern C: 잔상 가두리 레이저 (홀로그램 분신 수축 공격)
      /// 핵심 기믹: 홀로그램 클론을 사방으로 뿌리고 삼각형/사각형 가두리 형성 → 동시 수축!
      /// 패링 타이밍 정확히 맞춰야 탈출 가능 (가드 반동 활용!)
      /// </summary>
    public class BossPatternC : MonoBehaviour {

          [Header("=== 기본 설정 ===")]
        public Transform bossBody;
        public GameObject playerPrefab;

          [Header("=== 패턴 C: 가두리 레이저 ===")]
          [Tooltip("홀로그램 클론 프리펙브 - 사방으로 뿌림")]
        public GameObject hologramClonePrefab; // 홀로그램 클론 객체

          [Tooltip("가두리 형태 (삼각형/사각형)")]
        public bool isTriangleFormation = true; // 삼각형 형성 여부

          [Tooltip("가두리 반경 (6 단위 범위)")]
        public float cageRadius = 6f;

          [Tooltip("수축 시작 딜레이 (1.5 초)]")]
        public float contractionDelay = 1.5f;

          [Tooltip("수축 속도 (초당 2 단위)")]
        public float contractionSpeed = 2f;

          [Tooltip("가두리 생성 수 (8 개 클론)]")]
        public int cageCloneCount = 8;

          [Header("=== 시각 효과 ===")]
          [Tooltip("홀로그램 색상 (시안, 마젠타, 황록색)")]]
        public Color hologramColor = new Color(0f, 1f, 1f); // 시안

          // --- INTERNAL STATE ---
        private bool phaseTwoMode = false;
        private GameObject[] cageClones; // 가두리 클론들 (8 개)
        private Vector3[] cagePositions; // 가두리 위치 배열 (6 개정점)
        private float currentCageRadius = 0f;
        private bool contractionStarted = false;
        private int patternCycle = 0; // 패턴 반복 카운터


        public enum PatternCState {
            IDLE,               // 대기 중 (청각 힌트 준비)
            DISPENSING_HOLOCLONES,// 홀로그램 클론 뿌리는 단계
            CAGE_FORMATION,      // 가두리 형성 단계
            CAGE_CONTRACTION,    // 수축 공격 단계
            RECOVERY             // 재진입 대기
           }

        private PatternCState currentState = PatternCState.IDLE;

          // --- HELPER COMPONENTS ---
        private Rigidbody2D rb;              // Physics component (Unity 2D)
        private AudioSource audioSource;     // Audio playback


          /// <summary>
          /// 🕸️ Pattern C: 잔상 가두리 루틴 실행!

          /// 1 페이즈: 홀로그램 클론 뿌리기 (사방으로) → 가두리 형성 시각 효과
          /// 2 페이즈: 수축 공격 시작! (패링 타이밍을 정확히 맞춰야!)
          /// 핵심 기믹: 가두리가 점점 작아지며 플레이어 포획 → 가드 반동 활용해야 탈출 가능!
          /// </summary>
        public void ExecutePatternCCageTrapRoutine() {

            Debug.Log("🕸️ [Pattern C] 잔상 가두리 레이저 시작!");

              // 초기화 (패턴 전환 시)
            ResetCageState();

            if (phaseTwoMode) {
                Debug.log("⚠️ 2 페이즈: 수축 공격! 패링 타이밍을 맞춰라!");
                StartCoroutine(ExecutePhase2CageContraction());
               } else {
                Debug.log("✅ 1 페이즈: 홀로그램 가두리 형성!");
                StartCoroutine(ExecutePhase1CageFormation());
                return;
               }

            StartCoroutine(ExecutePhase2CageContraction()); // 자동 진입!
             }


        private void ResetCageState() {

              // 클론 비활성화 초기
            cageClones = null;

              // 시각적 효과 제거
            if (hologramColor.a > 0f) {
                AudioSource audio = GetComponent<AudioSource>();
                if (audio != null) audio.Play(); // 사운드 효과!
               }

              Debug.log("🔄 [Pattern C] 가두리 상태 초기화 완료!");
           }


          /// <summary>
          /// 1 페이즈: 홀로그램 가두리 형성 (사방으로 클론 뿌리기!)
          /// </summary>
        private IEnumerator ExecutePhase1CageFormation() {

              // 🔊 청각 힌트 (반짝반짝 사운드 효과) - 패턴 전환 전 준비!
            Debug.Log("🔊 [Pattern C Phase 1] 홀로그램 잔상 뿌리기 시작!");

              // 1️⃣ 홀로그램 클론 사방으로 뿌리기 (사각형/삼각형 위치 선정)
            if (hologramClonePrefab != null && cageClones == null) {

                cageClones = new GameObject[cageCloneCount];
                GenerateHologramCage(isTriangleFormation);

                Debug.log($"🕸️ [Pattern C Phase 1] 가두리 " + cageCloneCount + " 개 홀로그램 형성 완료!");

                  // 👀 시각적 효과 (홀로그램 색상)
                HighlightCageFormation(cageClones, hologramColor);

             } else if (cageClones != null) {
                 // 이미 생성됨! 시각 효과만 표시.
                Debug.log("👁️ [Pattern C Phase 1] 가두리 클론이 이미 존재함 - 시각 효과!");
                HighlightCageFormation(cageClones, hologramColor);

             }

              // 🔊 추가 청각 효과 (가두리 완성 신호)
            PlaySoundEffect("cage_form_complete.wav", 0.8f);

              // 2️⃣ 수축 공격 시작 대기 (1.5 초)
            Debug.log($"⏰ [Pattern C Phase 1] 가두리 수축 공격 {contractionDelay} 초 후 시작!");

            yield return new WaitForSeconds(contractionDelay);

            phaseTwoMode = true; // 자동 2 페이즈 전환!

              // 💥 수축 공격 시작!
            StartCoroutine(ExecutePhase2CageContraction());
             }


          /// <summary>
          /// 홀로그램 가두리 생성 및 활성화 (삼각형/사각형 배열)
          /// </summary>
        private void GenerateHologramCage(bool isTriangleFormation) {

              // 중심점 계산
            int totalPoints = isTriangleFormation ? 3 : 4; // 삼각형: 3 지점, 사각형: 4 지점 (중심 포함)

            cagePositions = new Vector3[totalPoints];
            
              // 중심점 설정 (0 번지수 - 고정)
            float radiusFactor = isTriangleFormation ? cageRadius : cageRadius * 1.2f;
            Vector3 centerPos = bossBody.position;

            // 각 지점 계산 및 클론 생성
            for (int i = 0; i < totalPoints; i++) {
                float angle = (2 * Mathf.PI / totalPoints) * i;

                  // 지점 위치 계산
                Vector3 pointPos = centerPos + new Vector3(
                    Mathf.Cos(angle) * radiusFactor,
                    Mathf.Sin(angle) * radiusFactor,
                    0f
                 );

                cagePositions[i] = pointPos;

                  // 홀로그램 클론 생성 (각 지점에 하나씩!)
                if (i < cageCloneCount && hologramClonePrefab != null) {
                    GameObject clone = Instantiate(hologramClonePrefab, pointPos + new Vector3(0f, 1.5f, 0f), Quaternion.identity);
                    
                    if (clone != null) {
                         // 홀로그램 색상 적용
                        Renderer renderer = clone.GetComponent<Renderer>();
                        if (renderer != null) {
                            renderer.material.color = hologramColor; // 시안색!
                          }
                        
                        clone.SetActive(true);
                        cageClones[i] = clone; // 배열에 저장.
                       } else {
                        Debug.LogWarning("⚠️ [Pattern C Phase 1] 클론 생성 실패!");
                        cageClones[i] = null;
                       }
                    } else {
                    Debug.log($"🔄 [Pattern C Phase 1] 클론 #{i} 활성화 완료!");
                   }
                }

             }

             Debug.log("✅ 가두리 클론 생성 완료! " + totalPoints + " 개!");
           }


          /// <summary>
          /// 홀로그램 가두리 시각적 효과 (색상 깜빡임, 빛 등)
          /// </summary>
        private void HighlightCageFormation(GameObject[] clones, Color hologramColor) {

            if (clones == null || clones.Length == 0) return;

              // 각 클론마다 색상 피드백 효과!
            for (int i = 0; i < clones.Length; i++) {
                if (clones[i] != null && clones[i].activeSelf) {
                    Renderer rend = clones[i].GetComponent<Renderer>();
                    if (rend != null) {
                        Color originalColor = rend.material.color;

                        // 시각 효과: 색상 변화 + 밝기 증감
                        StartCoroutine(HighlightAnimation(clones[i], hologramColor, originalColor));
                     }
                  }
               }
           }


         private IEnumerator HighlightAnimation(GameObject targetObj, Color highlightColor, Color originalColor) {

             Render rend = targetObj.GetComponent<Renderer>();
             
             if (rend == null) yield break;

               // 시각 효과: 깜빡임 애니메이션
            float duration = 1.0f;
            float elapsedTime = 0f;

              while (elapsedTime < duration) {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                  // 색상 인터폴레이션
                rend.material.color = Color.Lerp(originalColor, highlightColor, t);
                yield return null;
               }

            rend.material.color = originalColor; // 원래 색 복원!
            Debug.log("👁️ [Pattern C] 시각 효과 완료!");
             }


           /// <summary>
           /// 플레이어 위치가 가두리 내부인지 판별 (삼각형/사각형 내포 체크)
           /// </summary>
        private bool IsPlayerInsideCage(Vector3 playerPos, GameObject[] cageClones) {

              // 가장 가까운 클론 거리 체크 (반경 2 단위 이내면 포획!)
            if (cageClones == null || cageClones.Length == 0) return false;

            float minDistToCage = float.MaxValue;

             for (int i = 0; i < cageClones.Length; i++) {
                if (cageClones[i] != null && cageClones[i].activeSelf) {
                    Transform cloneTrans = cageClones[i].transform;
                    minDistToCage = Mathf.Min(minDistToCage, Vector3.Distance(playerPos, cloneTrans.position));
                   }
                 }

            return minDistToCage < 2f; // 반경 2 단위 이내면 포획됨!
           }


           /// <summary>
           /// 수축 시점 데미지 적용 (패링 성공/실패 판별)
           /// </summary>
        private void ApplyCageContractionDamage(Vector3 hitPos, string feedbackText) {

            Debug.log($"💥 [Pattern C] 수축 데미지 발생! " + feedbackText);

              // 🔥 데미지 계산 (수축 속도 × 보스 최대 체력 multiplier)
            float damage = contractionSpeed * bossMaxHealth * 0.15f; // 약 15% 데미지!

             if (player != null) {
                ApplyDamageToPlayer(player.transform.position, damage);

                  // 💥 넉백 적용 (수축력으로 밀려나기)
                Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
                if (playerRb != null) {
                    Vector3 recoilDir = bossBody.position - hitPos;
                    recoilDir.Normalize();

                     // 강력하게 밀기! (반동력 계산)
                    playerRb.AddForce(recoilDir * 10f, ForceMode.VelocityChange);

                    Debug.log("⚡ 플레이어가 수축력으로 밀려나기!");
                   }

                  // 시각적 피드백
                InvokeDamageFeedback(hitPos, feedbackText + " 데미지: " + damage.ToString("F1"));
                 }

              Invoke(nameof(StopCageContraction), 2.0f); // 2 초 후 수축 중지!
         }


           /// <summary>
            /// 플레이어에 데미지 적용 (IDamageable 인터페이스 확인)
            /// </summary>
        private void ApplyDamageToPlayer(Vector3 hitPos, float damageValue) {

            PlayerController player = FindObjectOfType<PlayerController>();
            
              if (player != null) {
                 player.TakeDamage(damageValue, false, this, hitPos); // 패링=false (일반 데미지)
                Debug.log($"💥 [Pattern C] Apply Damage: " + damageValue.ToString("F1"));
               } else {
                Debug.log("[Pattern C] PlayerController not found!");
               }

             }


           /// <summary>
            /// 패턴 종료 후 회복 대기 (Idle 상태 복원)
            /// </summary>
        private IEnumerator RecoveryCoroutine() {

              // 회복 대기 시간 (1 초)
            yield return new WaitForSeconds(1.0f);

              // 속도 초기화
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) {
                rb.velocity = Vector2.zero;
                 }

               // 재진입 가능
             this.enabled = true;

              Debug.log("✅ [Pattern C] 재진입 완료!");
             yield break;
             }


           /// <summary>
           /// 수축 공격 중지 (패링 성공 시!)
           /// </summary>
        private void StopCageContraction() {

            Debug.log("🔒 [Pattern C] 수축 중지! 패턴 종료!");

              // 가두리 클론 비활성화
            if (cageClones != null) {
                foreach (GameObject clone in cageClones) {
                    if (clone != null) {
                        clone.SetActive(false); // 소멸 효과!
                       }
                   }
                }

             phaseTwoMode = false; // 패턴 해제!

              Debug.log("🏁 [Pattern C] 수축 공격 중지 완료!");
           }


           /// <summary>
            /// 재진입 대기 (충돌 후 회복)
            /// </summary>
        public void ResetToIdle() {

            Debug.log("🎯 [Pattern C] 보스 재진입 준비 (Idle 상태 복원)!");

               // 초기화 (컴포넌트 타입에 따라 Rigidbody/Rigidbody2D 차이 있음!)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.zero;

               // 상태 복원
            this.enabled = true; // 재진입 가능!
             }


           /// <summary>
           /// 🎮 입력 시스템 연결 및 패턴 실행
           /// </summary>
        public void OnPatternTriggered() {

              // 초기화 (패턴 전환 시)
            ResetCageState();

            if (phaseTwoMode) {
                Debug.log("[Pattern C] 2 페이즈에서 수축 공격!");
                StartCoroutine(ExecutePhase2CageContraction());
               } else {
                Debug.log("[Pattern C] 1 페이즈에서 홀로그램 가두리 형성!");
                StartCoroutine(ExecutePhase1CageFormation());
               }
            }


        private AudioClip LoadAudioClip(string soundName) {

             // 오디오 클립 로딩 (Assets/Sounds/ 폴더 기준)
            return Resources.Load<AudioClip>(soundName);
          }


       }
}
