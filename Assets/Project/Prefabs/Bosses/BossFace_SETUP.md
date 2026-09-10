# 최종 보스 얼굴 설정

## 원본과 표시
원본: Assets/FinalBossFaceAtlas.png (1254×1254). 상위 가상현실 공모전/Assets 폴더에서 현재 Unity 프로젝트로 원본 그대로 복사했다. SHA256: 22AB71B10D735055B0751B340DA6F2F30750BBA92B5DC2A860AE04AB38EFF9BC.
Boss_GiftBox.prefab의 Face Atlas와 BossFace.mat BaseMap에 연결하고 approvedAtlasAssigned를 활성화했다. 이전 4×4 PNG는 보존했다.

Texture Import: Default / 2D, sRGB On, Read/Write On, NPOT None, Max Size 2048, Compression None, Mipmap Off, Bilinear, Wrap Clamp. Read/Write는 최초 1회 표정별 눈 마스크 분석에 필요하다. 새 Shader, Animator, AnimationClip, Component는 추가하지 않았다.

## 4열 × 3행 순서 및 원본 여백
1 Idle, 2 BattlePhase2, 3 BattlePhase3, 4 SummonPrepare,
5 SummonActivate, 6 SummonEnd, 7 EnergyCharge, 8 EnergyFire,
9 Mock, 10 Hit, 11 Rage, 12 FinalRage.

주의: 원본은 정사각형 안에 여백을 둔 4열×3행 그림이다. 이미지 높이를 단순히 3등분하면 12번 오른쪽 눈 꼭대기(y=824)가 8번 셀(0~836)에 들어간다. 따라서 Sprite Editor의 Grid By Cell Count 4×3 자동 슬라이스를 적용하지 않는다.
현재 MeshRenderer에는 Sprite subasset이 필요 없다. GetAtlasRect에서 원본 여백을 고려한 같은 크기의 정사각형 UV 창 12개를 사용한다. UV 원점은 왼쪽 아래이며 폭·높이는 0.25, x=열×0.25, y=0.67-행×0.28이다. 행·열은 0부터 시작한다. 픽셀 기준 폭·높이 313.5, x=열×313.5, 아래쪽 y=840.18/489.06/137.94.
정사각형 창과 모델 bounds 안에 들어가는 정사각형 표시 영역을 사용해 원본 눈·입 비율을 유지한다. 표정마다 크기를 바꾸지 않는다. 크기는 Face Size 범위 안에 맞추며 위치는 기존 Face Offset/Local Euler/Surface Offset을 사용한다.
원본 12표정의 주요 흰색 연결 영역 및 동공을 분석해 이 UV 창 밖으로 잘리는 특징이 없음을 확인했다. Mipmap/압축을 끄고 셀 가장자리의 검은 여백을 확보하여 이웃 표정 bleeding을 방지한다.

## 눈과 입 분리
얼굴은 URP Unlit MeshRenderer 1개, runtime material 1개, 64×64 분할 mesh 1개로 표시한다. BuildEyeMasks가 표정마다 세 개의 가장 큰 흰색 연결 영역을 찾아 가장 아래쪽을 입, 나머지를 좌우 눈으로 분류한다. 작은 흰 동공과 검은 동공 내부도 눈 이동 마스크에 포함한다. PNG는 수정하지 않는다.
표정별 눈 마스크는 최초 생성 시 한 번만 계산한다. 입에서 8px 이내인 메시 정점은 고정한다. 따라서 입을 가로지르는 공통 Rect나 얼굴 전체 Scale로 눈 움직임을 흉내 내지 않는다. 눈 픽셀 주변 검은 여백의 이동 가중치를 부드럽게 낮춘다. 마스크 생성 중 feature 인식에 실패한 셀은 눈 애니메이션만 비활성화한다.

## 표정과 Eye Animation 상태
CurrentExpression과 CurrentEyeAnimation은 별개다. 한 LateUpdate에서 원래 메시 정점으로부터 눈 애니메이션을 계산하므로 Coroutine 충돌이나 변형 누적이 없다. 표정이 바뀌면 transient 눈 애니메이션을 초기화하고 새 상태를 평가한다. 피격 후 충전으로 돌아오면 원래 chargeStarted 기준 진행률로 계속 좁힌다.

