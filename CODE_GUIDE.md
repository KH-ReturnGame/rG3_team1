# Project Red Hood — 코드 구조 가이드

> 유지보수용 문서. 코드가 어떻게 조직돼 있고, 어디를 고치면 무엇이 바뀌는지, 그리고 밟기 쉬운 함정들을 정리했다.
> (2026-07 기준. 파일을 옮기거나 시스템을 바꾸면 이 문서도 같이 갱신할 것)

---

## 1. 큰 그림 — 이 프로젝트의 3가지 핵심 규칙

### ① 거의 모든 매니저/UI는 "씬에 없다" — 코드가 스스로 만든다
하이어라키에서 GameManager나 HUD를 찾아도 **없다**. 대부분 이 패턴으로 게임 시작 순간 자동 생성된다:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Bootstrap()
{
    if (Instance == null) { var go = new GameObject("이름"); Instance = go.AddComponent<클래스>(); DontDestroyOnLoad(go); }
}
```

- 어떤 씬에서 Play를 눌러도 알아서 생기고, 씬이 바뀌어도 파괴되지 않는다(DontDestroyOnLoad).
- 접근은 `클래스.Instance` (static 싱글톤).
- **자동부팅 목록**: GameManager, Juice, SlowMoFx, Letterbox, Toast, AcquireFeed, AcquireBanner, HelpPopupUI, DialogueUI, HandbookUI, Minimap, QuestTracker, TreasureDetector, StatUI(=HUD, Hotbar/MenuUI도 같이 부착), DamagePopup, GameOverUI, LockedDoorUI, AreaTitle, CombatTutorial, AutoPrecog, PrecogCharm, VillageGuide, LowHealthFx, SceneFader 등.
- **씬에 직접 배치하는 것들**: 플레이어(프리팹), 적, 카메라(CameraFollow), 상점/제작대/게시판/NPC, BattleArena, TreasureChest, LockedDoor, FakeWall, HelpTrigger, TutorialSequence, IntroCutscene, HubEntryCutscene, CameraZone 등 "그 씬에 속한 것들".

### ② UI는 전부 IMGUI(OnGUI) — Canvas가 없다
- 모든 UI는 각 스크립트의 `OnGUI()` 안에서 `GUI.Label`, `GUI.DrawTexture` 등으로 **매 프레임 직접 그린다**. 유니티 Canvas/uGUI는 안 쓴다.
- 색·패널·테두리 같은 공용 그리기는 전부 **`UITheme`**(Assets/Script/UI/UITheme.cs) 헬퍼를 쓴다. **테마 색을 바꾸고 싶으면 UITheme 상단 팔레트 상수만 바꾸면 전 UI에 반영**된다(금테+먹색 테마).
- 아이콘·글로우·하트·팔각 메달리온 등은 이미지 파일이 아니라 **코드로 픽셀을 찍어 만든 절차 생성 텍스처**가 많다(에셋 없이 동작). 나중에 진짜 아트로 교체할 때는 해당 `~Tex()` 함수 대신 스프라이트를 그리면 된다.

### ③ UI가 열리면 플레이어 입력이 잠긴다 — Inventory의 플래그 집계
```csharp
// Inventory.cs
public static bool InvUIOpen, ShopUIOpen, CraftUIOpen, QuestUIOpen, HandbookUIOpen, DialogueOpen, PauseOpen, LockUIOpen;
public static bool IsUIOpen => 위 플래그 중 하나라도 true;
```
- 각 UI는 열릴 때 **자기 플래그만** 켜고 닫힐 때 끈다(서로 덮어쓰지 않게).
- `PlayerController.CheckInput()`이 `Inventory.IsUIOpen`이면 이동/공격 입력을 차단한다.
- **새 전체화면 UI를 만들면**: 플래그 하나 추가 → IsUIOpen에 OR → 열고 닫을 때 세팅. (LockedDoorUI가 최근 예시)

---

## 2. 폴더 지도

```
Assets/Script/        ← 우리(메인) 코드. 하위 폴더로 역할 구분
  Core/      게임 전역 시스템(매니저, 컷씬, 연출, 난이도)
  Player/    플레이어 조작·상호작용
  Combat/    적 AI, 투사체
  Inventory/ 아이템 데이터·인벤토리·장비
  UI/        모든 화면 표시(OnGUI)
  World/     씬에 배치하는 오브젝트(상자, 문, 상점, 기믹)
  Camera/    카메라 추적·구역
  Save/      세이브/로드, 타이틀 화면
