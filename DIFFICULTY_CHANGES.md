# 난이도 시스템 변경 및 검증

## 구현

- 기존 `GameDifficultyState`와 방장 생성/RPC → State Authority 구조를 재사용했습니다. 기존 저장값을 보존해 Easy=0, Medium=1, Hard=2, Extreme=3입니다. 중의 기존 이름 Medium을 유지했습니다.
- 로비의 실제 기존 UI는 좌우 화살표 방식입니다. 동일한 스프라이트·TMP 폰트·색상 테마를 사용한 하/중/상/최상 직접 선택 버튼을 추가했습니다. 기존 화살표와 배경은 참조를 유지한 채 비활성화했습니다. 모든 새 버튼은 동일한 150×60 크기이며, 권장 인원과 선택 색상이 표시됩니다.
- `Spawned`에서 난이도 네트워크 오브젝트를 Runner의 DontDestroyOnLoad로 등록합니다. `RoomManager.LoadGameplayScene`에서 선택을 잠근 뒤 기존 Fusion 씬 전환을 실행합니다. 튜토리얼·Stage1·Stage2·보스는 Dreamland_map_3의 기존 진행 구조를 사용합니다.
- 웨이브 전체 합계를 반올림(0.5 올림)하고 최소 1마리를 보장합니다. 원래 0마리인 빈 구성은 0을 유지합니다. 혼합 종류/방향은 누적 합계로 분배하여 개별 반올림으로 총수가 증가하지 않습니다.
- 적 HP는 기존 스폰 권한자의 Configure 입력에 한 번 적용하고 기존 EnemyHealth 네트워크 필드로 복제합니다. 보스는 기존 BossCombat State Authority의 BossMaxHealth에만 별도로 적용합니다.
- Core 피해는 기존 DreamlandProgressSync 권한 처리에서 한 번 적용합니다. 근접·미니건 투사체·드론·보스 고정 피해 공격 모두 이 경로를 사용합니다. CoreState의 오프라인 처리는 네트워크 경로와 상호 배타적입니다.
- 직업·무기·스킬·시너지·플레이어 공격 데미지·적 이동속도·공격 주기는 변경하지 않았습니다. 씬/프리팹의 원본 전투 수치는 변경하지 않았습니다. 로비 없이 직접 실행할 때는 기존 중 기준입니다.
- 튜토리얼의 플레이어별 훈련 표적은 기존 소유권/진행 조건을 유지합니다. 표적 HP에는 일반 적 HP 배율을 적용합니다. 보스 본체는 하나이며, 소환 드론 묶음과 동시 소환 한도에는 적 수 배율을 적용합니다.

## 적용 수치

| 난이도 | 권장 인원 | 적 수 배율 | 일반 HP 배율 | Core 피해 배율 | 보스 HP |
|---|---|---:|---:|---:|---:|
| 하 | 1 | 0.5 | 0.7 | 0.7 | 2280 |
| 중 | 2~4 | 1 | 1 | 1 | 7600 |
| 상 | 6 | 1.5 | 1.1 | 1.1 | 11400 |
| 최상 | 8 | 2 | 1.2 | 1.2 | 15200 |

웨이브 개수는 그대로이며 다음은 **각 웨이브의 적 마릿수**입니다. Stage2는 기존 4방향 구성 기준입니다.

| 난이도 | Stage1 1/2/최종 | Stage2 1/2/최종 | 보스 드론 1회 소환 / 동시 한도 |
|---|---|---|---|
| 하 | 6 / 12 / 20 | 10 / 20 / 28 | 3 / 12 |
| 중 | 12 / 24 / 40 | 20 / 40 / 56 | 6 / 24 |
| 상 | 18 / 36 / 60 | 30 / 60 / 84 | 9 / 36 |
| 최상 | 24 / 48 / 80 | 40 / 80 / 112 | 12 / 48 |

일반 HP는 기존 스테이지별 설정에 추가 적용합니다. 기존 근접 적은 웨이브 HP 배율을 사용하지 않는 로직을 그대로 유지했습니다.

| 대상 | 하 | 중(기존) | 상 | 최상 |
|---|---:|---:|---:|---:|
| 근접 HP | 70 | 100 | 110 | 120 |
| Stage1 2차 원거리 HP | 87.5 | 125 | 137.5 | 150 |
| Stage1 최종 원거리 / Stage2 1차 드론 HP | 105 | 150 | 165 | 180 |
| Stage2 2차 원거리·드론 HP | 126 | 180 | 198 | 216 |
| Stage2 최종 원거리·드론 HP | 154 | 220 | 242 | 264 |
| 근접 Core 피해 | 4.2 | 6 | 6.6 | 7.2 |
| 미니건 Core 피해 | 3.15 | 4.5 | 4.95 | 5.4 |
| 드론 Core 피해 | 0.07 | 0.1 | 0.11 | 0.12 |
| 보스 Core 기본 피해(패턴 배율 적용 전) | 14.7 | 21 | 23.1 | 25.2 |

최종 제출용 에디터 테스트 설정은 `enableTestDamageBoost=0`, `testDamageMultiplier=5`입니다. Inspector 표시명은 `Enable Solo Boss Test Damage`이며 코드 기본값도 false입니다. 기존 5배 기능은 유지하고, UNITY_EDITOR와 옵션이 모두 활성화된 경우에만 플레이어의 보스 대상 피해에 적용됩니다. 일반 보스/네트워크 보스 양쪽의 테스트 배율 연산은 #if UNITY_EDITOR 안에 있으므로 PC/Quest 빌드에서는 컴파일되지 않습니다.