- Idle: 2.5~5초 랜덤 대기, 0.22초 동안 Y 1→0.90→1.
- SummonPrepare: 0.2초에 걸쳐 Y 0.90, 미세한 안쪽/아래 이동과 소환 방향 look.
- SummonActivate: 0.2초 동안 Y 0.90→1.05→1, 바깥쪽/아래로 미세 impact.
- SummonEnd: 거의 정지, 한쪽으로 0.3% 이동.
- EnergyCharge: 충전 전체 시간에 EaseInOut으로 Y 1→0.82.
- EnergyFire: 0/0.06/0.14/0.22초에 Y 0.82/1.10/0.93/1.
- Hit: 0.18초에 한 번 0→양수→음수→0 흔들림, 표정은 0.2초.
- Rage: 1.5~3초 대기 후 0.22초 미세 반대 방향 twitch.
- FinalRage: 0.8~1.8초 대기 후 0.22초 twitch, Y 0.92~1.0.

눈 랜덤은 별도의 System.Random을 사용해 전투/소환의 Unity Random 상태를 바꾸지 않는다. 특수 동공은 생성하지 않았다.

## 기존 전투 연결/API
SetExpression(Expression), SetBattlePhase(int), BeginSummonPrepare(worldTarget), CancelSummon(), BeginEnergyCharge(duration), NotifyEnergyFired(), NotifyAttackSuccess(), EndAction().
Hit 0.2초, Mock 0.65초, Fire 0.4초 후 현재 진행 중 행동/기본 얼굴을 표시한다. 임시 표정 중에도 행동 시계는 진행한다. HP 50% 이하 Rage, 20% 이하 FinalRage. 사망은 FinalRage, 정화 시작 시 얼굴 숨김을 유지한다.
Director 소환 대기의 마지막 최대 0.8초에 Prepare. 기존 동기식 spawn 시 Activate를 표시하고 최소 0.2초 후 End 0.8초를 표시한다. 실제 생성 시각과 대기 설정은 변경하지 않았다. 첫 대기 0이면 Prepare 없이 생성하며 짧은 대기에서는 Prepare도 짧아진다. 뚜껑 열림 애니메이션은 기존 코드에 없다.
기존 페이즈 2/3·1/3 이동 이벤트 연결을 유지한다. HP 1/3의 Phase3 기본 얼굴은 HP 50% Rage 우선순위에 따라 Rage로 표시된다.
에너지탄 및 플레이어 피해 성공 이벤트는 현재 보스 코드에 없어 API까지만 제공한다. 기존 코어 슬램/스핀에 잘못 연결하지 않았다.
EnemyHealth.HitRegistered/HealthChanged를 읽으며 Fusion 프록시 HP 감소도 피격 표시로 반영한다. Boss Director는 Instantiate하는 로컬 보스 구조이며 동기화된 Boss Pattern 필드는 없다. 새로운 Networked/RPC를 추가하지 않았다. 중요한 패턴 상태의 전 클라이언트 일치는 기존 구조만으로 보장할 수 없으며 실제 멀티플레이 검증과 기존 보스 동기화 작업이 필요하다.

## 검증 범위
원본 복사 SHA256 일치, 12개 UV 창의 특징 잘림 검사, C# 프로젝트 빌드를 수행한다. Unity Play Mode의 실제 보스 배치·가독성, 마스크 변형, 소환/연속피격 복귀, 두 클라이언트 표시는 별도로 확인해야 한다. 외부 dotnet 빌드는 Unity 에디터의 임포트/플레이 검증을 대체하지 않는다.

## Editor Play Mode 얼굴 테스트
Play Mode에서 보스의 FinalBossFaceController Inspector를 열고 Face Preview 섹션을 사용한다.
- Editor Face Preview On: Editor Expression Number 1~12로 원본 순서 직접 선택. HP/행동 표정보다 우선 표시하며 실제 패턴은 계속 진행한다.
- Editor Animate Eyes Off: 정지 원본 비교. On 후 컴포넌트 우클릭 > Face Preview > Replay Selected Eye Animation으로 재생.
- Show Selected / Next Expression / Previous Expression: 선택 재생 또는 순서대로 탐색.
- 4/5/6 소환 준비·활성·완료, 7 충전(Editor Charge Seconds 기본 2초), 8 발사, 10 피격, 11/12 즉시 광폭 twitch를 확인한다. Idle도 즉시 한 번 squint한다. 재생 후 선택한 표정은 유지되므로 짧은 표정을 놓치지 않는다.
- Stop — Return To Live State 또는 Editor Face Preview Off: 현재 실제 HP/행동 표정으로 복귀. 테스트 시작 전의 오래된 상태를 복원하지 않는다.
테스트는 Unity Editor Play Mode에서만 작동한다. #if UNITY_EDITOR로 필드·메뉴·덮어쓰기를 빌드에서 제외한다. 키 입력은 추가하지 않아 게임 조작과 충돌하지 않는다. 사망/정화로 숨겨진 얼굴을 강제로 되살리지는 않는다.

