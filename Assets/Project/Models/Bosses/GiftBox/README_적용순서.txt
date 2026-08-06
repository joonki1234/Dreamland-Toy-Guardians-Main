DreamGuardians Waspy 비행 드론 적 Patch 06

[이번 패치 내용]
1. Waspy 드론이 포탈 위치에서 코어 주변의 공중 공격 지점으로 이동합니다.
2. 공중에서 위아래로 가볍게 호버링합니다.
3. 코어와 8m 거리를 유지하고 1.25초마다 5 데미지를 줍니다.
4. 공격 순간 청록색 레이저가 잠깐 표시됩니다.
5. 피격 시 Animator의 Hit Trigger를 실행합니다.
6. 사망 시 Animator의 Die Trigger를 실행합니다.
7. 기존 체력, 체력바, 정화, 꿈 에너지, 웨이브 완료 판정에 포함됩니다.
8. Stage 2 Wave 2에 2기, Final Wave에 4기가 추가됩니다.

[파일 적용]
1. Unity의 Play 모드를 종료합니다.
2. 압축 파일 안의 Assets 폴더를 Unity 프로젝트 최상위 폴더에 덮어씁니다.
3. Unity로 돌아가 컴파일이 끝날 때까지 기다립니다.
4. Console에 빨간 컴파일 오류가 없는지 확인합니다.

[DroneEnemy_Waspy 프리팹 설정]
1. Project 창에서 아래 프리팹을 더블클릭합니다.
   Assets/Project/Prefabs/Enemies/DroneEnemy_Waspy.prefab
2. 프리팹 최상위 DroneEnemy_Waspy를 선택합니다.
3. Add Component를 누르고 Drone Enemy Waspy를 추가합니다.
4. Animator 칸은 비워도 자식 Animator를 자동으로 찾습니다.
5. Hit Trigger Name은 Hit, Die Trigger Name은 Die로 둡니다.
6. Collider가 없으면 실행 중 모델 크기에 맞는 BoxCollider가 자동 생성됩니다.
7. Ctrl+S로 프리팹을 저장합니다.

[Stage 2 연결]
1. Dreamland_map_3 씬을 엽니다.
2. Hierarchy에서 GameManager 오브젝트를 선택합니다.
3. Inspector의 Stage 2 Wave Controller 컴포넌트를 찾습니다.
4. Drone Enemy Prefab 칸에 다음 프리팹을 드래그합니다.
   Assets/Project/Prefabs/Enemies/DroneEnemy_Waspy.prefab
5. 기본 수량을 확인합니다.
   Wave 2 Drone Enemy Count: 2
   Final Wave Drone Enemy Count: 4
6. Ctrl+S로 씬을 저장합니다.

[Drone Enemy Waspy 기본값]
Attack Range: 8
Flight Height: 3
Move Speed: 1.8
Turn Speed: 6
Model Yaw Offset: 0
Arrival Tolerance: 0.35
Hover Amplitude: 0.12
Hover Frequency: 2
Core Damage: 5
Attack Interval: 1.25
Beam Width: 0.04
Beam Duration: 0.12

[테스트할 것]
1. Stage 2 Wave 1에는 드론이 등장하지 않는지
2. Stage 2 Wave 2에 드론 2기가 등장하는지
3. 드론이 지상 적과 달리 공중으로 올라가는지
4. 드론이 코어 약 8m 앞에서 멈추는지
5. 공격 순간 레이저가 보이고 코어 체력이 감소하는지
6. 피격 시 Hit 애니메이션이 재생되는지
7. 사망 시 Death 애니메이션이 재생된 뒤 정화되는지
8. Final Wave에 드론 4기가 추가되는지
9. 모든 적을 처치하면 Stage 2가 정상 완료되는지

[방향이 반대인 경우]
드론이 뒤를 보며 이동하면 Drone Enemy Waspy 컴포넌트의
Model Yaw Offset을 180으로 바꿉니다.

[Animator 경고 또는 동작 누락]
Animator Parameters에 아래 Trigger가 대소문자까지 정확히 있어야 합니다.
- Hit
- Die

Any State -> Hit 전환 조건: Hit
Any State -> Death 전환 조건: Die
Hit -> Idle: Has Exit Time 체크, 조건 없음
Death에서는 나가는 Transition 없음