Assets/Scripts/       ← 팀원 코드(ScenePlayerTest 등). 함부로 수정하지 않기
Assets/Prefabs/       ← 프리팹(Player, Enemy_*, EnemyProjectile, Treasure Chest…)
Assets/Resources/     ← 코드가 이름으로 로드하는 에셋
  Items/       모든 ItemData 에셋(여기 넣으면 도감·세이브에 자동 등록)
  Portraits/   대화 초상화 PNG — "화자 이름과 같은 파일명"이면 자동 사용
Assets/Scenes/        ← StartScene(타이틀), TutorialScene, StartingArea(마을), Stage1~3, BossScene, MainMap
```

---

## 3. 시스템별 핵심 파일

### Core — 전역 시스템
| 파일 | 역할 |
|---|---|
| **GameManager.cs** | 체력(반칸 단위 `currentHalf`)·골드·레벨/XP·스탯·포션 쿨타임·버프. 값이 바뀌면 `OnStatsChanged` 이벤트 → UI가 알아서 갱신. **사망 = `TakeDamageHalves`가 0 도달 → `Die()` → `OnPlayerDied` 이벤트** |
| **GameFlow.cs** | 스테이지 진행/귀환, 사망·클리어 **결과창**(스테이지 씬 전용. 튜토 사망은 GameOverUI 담당) |
| **Difficulty.cs** | 난이도(쉬움/어려움). `PlayerPrefs`에 저장. `Difficulty.AutoPrecog`로 조회 |
| **AutoPrecog.cs** | 쉬움 전용 — HP 반칸 위기+피격 직전에 짧은 슬로우 자동 발동(전 씬) |
| **CombatTutorial.cs** | 튜토 씬 전용 — 첫 전투 도움말 + **각성 패링 레슨**(1회) + 각성 회복 + 튜토 적 공격력 상한 |
| **Juice.cs** | 타격감: `Juice.Hit()` `JustParry()` `Deflect()` = 히트스톱+셰이크+플래시 |
| **ParryFx.cs** | 패링 스파크(절차 생성 VFX, 히트스톱 중에도 재생) |
| **SlowMoFx.cs** | 슬로우모션+집중 연출. `BeginHeld`(수동 해제)/`BeginTimed`(자동)/`End` |
| **Letterbox.cs** | 컷씬 위아래 검은 바. **`Letterbox.Covering`이 true면 HUD들이 스스로 숨는다** |
| **IntroCutscene.cs** | 새 게임 인트로(암전→쓰러진 자세(GroundSlam07 프레임 고정)→독백→기상) |
| **TutorialSequence.cs** | 튜토 각본: 독백 중 접근몹 → 경고 대사(검은 바 유지) → 발도 → 아레나 감시 |
| **HubEntryCutscene.cs** | 마을 첫 도착 각성 연출 + 여울 대화 |
| **QuestManager.cs** | 퀘스트 정의(`BuildQuests`)·수주·진행·완료. 보상은 게시판에서 수령(Claim) |
| **SceneFader.cs** | 씬 전환 페이드. `SceneFader.FadeToScene("씬이름")` |

### Player
| 파일 | 역할 |
|---|---|
| **PlayerController.cs** | 이동·점프·대시·공격 콤보·가드/패링·피격·컷씬 훅. 가장 크고 중요한 파일 |
| **PlayerInteractor.cs** | 근처 `IInteractable` 감지 → F 프롬프트 표시 → `Interact()` 호출 |

PlayerController에서 자주 만질 것:
- **패링**: `parryWindow`(전체 창) / `justParryWindow`(저스트 구간) / `parryChainWindow`(연속 쳐내기). `TakeDamage` 안에서 저스트(그로기+보상) vs 쳐내기(무효만) 분기
- **컷씬 제어**: `cutsceneActive`(입력·자동애니 잠금), `PlayAnim`, `PlayAnimFrozen`(특정 프레임 정지), `CutsceneWalk`, `CutsceneDrawSword`
- **투사체 패링**: `TryDeflectProjectile` — Projectile이 호출

### Combat — 적
- **Enemy.cs** = 베이스 클래스. 상태머신(Patrol→Chase→Windup→Strike→Recover / Groggy / Dead)이 `Tick~()` virtual 메서드로 쪼개져 있어서 **상속으로 이동/공격만 교체**한다.
  - `RangedEnemy` : 거리 유지 + 투사체 발사(`aimAngleLimit`으로 조준각)
  - `FlyingEnemy` : 중력 0 비행, 플레이어 위 고도 유지 → 급강하 돌진(`hoverHeight`, `diveRecover`)
- 공통 기능(체력/그로기/전리품/사망/데미지 숫자)은 베이스에 있으니 새 적도 웬만하면 **Enemy 상속 + Tick 오버라이드**로.
- `Enemy.WindupStarted` — 적이 공격 예비동작에 들어가면 발생하는 **static 이벤트**. 예지 3형제(CombatTutorial/AutoPrecog/PrecogCharm)가 전부 이걸 구독한다.
- `AttackHold` — 대화창/컷씬 중이면 모든 적이 공격을 시작하지 않는 게이트(BeginAttack 첫 줄).
- **Projectile.cs** : 직선 비행, 플레이어 명중 시 피해. 패링 창이면 **반사**되어 적에게 플레이어 공격력 기준 피해(`reflectDamageMult`).

### Inventory — 아이템
- **ItemData.cs** (ScriptableObject) : 아이템 한 종 = 에셋 하나. 이름/아이콘/설명/희귀도/칸 크기(gridW×H)/소비 효과/쿨타임(`cooldownSeconds`, 0=기본 30초)/장신구 보너스.
  - **새 아이템 추가 = Resources/Items 아래에 에셋 생성이 전부.** 도감·세이브·상점 판매가 자동으로 인식.
- **ItemDatabase.cs** : id→ItemData 사전 + `All()`(도감용 전체 목록).
- **Inventory.cs** : 타르코프식 그리드(기본 4×4, 주머니 확장 5×5/6×6). 엔트리 목록 `slots` = {item, count, x, y, rot}. 핵심 API: `Add`(자동배치, 넘치면 반환)/`Remove`/`CountOf`/`CanPlace`/`Place`.
- **Equipment.cs** : 장신구 3×3 그리드. 착용 보너스는 `GameManager.SetEquipBonuses`로 반영.
- **ItemPickup.cs** : 바닥 드랍(줍기 F). `SpawnWorld(item, count, pos, …)`로 코드에서 드랍 생성.

### UI — 화면
| 파일 | 무엇 |
|---|---|
| **UITheme.cs** | ★공용 팔레트+그리기 헬퍼. `DrawPanel`(고급 프레임) `DrawHeader`(통일 헤더) `DrawSlot` `RarityRing`(등급별 장식) `TipFrame`(툴팁) `Divider`/`Diamond`/`Corners`(장식) |
| **StatUI.cs** | HUD(나인 솔즈풍): 메달리온+골드 배지+HP 세그먼트+XP 게이지+포션 차지+Q핍+예지 눈. 팔레트는 파일 상단 상수 |
| **Hotbar.cs** | 숫자키 1~3 소비 아이템(인벤 우클릭으로 등록) |
| **InventoryUI.cs** | B키 배낭(그리드 드래그·R회전)+후드(스탯) 탭. 우클릭 메뉴/툴팁 |
| **InvGridGUI.cs** | 인벤 그리드를 다른 UI(상점·잠긴 문)에 그려주는 **공용 렌더러** |
| **ShopUI / CraftingUI / QuestBoardUI / QuestLogUI** | 상점·제작대·의뢰 게시판·퀘스트 로그 |
| **HandbookUI.cs** | G키 핸드북(풀스크린): 지도/도감/도움말 탭, Q/E 전환. 도감 발견 기록 static `seenItems` |
| **DialogueUI.cs** | VN 대화창. `DialogueUI.Show(이름, 초상화, 줄들, 완료콜백)`. 연출 태그 `[놀람]` `[흔들림]` `[떨림]`을 대사 맨 앞에. Ctrl 홀드 스킵 |
| **HelpPopupUI.cs** | ★도움말 카드(야숨식): 화면 딤+**시간 정지**+인벤 크기 카드(위 설명/아래 GIF), [F]로 닫기, 큐 처리. `Show(gifId, 제목, 본문)`. `[키]`·`*강조*` 금색 하이라이트, `Seen` 기록. 각성 패링 큐만 스티키 배너 유지 |
| **Toast / AcquireFeed / AcquireBanner** | 상단 알림 / 우측 획득 피드 / 중앙 획득 배너 |
| **MenuUI.cs** | ESC 일시정지 |
| **GameOverUI.cs** | 튜토 사망 게임오버(다시 시작=인트로부터/타이틀로) |
| **DamagePopup.cs** | 데미지(빨강)/회복(초록) 플로팅 숫자. `DamagePopup.Damage(위치, 값)` |
| **LockedDoorUI.cs** | 잠긴 문 — 열쇠를 그리드에서 집어 구멍에 꽂기 |
| **Minimap / MapScanner / AreaTitle** | 우상단 미니맵(,키) / 지도 텍스처 생성 / 씬 진입 지역명 배너(**씬 이름→표기는 AreaTitle.Resolve 표에서 수정**) |

### World — 씬 배치물
| 파일 | 무엇 |
|---|---|
| **BattleArena.cs** | 전투 방: 존(트리거)에 들어와 적이 인식하면 문이 닫히고, 전멸하면 열림+보상(`onClearActivate`). `startClosed`=처음부터 닫힌 게이트. **문 벽은 반드시 TilemapCollider2D+Ground 레이어** |
| **TreasureChest.cs** | 보물상자(F 개봉, loot[] 드랍, 열림 상태 세이브 연동) |
| **LockedDoor.cs** | 잠긴 문(열쇠 필요) → LockedDoorUI로 연결 |
| **FakeWall.cs** | 플레이어가 들어가면 흐려지는 가짜 벽(비밀 통로) |
| **PrecogCharm.cs** | 장신구 '예지안' 효과 + 붉은 눈빛 VFX |
| **ShopStation / CraftStation / EngineerStation / QuestBoard** | F 상호작용 → (NpcDialogue 대화 후) 해당 UI 열기 |
| **NpcDialogue.cs** | NPC 대사 데이터(이름/초상화/줄들). 스테이션에 같이 붙이면 대화 먼저 |
| **SceneDoor / GatheringSpawn / HelpTrigger / Spike / MovingPlatform / CrumblingPlatform / BouncePad / OneWayPlatform** | 씬 이동 문 / 채집 스폰 / 구역 도움말 / 기믹들 |
| **MapDiscovery.cs** | 탐험한 CameraZone 기록(미니맵/지도의 fog-of-war) |

### Camera
- **CameraFollow.cs** : 부드러운 추적 + 줌(`zoomMul`, 0.78=22% 줌인) + 룩어헤드(`lookAheadFrac`) + 경계 클램프 + 셰이크.
- **CameraZone.cs** : 방마다 BoxCollider2D로 카메라 범위 지정(겹치면 priority). `zoomMul`>0이면 그 방만 다른 줌.

### Save
- **SaveData.cs** : 저장되는 필드 정의(JSON). **새 필드를 추가해도 옛 세이브는 기본값으로 읽혀서 호환**된다.
- **SaveSystem.cs** : 3슬롯. `NewGame`/`LoadGame`/`SaveCurrent`. 씬 이동·일시정지에서 자동 저장. 흐름: 저장=`Capture(data)`, 복원=`Apply(data)`(씬 로드 후). **뭔가 저장하고 싶으면 SaveData에 필드 추가 → Capture/Apply에 한 줄씩.**
- **StartMenu.cs** : 타이틀 화면(새 게임/불러오기/난이도 선택).

---

## 4. 주요 흐름 (무슨 일이 어떤 순서로 일어나나)

**게임 시작**
```
StartScene(StartMenu) → 새 게임 클릭 → SaveSystem.NewGame(슬롯, "TutorialScene")
  → IntroPending=true → 씬 로드 → IntroCutscene(암전·독백·딸피) 
  → TutorialSequence(접근몹→경고 대사→발도→아레나) → CombatTutorial(각성 패링 레슨)
