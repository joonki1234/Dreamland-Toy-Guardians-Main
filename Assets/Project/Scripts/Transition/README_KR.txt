FinalFlow Batch 03
==================

이번 묶음에서 연결되는 흐름
---------------------------
Stage 2 완료
-> EnemyAbsorption
-> FullVRTransition
-> BossBattle
-> 보스 처치
-> Ending
-> Finished

포함 파일
---------
1. DreamlandGameFlowController.cs   (기존 파일 교체)
2. DreamlandTransitionController.cs (기존 파일 교체)
3. FinalBossAttackController.cs     (새 파일)
4. FinalBossDirector.cs             (새 파일)
5. EndingDirector.cs                (새 파일)

권장 위치
---------
Assets/Project/Scripts/Transition/

파일 적용 주의사항
------------------
- 기존 DreamlandGameFlowController.cs.meta는 삭제하지 마세요.
- 기존 DreamlandTransitionController.cs.meta는 삭제하지 마세요.
- Transition 폴더 전체를 교체하지 말고 위 .cs 파일만 넣으세요.
- 새 파일 3개의 .meta는 Unity가 자동 생성하게 둡니다.

Hierarchy 설정
--------------
A. 00_SYSTEM/DreamlandTransitionController 오브젝트
   1) DreamlandTransitionController 컴포넌트를 추가합니다.
   2) 기존 DreamlandStageTest 컴포넌트는 비활성화하거나 제거합니다.
   3) 아래 참조를 연결합니다.
      - Game Flow Controller: GameManager
      - Mission UI: [Dreamland Tutorial + Stage1 Prototype]
      - Reality World: 03_REALITY_WORLD
      - Interior Dream: 04_INTERIOR_DREAM
      - Final Dreamland: 05_FINAL_DREAMLAND
      - Portal Effects: 06_PORTAL_EFFECTS

B. GameManager 오브젝트
   1) FinalBossDirector 컴포넌트를 추가합니다.
   2) EndingDirector 컴포넌트를 추가합니다.
   3) DreamlandGameFlowController에서 아래 참조를 연결합니다.
      - Stage 1 Director: 기존 연결 유지
      - Stage 2 Director: 기존 연결 유지
      - Transition Controller: 00_SYSTEM/DreamlandTransitionController
      - Final Boss Director: GameManager
      - Ending Director: GameManager

C. GameManager/FinalBossDirector
   - Game Flow Controller: GameManager
   - Mission UI: [Dreamland Tutorial + Stage1 Prototype]
   - Core: CoreState가 붙은 코어 오브젝트
   - Boss Prefab: 테스트 중에는 None 가능
   - Boss Spawn Point: 테스트 중에는 None 가능
   - Create Prototype Boss When Prefab Missing: 체크

D. GameManager/EndingDirector
   - Game Flow Controller: GameManager
   - Mission UI: [Dreamland Tutorial + Stage1 Prototype]

빠른 테스트
-----------
전체 게임을 다시 플레이하지 않고 다음과 같이 확인할 수 있습니다.

1) Play Mode 시작
2) GameManager의 DreamlandGameFlowController 컴포넌트 메뉴(점 3개)를 엽니다.
3) "테스트 - Stage 2 이후 전환 시작" 실행
   - 포탈 흡수 연출
   - 현실 월드 비활성화
   - 내부/최종 꿈나라 활성화
   - 보스전 진입
4) 또는 "테스트 - 보스전 직접 시작" 실행
   - 프로토타입 보스가 카메라 전방 약 12m에 생성됩니다.
5) 기본 보스 HP는 500입니다.
   PrototypeRayWeapon 기본 피해가 25라면 약 20회 명중으로 처치됩니다.
6) 보스 처치 후 엔딩이 재생되고 Finished 상태가 되어야 합니다.

예상 로그
---------
[GameFlow] 상태 변경: EnemyAbsorption -> FullVRTransition
[DreamTransition] 전체 꿈나라 상태 적용 완료...
[GameFlow] 상태 변경: FullVRTransition -> BossBattle
[FinalBoss] 최종 보스전이 시작됐습니다...
[FinalBoss] BossDefeated 이벤트를 발생시킵니다.
[GameFlow] 상태 변경: BossBattle -> Ending
[Ending] EndingCompleted 이벤트를 발생시킵니다.
[GameFlow] 상태 변경: Ending -> Finished

현재 범위의 한계
----------------
- 실제 Meta Quest Passthrough 종료는 아직 포함하지 않습니다.
- 현재 전환은 아래 씬 루트의 활성 상태를 바꾸는 PC 프로토타입입니다.
  03_REALITY_WORLD
  04_INTERIOR_DREAM
  05_FINAL_DREAMLAND
  06_PORTAL_EFFECTS
- 실제 보스 프리팹과 공격 패턴 대신 테스트용 캡슐 보스를 자동 생성합니다.