## 수정 파일

| 파일 | 이유 |
|---|---|
| Assets/Lobby_Scripts/GameDifficultyState.cs | Extreme, 단일 설정 구조, 씬 간 유지, 선택 잠금 |
| Assets/Lobby_Scripts/LobbySelectionController.cs | 4단계 선택 함수, 권장 인원, 선택 색상 |
| Assets/GameScene/LobbyScene.unity | 직접 선택 버튼 4개와 영구 OnClick 연결, 난이도 영역 배치 |
| Assets/RoomManager.cs | 기존 씬 전환 직전에 공용 난이도 잠금 |
| Assets/DreamlandTutorialStage1/Runtime/DreamEnemySpawner.cs | 전체 웨이브 수 분배, 스폰 HP, 훈련 표적 HP 권한 처리 |
| Assets/DreamlandTutorialStage1/Runtime/DreamEnemySpawner.BossCombat.cs | 보스 권한자의 초기 HP 배율 |
| Assets/DreamlandTutorialStage1/Runtime/Stage1WaveController.cs | 표시하는 웨이브 총수 일치 |
| Assets/Project/Scripts/Transition/Stage2WaveController.cs | 표시/이벤트 웨이브 총수 일치 |
| Assets/Project/Scripts/Transition/FinalBossDirector.cs | 소환 드론 수/한도 배율, 실제 보스 HP 로그 |
| Assets/DreamlandProgressSync.cs | Core 피해 최종 권한 처리 배율 |
| Assets/DreamlandTutorialStage1/Runtime/CoreState.cs | 오프라인 Core 피해 배율 |

## 검증 결과 및 한계

- 초기 `dotnet build Assembly-CSharp.csproj --no-restore`: 오류 0개. 기존 경고 81개.
- 최종 코드: csproj의 전체 소스/참조/define으로 Roslyn C# 컴파일 성공(exit 0). 후속 MSBuild 실행은 환경의 SDK 디렉터리 접근 제한으로 직접 컴파일로 대체했습니다.
- 실제 DifficultySettings 소스를 추출해 실행: 혼합 구성 2500개, 방향별 웨이브 12개, 최소값/반올림, 보스 HP 검증 통과. 독립 실행에서는 Unity Mathf의 Max/FloorToInt를 동일 연산으로 대체했습니다.
- LobbyScene YAML의 중복 ID 없음, 씬 내부 fileID 누락 없음(프리팹 stripped 오브젝트 포함). 4개 OnClick 대상/메서드/정수 인수 0~3 확인.
- `git diff --check` 통과. 플레이어 공격 관련 파일과 EnemyHealth의 데미지 처리 코드는 변경되지 않았습니다.
- Unity 6000.5.2f1 배치 실행 시 `No valid Unity Editor license found`, 종료 코드 198. 따라서 Unity 재임포트/Fusion weaving, 실제 UI 렌더링, 2클라이언트 동기화는 이 환경에서 실행 검증하지 못했습니다. 라이선스 로그: Library/DifficultyVerification.log.

## Unity Editor 확인 경로와 Inspector 필드

1. **LobbyScene** → `02_LOBBY_UI/LobbyCanvas/JobSelectPanel/DifficultyEasyButton`, `DifficultyMediumButton`, `DifficultyHardButton`, `DifficultyExtremeButton`
   - Rect Transform: Width 150, Height 60.
   - Button → On Click(): `LobbySelectionController.SelectDifficulty`, 인수 각각 0/1/2/3.
   - 자식 `Text (TMP)` → Text Input: 하/1인용, 중/2~4인용, 상/6인용, 최상/8인용. 화면에서 글자와 선택 색상 확인.
2. **LobbyScene** → `00_SYSTEM/LobbySelectionManager`
   - Lobby Selection Controller → Difficulty Buttons: 위 4개 버튼 순서.
3. **Play Mode, DontDestroyOnLoad** → `GameDifficultyState(Clone)`
   - Game Difficulty State → Current Difficulty, Selection Locked(필요시 Debug Inspector).
   - 두 클라이언트에서 같은 값인지, 게임 씬 진입 뒤 값이 유지되고 잠기는지 확인.
4. **Dreamland_map_3** → `00_SYSTEM/GameManager`
   - Final Boss Director → Boss Max Health: 원본 7600 유지. 전투 중 보스 HP HUD는 선택에 따라 2280/7600/11400/15200.
   - Stage2 Wave Controller → Wave1/2/Final의 Melee/Ranged/Drone Per Direction: 원본 유지.
5. **Dreamland_map_3** → `[Dreamland Tutorial + Stage1 Prototype]`
   - Dream Enemy Spawner → Base Enemy Health 100, Core Damage 6, Enable Solo Boss Test Damage 비활성, Test Damage Multiplier 5 유지.
   - Stage1 Wave Controller → Groups: 원본 12/12/24, Second Wave Ranged Enemy Count 12, Final Wave Ranged Enemy Count 16 유지.

권장 인원 표시는 방 입장 인원 제한이 아닙니다. 기존 최대 인원 8명 설정은 유지합니다.