## 2026-09-10 Play Mode 재검증 결과
UV 좌우 반전 및 눈 마스크 이동 부호 수정이 이미 적용된 것을 확인하여 컨트롤러를 추가 수정하지 않았다. 기존 12표정, 소환 단계, 피격 0.2초, HP 50%/20%, 눈 애니메이션 및 Editor ContextMenu를 유지했다.
Unity 6000.5.2f1의 실제 Play Mode에서 Boss_GiftBox 프리팹의 시각 전용 인스턴스와 실제 FinalBossFaceController를 사용했다. 임시 검증 씬에서는 전투/네트워크 컴포넌트를 실행하지 않았다. 종료 후 Dreamland_map_3 씬 구성을 복원했다.
정지 12장과 0/0.06/0.11/0.14/0.18/0.22/0.5/1/2초의 애니메이션 108장을 렌더링했다.
- 1~12 표정과 상하 행 순서 정상. 3/9/11/12의 비대칭 방향이 원본과 일치한다.
- 원본과 정지 렌더의 흰색 실루엣 겹침 비율은 96.6~97.9% (해상도·필터 경계 차이 포함)이며 모든 표정에서 좌우 반전 비교보다 높다.
- 입 연결 영역과 주변 1px을 각 9개 시점에서 비교했을 때 변화 픽셀 0, 최대 채널 차이 0.
- Prepare 안쪽 이동, Activate 바깥쪽 이동, End 한쪽 보기, Hit 오른쪽→왼쪽→복귀, Rage/FinalRage 반대 방향 twitch를 확인했다.
- 얼굴 Transform 위치/크기/회전은 모든 캡처에서 동일했다. 원본에 그려진 표정별 비대칭·실루엣 크기 차이는 유지된다.
- 정면 320×320 렌더에서 특징 잘림과 인접 표정 bleeding을 발견하지 않았다. VR 거리·비스듬한 시점·실제 전투 장면 검증까지 수행한 것은 아니다.
- dotnet 빌드: 오류 0, 경고 71. Unity가 변경된 스크립트를 컴파일하고 Play Mode 캡처를 완료했다.
PNG SHA256은 기존 22AB71B10D735055B0751B340DA6F2F30750BBA92B5DC2A860AE04AB38EFF9BC와 동일하다.
검증 산출물은 Library/BossFaceVisualQA/static.png, frame-02.png, frame-09.png, pixel-analysis.txt, transforms.csv에 있다. Library는 Git 추적 대상이 아니며 삭제/재임포트 시 산출물이 사라질 수 있다. 임시 자동 실행 스크립트는 Assets에서 제거했고 재검증용 사본만 해당 Library 폴더에 보관했다. 상시 테스트 기능은 컨트롤러의 UNITY_EDITOR 메뉴만 남겼다.

## 2026-09-10 실제 보스전 이벤트 연결 검증
FinalBossFaceController, PNG, 디자인/UV/눈 애니메이션 값, 전투/네트워크 소스는 이 단계에서 변경하지 않았다. 실제 Dreamland_map_3 씬의 기존 DreamlandGameFlowController.TestStartBossBattle 경로를 Play Mode에서 호출했다. 시각 전용 프리팹이 아니라 Director가 생성/설정한 실제 보스와 기존 하수인 스폰 루틴을 사용했다. 테스트 피해는 임시 Editor 도구에서 EnemyHealth.TakeDamage(DamageInfo)를 호출해 넣었으며 HP 필드를 직접 덮어쓰거나 소환 간격을 변경하지 않았다. 테스트 종료 후 Play Mode를 종료했다.

|검증|실행 결과|
|---|---|
|Idle|23.099초 Fighting 진입, HP 7600, Idle, Renderer enabled=True|
|소환 준비|24.300초 SummonPrepare|
|준비 중 피격|24.401초 TakeDamage(1), 24.420초 Hit 관측, 24.606초 SummonPrepare 복귀. 입력부터 복귀까지 0.205초|
|첫 실제 소환|25.102초 드론 누적 6마리, SummonActivate; 25.305초 SummonEnd; 26.106초 Idle|
|HP 50%|27.113초 기존 피해 API로 HP 3800/7600, Hit 후 27.320초 Rage|
|50% 소환 복귀|35.104초 누적 12마리와 Activate; 35.312초 End; 36.118초 Rage|
|HP 20%|38.100초 기존 피해 API로 HP 1520/7600, Hit 후 38.314초 FinalRage|
|20% 소환 복귀|44.315초 Prepare; 45.114초 누적 18마리와 Activate; 45.318초 End; 46.123초 FinalRage|

