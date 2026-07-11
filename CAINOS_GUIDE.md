# Cainos "Pixel Art Platformer – Dungeon" 부가요소 사용 가이드

타일맵 말고도 이 에셋에는 **프리팹 200여 종 + FX(불꽃/먼지/빛줄기) + 작동하는 장치(문/상자/승강기/트랩) + 셰이더**가 들어 있다.
전부 `Assets/Cainos/Pixel Art Platformer - Dungeon/` 아래에 있다.

## 0. 가장 빠른 시작 — 데모 씬 구경하고 복붙

- `Scene/SC Demo Scene.unity` : 조명·FX·장치가 다 세팅된 연출 데모. **여기서 마음에 드는 오브젝트를 Ctrl+C → 우리 씬에 Ctrl+V** 하는 게 제일 빠르다.
- `Scene/SC All Props.unity` : 전체 프롭 카탈로그(진열장). 이름 모를 때 여기서 찾기.
- ⚠️ 데모 씬을 **플레이해 보기만** 하고, 저장하거나 우리 씬 위에 덮지 말 것.

## 1. 프롭 프리팹 (`Prefab/Props/` — 드래그 앤 드롭)

- 가구/살림: 책장·침대·탁자·의자·선반·항아리·병·술통·상자짝
- 분위기: 해골·뼈·관·거미줄(`Prefab/Spider Web/`)·벽 장식(`Prefab/Wall Deco/`)·돌무더기(Debris)
- 조명 소품: **Torch 01, Candle 01~08, Lamp, Chandelier, Fireplace** — 불꽃 파티클+글로우가 이미 붙어 있어서 놓기만 하면 일렁인다
- 구조물: Platform(부서진 발판 포함), Pillar, Stairs, Window, Ladder
- 배치 후 **Sorting Layer/Order**만 우리 규칙에 맞추면 끝. 충돌 필요하면 BoxCollider2D 직접 추가(대부분 장식이라 콜라이더 없음).

## 2. 작동하는 장치 (스크립트 포함 — 그냥 쓰면 됨)

| 프리팹 | 하는 일 | 쓰는 법 |
|---|---|---|
| `Door Iron/Wood 01`, `Trapdoor` | 열림/닫힘 애니 | 인스펙터 Runtime의 IsOpened 또는 코드에서 `door.IsOpened = true` |
| `Switch 01` | 레버 — 문과 연동 | Switch의 `target`에 Door 드래그 → 플레이어 상호작용 대신 우리 IInteractable로 감싸서 켜도 됨 |
| `Elevator` | 체인 승강기(왕복) | lengthRange(이동 폭)·moveSpeed 조절. 발판이 Rigidbody2D라 그대로 올라탈 수 있음 |
| `Chest 01/02` | 개폐 연출만 | 우리 TreasureChest 시스템이 이미 있으니 **외형 교체용**으로만 |
| `Swinging Blade Trap`, `Spear Trap`, `Spike Ball` | 움직임 연출만(피해 없음!) | **`TrapDamage` 컴포넌트**(우리가 만든 어댑터, `Assets/Script/World/TrapDamage.cs`)를 날/가시에 붙이면 피해가 들어감. damage(하트)·unblockable 조절 |

## 3. FX 재료 (`Material/FX/` + 데모 씬의 FX 오브젝트)

횃불 불꽃, 촛불, 가마솥 연기, 떠다니는 먼지(Dust/Lamp Dust), **빛줄기(Light Shaft)**, 코인 스파크.
- 전부 URP 파티클/스프라이트 셰이더라 **우리 2D 렌더러에서 그대로 동작**한다.
- 새로 만들 필요 없이 데모 씬에서 해당 FX 오브젝트를 복사해 오는 걸 추천(파티클 세팅이 잘 되어 있음).
- 던전 분위기 낼 때: 어두운 방 + Light Shaft 1~2개 + Dust + 횃불 조합이 가성비 최고.

## 4. 셰이더 4종 (`Shader/`) — 우리 프로젝트에서의 진실

| 셰이더 | 우리(2D 렌더러)에서 |
|---|---|
| ASE Sprite Unlit Shadow Mask | ○ 그냥 스프라이트처럼 렌더 |
| ASE Sprite 3D Lit Shadow Mask (+Transparent) | △ 렌더는 되지만 **'진짜 그림자 드리우기'는 안 됨** — 그 기능은 3D 라이트 전용 |
| ASE Sprite Shadow (`MT Shadow`) | ○ **가짜 그림자용** — 아래 AddShadow 참고 |

- **AddShadow(추천)**: 프롭의 SpriteRenderer가 있는 오브젝트에 `AddShadow` 컴포넌트를 추가 → shadowMaterial에 `Material/MT Shadow` 드래그 → 인스펙터의 **Add 버튼** 클릭. 스프라이트 뒤에 비스듬한 그림자 사본이 생겨 입체감이 확 산다(에디터 전용 원클릭, 런타임 비용 없음).

## 5. ⚠️ 절대 하지 말 것

- `Rendering/` 폴더의 **Universal Render Pipeline Asset을 프로젝트 설정에 적용하지 말 것.**
  그건 이 팩 전용 3D 파이프라인이라, 적용하는 순간 **우리 2D 라이트(Light 2D)가 전부 꺼진다.**
  (팩의 '리얼 그림자' 데모는 그 파이프라인 전제 — 우리는 AddShadow 가짜 그림자로 대체)
- `Third Party/Lucid Editor`는 에셋 인스펙터용 라이브러리 — 건드릴 필요 없음.
