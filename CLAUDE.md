# CLAUDE.md — 프로젝트 인수인계 (rG3_team1 / "Project Red Hood")

> 이 문서는 이어서 작업하는 Claude Code를 위한 핸드오프다. **먼저 이 문서를 읽고**, 코드 구조 상세는 [CODE_GUIDE.md](CODE_GUIDE.md), Cainos 에셋 활용은 [CAINOS_GUIDE.md](CAINOS_GUIDE.md)를 참고하라. 사용자와는 **한국어(존댓말)** 로 소통한다.

## 게임 개요
- Unity 6 URP **2D 메트로배니아** 액션 게임 "레드 후드"(가제). 패링 중심 하드코어 전투 + 몬헌식 사냥/제작 루프. 레퍼런스: 스컬, 나인 솔즈, 할로우 나이트. 분위기: 사이버펑크 + 산업폐허 지저(지하) 문명.
- **가장 가까운 목표 = 경황제 대회 10분 데모.** 임팩트 있는 짧은 플레이. 스팀 출시도 염두.
- 데모 서사: 인트로(지상 나가려다 추락+기억상실) → 튜토(회복) → 마을 각성([여울]이 치료) → 메인퀘 → 첫 던전(Metroidvania) → **첫 보스 처치 → [코어] 획득**.
- 핵심 스탯: 체력=하트(반칸 단위, 가드 50% 경감). 스태미나는 제거됨(대시/가드는 쿨타임). 성장: 레벨/XP·개조포인트·후드 모듈·장신구 3×3 그리드.
- 씬: StartScene(타이틀) → TutorialScene → StartingArea(마을/허브) → **Metroidvania(메인 맵)** → 보스는 Metroidvania 안의 한 구역.
- 스토리 캐논 요약: 주인공 [후드]는 기억상실 고아, 기프트=**적응력+"근본" 통찰**로 타 개체 기프트 흡수(보스 처치 시 [코어] 드랍→각성). 100년 전 지상↔지하 전쟁, 지하 패배·기록 삭제. [여울]=은발 감지 기프트 NPC(까칠+친절), 주인공을 발견·치료. 대사/퀘스트는 이 캐논에 맞춘다.

## ⚠️ 반드시 지킬 규칙 (실제로 사고 났던 것들)

### Unity MCP 워크플로우
- `.cs` 편집 후 **사용자에게 "창 포커스" 요청하지 말고** MCP `refresh_unity`(compile=request, mode=force)로 직접 컴파일 트리거. 편집을 **전부 끝낸 뒤 한 번만** 호출(파일마다 X — 매 컴파일이 도메인 리로드). 끝나면 `read_console`(types=["error"], filter "CS")로 에러 확인.
- **컴파일 전 반드시 `EditorApplication.isPlaying` 확인.** 플레이 중 컴파일 → 도메인 리로드로 자동부팅 싱글톤 static이 전부 죽는다(사용자가 "상점/UI 사라졌다"고 인지). **플레이 중이면 컴파일 보류.** 사용자가 플레이 중일 땐 "플레이 종료 감지 마커" 패턴을 쓴다: `EditorApplication.update` 콜백으로 `!isPlaying`일 때 마커 파일 write → Bash `run_in_background`로 마커 대기 → 종료되면 컴파일.
- **플레이 중 씬 편집 금지**(플레이 종료 시 변경 손실). 씬 편집은 플레이 종료 후.

### 씬(.unity) 취급 — 데이터 손실 주의
- **씬을 열거나(OpenScene) 전환하기 전 항상 활성 씬의 `isDirty`를 확인.** dirty(미저장)인데 내 변경이 아니면 **함부로 reload/discard하지 말 것** — 사용자가 에디터에서 편집 중이던 작업일 수 있고, 미저장은 git/stash/Temp 어디에도 없어 **복구 불가**. (실제로 이 사고로 사용자 마을 디자인을 날린 적 있음.) 플레이 종료 후 활성 씬이 내가 마지막에 연 씬과 다르면 = 사용자가 그 사이 다른 씬을 열었다는 뜻.
- 진단용 `EditorSceneManager.OpenScene`은 사용자의 열린 씬을 바꾼다 — 작업 후 되돌리거나 알릴 것.

### Unity 직렬화 함정
- **public 필드 기본값을 코드에서 바꿔도, 씬/프리팹에 직렬화된 값이 코드 기본값을 덮어쓴다.** 기본값 변경 시 프리팹/씬 인스턴스 값도 함께 갱신할 것. (예: `maxJumps` 프리팹은 2인데 특정 씬 인스턴스가 1000으로 오버라이드돼 무한 점프가 났던 적 있음 → 씬 인스턴스 오버라이드를 revert해야 함.)
- 모든 UI는 **IMGUI(OnGUI)** 다(Canvas 없음). `GUI.color`를 바꾼 뒤 반드시 `Color.white`로 복원(안 하면 이후 UI가 전부 그 색으로 물듦 — 상점 UI 검게 깨진 사고).
- `execute_code`가 `success=false`/null을 반환해도 실제로는 실행됐을 수 있음(MCP 재시도로 이중 실행 가능) — 상태를 다시 조회해 확인.