시각 상태의 관측 시각에는 Editor update/프레임 간격이 포함된다. Hit 테스트는 SummonPrepare 중 수행했으며 Activate/End 각각에 대한 피격 주입은 별도로 수행하지 않았다. 코드상 피격은 행동 타이머를 바꾸지 않고, 완료된 단계로 되감지 않고 현재 진행 상태를 표시한다. 상자 뚜껑 열림 애니메이션은 기존 패턴에 없으며 검증한 Activate 기준은 실제 적 Spawn 성공이다.

### VR
XRSettings.isDeviceActive=False, loadedDeviceName 비어 있음. 실제 HMD 착용 상태의 방향/거리/스테레오/가독성 검증은 미수행이다. 실제 보스전에서 얼굴 Renderer 활성 및 부모 추종은 확인했지만 이를 VR 검증 통과로 간주하지 않는다. 보스의 기존 점프/스쿼시/페이즈 이동에 따라 얼굴 월드 위치·스케일도 함께 바뀌는 것은 기존 연출이다.

### Photon Fusion: 주요 상태 일치 보장 불가
런타임 실제 보스 GetComponent<NetworkObject>() 결과는 False였다. FinalBossDirector.SpawnBossObject는 Instantiate, Director/AttackController는 MonoBehaviour이며 보스 패턴을 전달하는 Networked 필드나 RPC가 없다. 이 보스의 EnemyHealth는 네트워크 Spawned 상태가 아니므로 로컬 fallback HP를 사용한다.
DreamEnemySpawner.SpawnEnemy는 실행 중인 Shared 마스터에서만 Runner.Spawn을 호출한다. 이번 테스트에서는 드론 6/12/18마리 생성 성공을 관측했다. 반면 비마스터 경로는 null을 반환하고 Director는 CancelSummon을 호출한다. 따라서 같은 하수인을 보더라도 비마스터 얼굴은 Activate/End를 동일하게 재생하지 못하는 코드 경로가 존재한다. 로컬 보스 HP/페이즈도 서로 동일하다고 보장할 수 없다.
두 클라이언트 동시 비교는 수행하지 않았다. 8번 요구사항은 미통과/구조적 미보장으로 분류한다. 얼굴 값을 바꾸거나 임의 RPC를 추가해 가리지 않았다. 해결에는 보스의 네트워크 생성/권한 및 기존 패턴 상태 전달 경로를 먼저 정하는 별도 작업이 필요하다.

### 에너지탄/플레이어 명중 조사
- FinalBossAttackController의 실제 직접 공격: SlamAttackRoutine/SpinAttackRoutine -> DamageCore -> CoreState.TakeDamage. 플레이어 피해 확정 경로가 아니다.
- spinChargeEffectPrefab/PlaySpinChargeEffects는 회전 준비 VFX이며 검은 에너지탄 충전 이벤트가 아니다.
- CoreEnemyProjectile.Spawn은 RangedMinigunEnemy가 호출하며 HitCore에서 코어 피해를 준다. 최종 보스 탄환이나 플레이어 명중 이벤트로 연결할 수 없다.
- 플레이어 무기 탄환의 EnemyHealth.TakeDamage는 보스가 피격되는 방향의 이벤트다. Mock 성공 이벤트로 사용하지 않는다.
- StatusReceiver.TakeDamage는 로그만 남기는 메서드이며 보스 소유의 플레이어 피해 확정 이벤트를 제공하지 않는다.
연결에 부족한 이벤트는 (1) 보스 에너지 충전 시작/충전 시간/취소, (2) 보스 소유 에너지탄의 실제 생성·발사 확정, (3) 보스 공격으로 플레이어 피해가 확정되었음을 공격 주체와 함께 전달하는 이벤트다. 현재 BeginEnergyCharge/NotifyEnergyFired/NotifyAttackSuccess는 외부 호출 가능한 얼굴 API까지만 존재하며 실제 보스 공격 호출자는 없다. 새로운 공격 시스템은 만들지 않았다.

실행 로그: Library/BossBattleQA/trace.txt. 임시 실행 도구는 Assets에서 제거했고 사본은 Library/BossBattleQA에만 보관한다. Library는 Git에 포함되지 않으므로 핵심 결과는 위 표에 기록했다.