```

**전투 한 사이클**
```
적: Patrol → (감지) Chase → (사거리+쿨) BeginAttack → Windup ─ WindupStarted 이벤트 발생
                                                        ↓
플레이어가 우클릭(가드 시작) 중 피격 → PlayerController.TakeDamage
  ├─ 저스트 구간(0.18s): 적 그로기+반격+Q쿨 초기화+강연출
  ├─ 그 외 패링 창: 쳐내기(피해 무효)
  ├─ 패링 직후 0.35s: 연속 추가타 자동 쳐내기
  └─ 실패: 하트 감소(GameManager.TakeDamageHalves) → 0이면 사망
적 피격: Enemy.TakeDamage → DamagePopup(빨강) → 0이면 사망+loot 드랍
```

**상호작용**: `PlayerInteractor`가 콜라이더 감지 → F → `IInteractable.Interact()` → (대화) → UI 열림 → `Inventory.~Open=true` → 입력 잠금.

**저장/복원**: 씬 이동·일시정지 → `SaveCurrent()`(Capture) / 씬 로드 완료 → `Apply()` — 스탯·인벤(배치 좌표까지)·장비·퀘스트·도감·도움말·열린 상자.

---

## 5. 자주 하는 작업 레시피

- **아이템 추가**: Project 창 → Resources/Items → 우클릭 Create → ItemData 에셋 → 이름/아이콘/희귀도/칸 크기/효과 입력. 끝(도감·세이브 자동).
- **상점 품목**: ShopUI의 상인별 품목 배열 수정.
- **제작 레시피**: CraftingUI의 레시피 목록에 추가.
- **적 추가**: Enemy_Melee 프리팹 복제 → 스탯/loot 수정. 행동이 다르면 Enemy 상속 클래스 작성 후 컴포넌트 교체.
- **대사 수정**: 씬의 NpcDialogue / IntroCutscene / TutorialSequence **인스펙터**에서. 연출은 줄 앞에 `[놀람]` 등.
- **초상화 추가**: Resources/Portraits/에 `화자이름.png`(Sprite로 임포트) — 자동 적용.
- **UI 색 변경**: UITheme.cs 상단 팔레트. HUD만은 StatUI.cs 상단 상수.
- **지역명 표기**: AreaTitle.Resolve의 switch에 씬 이름 추가.
- **퀘스트 추가**: QuestManager.BuildQuests에 정의 추가.
- **도움말 카드 추가**: HelpTrigger(구역 진입) 배치 or 코드에서 `HelpPopupUI.Instance.Show("gif_id", 제목, 본문)`.
- **도움말 GIF 넣기**: GIF를 **프레임 PNG들로 추출**(ezgif.com 'split' 등) → `Assets/Resources/Help/<id>/000.png, 001.png…` 로 저장하면 해당 카드 하단에서 자동 루프 재생(기본 10fps, HelpPopupUI.gifFps). id 목록: attack(공격) parry(패링·그로기) chest(보물상자) arena(배틀 아레나) locked_wall(잠긴 문) fake_wall(비밀 통로) loot(전리품) charge_jump(차지점프) hotkeys(단축키). 파일이 없으면 "시연 영상 준비 중" 표시.
- **밸런스**: 플레이어(공격력 10·패링 창)=Player 프리팹 인스펙터 / 적(체력 60·공격력)=각 Enemy 프리팹 / 반사탄=EnemyProjectile 프리팹.

---

## 6. ⚠️ 함정 목록 (실제로 당했던 것들)

1. **플레이 모드 중 씬 편집은 저장 안 된다** — 정지하면 사라짐. 씬을 만지기 전 반드시 플레이 정지 확인.
2. **플레이 중 스크립트 저장(재컴파일) 금지** — 도메인 리로드로 싱글톤 static이 전부 죽어 세션이 이상해진다("상점이 안 열려요"의 정체). 정지 후 컴파일.
3. **public 필드의 코드 기본값을 바꿔도 씬/프리팹엔 옛 값이 남는다** — 이미 배치된 인스턴스는 직렬화된 값을 쓴다. 코드와 함께 씬/프리팹 인스펙터 값도 갱신할 것.
4. **OnGUI에서 `GUI.color`를 바꿨으면 반드시 `Color.white`로 복원** — 안 하면 이후 그리는 모든 것이 그 색으로 물든다(상점이 새까매졌던 사고).
5. **GUIStyle은 공유 객체** — 한 곳에서 `style.normal.textColor`를 바꾸면 그 스타일을 쓰는 다른 라벨도 바뀐다. 용도별 전용 스타일을 만들거나 쓰고 나서 복원.
6. **타일맵으로 만든 벽/게이트는 그림일 뿐** — 막으려면 TilemapCollider2D 추가 + 레이어 Ground. (아레나 벽 통과 사고)
7. **`Mathf.SmoothStep(a,b,t)`는 셰이더의 smoothstep이 아니다**(a→b 보간). 가장자리 페이드는 `t*t*(3-2t)` 수동 계산.
8. **세이브 필드 추가는 안전, 삭제/개명은 위험** — JsonUtility는 없는 필드를 기본값으로 읽지만, 이름이 바뀌면 데이터를 잃는다.
9. **DestroyImmediate/일괄 삭제 전엔 커밋** — 에디터 스크립트로 대량 삭제하다 타일맵을 날린 적 있다. 위험한 작업 전 git 커밋.
10. **자동부팅 싱글톤에 비직렬화 배열이 있으면 지연 초기화** — 플레이 중 재컴파일 후 null이 될 수 있다(EnsureInit 패턴 참고: StartMenu).
11. **레터박스(컷씬) 중 HUD는 자동으로 숨는다** — `if (Letterbox.Covering) return;`이 각 HUD OnGUI 첫 줄에 있다. 새 HUD를 만들면 같은 줄을 넣을 것.
12. **컷씬 잠금은 `cutsceneActive`** — 여러 컷씬이 이어질 때 한 프레임이라도 풀리면 입력이 새어들어간다(걷기 애니 사고). TutorialSequence의 WaitForEndOfFrame 재잠금 패턴 참고.

---

## 7. 사운드 넣는 법 (배선 완료 — 파일만 넣으면 됨)

`AudioManager`(Core)가 자동부팅되고 게임 곳곳에 훅이 이미 박혀 있다. **오디오 파일을 아래 경로에 정해진 이름으로 넣기만 하면 소리가 난다**(코드 수정 불필요, 파일이 없으면 무음으로 무시):

```
Assets/Resources/Audio/SFX/   ← 효과음(.wav 권장)
Assets/Resources/Audio/BGM/   ← 배경음(.ogg 권장, 루프)
```

| SFX 파일명 | 언제 |
|---|---|
| swing | 플레이어 공격 휘두름 |
| hit | 적 타격(히트스톱과 동시) |
| parry_just | 저스트 패링 "팅" |
| deflect | 일반 쳐내기 |
| player_hit | 플레이어 피격 |
| dash | 대시 |
| enemy_die | 적 사망 |
| pickup | 아이템 줍기 |
| potion | 포션 사용 |
| chest_open | 보물상자 |
| unlock | 잠긴 문 해제 |
| door_slam / door_open | 아레나 게이트 쾅/개방 |
| levelup | 레벨 업 |
| acquire | 획득 배너(모듈 등) |
| gameover | 게임 오버 |

**BGM 파일명**: title(타이틀) / tutorial / village(마을) / stage(지하 1~3·MainMap) / boss — 씬이 바뀌면 자동 크로스페이드. 씬↔곡 매핑은 `AudioManager.SceneBgm()` 표에서 수정.

코드에서 새 소리를 추가하려면 `AudioManager.Sfx("이름")` 한 줄(+위 표에 기록). 볼륨은 `AudioManager.MasterVolume/BgmVolume/SfxVolume`(PlayerPrefs 저장) — 설정 UI를 만들면 이 값만 조절하면 된다.

---

## 8. 디버깅 팁

- **콘솔부터**: 빨간 에러가 하나라도 있으면 그 UI/시스템 전체가 침묵할 수 있다(OnGUI 예외 = 창이 안 그려짐).
- **어디서 생긴 오브젝트인지 모르겠다** → 이 문서 1-①의 자동부팅 목록 확인. 하이어라키의 DontDestroyOnLoad 섹션에 모여 있다.
- **값이 코드와 다르게 동작한다** → 십중팔구 씬/프리팹에 직렬화된 옛 값(함정 3). 인스펙터를 확인.
- **세이브 꼬였다** → `%USERPROFILE%\AppData\LocalLow\<회사명>\<게임명>\save_0.json` — 그냥 텍스트라 열어서 보거나 지우면 된다.
- 특정 상황 재현이 어려우면 GameManager의 `showDebugStats`, Enemy 기즈모(씬 뷰에서 감지 범위 표시) 활용.