### 작업 스타일
- 사용자는 **아마추어 개발자**(기존 코드 상당수를 ChatGPT/Gemini가 작성해 허술한 부분 많음). 코드뿐 아니라 **유니티 에디터 설정(콜라이더/레이어/Animator/Root Motion/직렬화)에서 잘못될 수 있는 함정**을 짚어줄 것.
- **시키지 않은 큰 작업으로 범위를 넓히지 말 것.** 파일 삭제 등 되돌리기 어려운 건 먼저 물어보기.
- 왜 그렇게 고쳤는지 설명. 변경은 통째로 갈아끼우기보다 명확하게.
- **테스트는 세이브 슬롯 격리 후 삭제**(예: 슬롯 2에 저장·검증·`SaveSystem.Delete(2)`).
- 검증은 가능한 한 **플레이 모드에서 실측**(리플렉션으로 private 상태 읽기 + `EditorApplication.update` 콜백으로 시간에 따라 로깅 → 파일 write → Bash로 확인). MCP 편집기는 호출 사이 Update가 거의 안 도니, 실시간 손맛/lerp/쿨다운은 사용자 실플레이 확인이 필요.

## 코드 구조 (상세는 CODE_GUIDE.md)
- 폴더 소유권: `Assets/Script/` = 이 사용자(player) 코드. `Assets/Scripts/`(s) = 다른 팀원 코드. **헷갈리지 말 것.**
- 대부분 시스템이 **자동부팅 싱글톤**(`RuntimeInitializeOnLoadMethod` + `DontDestroyOnLoad`). UI는 전부 IMGUI.
- **UITheme.cs** = 중앙 팔레트(먹색 + 금테 Accent + 민트 Good). 색 바꾸려면 여기.
- **HelpPopupUI** = 야숨식 도움말 카드(timeScale 0 + GIF). `Show(id,제목,본문)` / `ShowPages(force, HelpPage...)` 다중 페이지 / `ShowOnce`. GIF는 `Assets/Resources/Help/<id>/` 프레임 PNG.
- **Enemy.cs** = 적 베이스(State 머신 + Tick* virtual). RangedEnemy/FlyingEnemy/BossEnemy가 상속. `invuln` 플래그로 무적. 패링당하면 `ApplyGroggy`.
- 세이브: `Assets/Script/Save/`(SaveSystem 3슬롯 JSON, SaveData, StartMenu). 씬 이동/일시정지 시 자동저장.

## 최근 작업 상태 (이 핸드오프 시점)
- **보스 완성**(BossEnemy.cs, "굶주린 흡수체"): 패턴 상태머신(Combo3 평타연타 / Charge 돌진 / Volley 3+1 원거리 / LeapSlam 도약내려찍기+방사충격파 / RedSlash 패링불가 붉은베기 / Roar 페이즈2전환). **패링 불가 공격은 바닥 기준 깜박이는 빨간 경고 박스**. 저스트 패링 3회 누적 → 그로기. **페이즈2 전환(Roar) 동안 무적, 다음 패턴 시작 시 해제.** 패턴 빈도: 평타(Combo3) 위주, 돌진 적게, 이동은 대쉬로 적극적. Metroidvania 안 (약 x283,-20) 배치. 프리팹 `Assets/Prefabs/Boss_Devourer.prefab`.
- **보스 애니메이션 = Hero_Knight 에셋**(`Assets/Hero_Knight/`). 클립·컨트롤러(`Boss.controller`)를 코드로 생성했음. **Attack은 트리거 방식**(각 타격 휘두름마다 `anim.SetTrigger("attack")`) — 연타가 애니를 끊지 않게 각 타를 "휘두름→정점 타격→마무리" 2단계로 나누고 `attackAnimLen`(클립 길이 런타임 조회)에 맞춤. 이동상태(Idle/Run/Jump/Fall)는 상태 간 전환, 공격/피격/사망만 Any State 트리거. `InvertSprite=false`(Hero_Knight는 오른쪽 정방향), Animator `cullingMode=AlwaysAnimate`.
- **타임어택 모드**(축제용): 새 게임 슬롯 선택 화면에서 모드 선택(일반/트레져 헌터/스피드런). GameMode.cs + TimeAttack.cs(타이머 HUD·완료 조건·베스트 기록 PlayerPrefs). 트레져=Metroidvania 상자 전부 개봉, 스피드런=mq_boss 완료. SaveData에 gameMode/playTime/timeAttackDone.
- **메인 퀘스트 체인**: mq_awaken→guide_village→mq_descend→mq_boss(튜토→마을→탐사→첫 보스). QuestManager + MainQuestFlow.
- 최근 수정: 엔지니어 강화창 버그(대화 F 종료가 상호작용에 재사용돼 대화 재시작 → DialogueUI.ClosedAt 0.2s 그레이스로 PlayerInteractor 잠금), 원거리 몹 조준을 발밑→몸통(콜라이더 중심), Q스킬 쿨 완료 연출(확산 링+글로우+맥동), 게임오버 재시작=새 게임(SaveSystem.NewGame), Metroidvania 무한 점프(씬 Player maxJumps 1000→2 revert), 도움말 다중 페이지+전투 억제.

## 알려진 이슈 / 진행 중
- **Metroidvania.unity가 자주 dirty 상태로 남아 있었음** — 사용자가 맵 타일 레이아웃을 직접 편집 중이라 그럴 수 있으니, **저장/reload 전 반드시 사용자에게 확인.**
- 에셋 작업(스프라이트/애니메이션 소스, 도움말 GIF 촬영, 사운드 wav, 배경, 맵 타일 배치, 메인 퀘스트 로고 등)은 **사용자 담당** — 코드/연동은 Claude, 아트는 사용자.
- 사운드: `Assets/Resources/Audio/SFX·BGM/`에 이름 맞춰 wav 넣으면 로드(없으면 무시). AudioManager.

## 컨벤션
- git 커밋 메시지 끝에 `Co-Authored-By: Claude ...` (사용자가 커밋/푸시하라고 할 때만).
- 커밋/푸시는 **사용자가 명시적으로 요청할 때만.**
