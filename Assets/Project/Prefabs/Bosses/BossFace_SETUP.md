# Final Boss Face 설정

얼굴은 `FinalBossFaceController`의 로컬 Presentation 기능이다. HP, 데미지,
판정, 타이밍, 소환, 이동, Fusion 및 사망/클리어 흐름을 변경하지 않는다.
보스 프리팹 루트에 컴포넌트가 추가되어 있으며 Atlas/Material은 직접 연결한다.

## Atlas 제작

- 권장 1024×1024, 4×4, 셀 256×256, RGBA PNG.
- 배경은 실제 알파 0. 체크무늬나 회색 배경을 픽셀에 굽지 않는다.
- 눈/입은 흰색 또는 회색조. 셀 경계에 투명 여백을 둔다.
- 15, 16번 셀은 완전 투명. enum 값은 0~13만 사용한다.
- 왼쪽 위부터 오른쪽으로, 다음 행으로 읽는다. 재배열하지 않는다.

| | 열 1 | 열 2 | 열 3 | 열 4 |
|---|---|---|---|---|
| 행 1 | 01 IdlePhase1 | 02 Summon | 03 ApproachPhase2 | 04 IdlePhase2 |
| 행 2 | 05 ApproachPhase3 | 06 IdlePhase3 | 07 SlamWindup | 08 SlamImpact |
| 행 3 | 09 Spin | 10 Hit | 11 DeathShock | 12 DeathWeak |
| 행 4 | 13 CleanseTransition | 14 CleanseGentle | 15 Empty | 16 Empty |

UV scale `(0.25, 0.25)`, offset `(index % 4 / 4, (3 - index / 4) / 4)`.
여기서 index는 0부터 시작하며 행 계산은 정수 나눗셈이다.

## Unity 연결

1. 예: `Assets/Project/Textures/Bosses/FinalBossFaceAtlas.png`에 원본을 추가한다.
2. Import: Texture Type **Default**, Shape **2D**, sRGB On,
   Alpha Source **Input Texture Alpha**, Alpha Is Transparency On,
   Wrap **Clamp**, Filter **Bilinear**, Max Size **1024**, Read/Write Off,
   Mipmap 우선 **Off**, 원본 검수 Compression **None**. 플랫폼 override도 확인한다.
3. Material 생성: Shader **Universal Render Pipeline/Unlit**, Surface Transparent,
   Blending Alpha, Render Face Front, Alpha Clipping Off, Base Color White `(1,1,1,1)`.
   Base Map에 Atlas를 지정한다.
4. `Boss_GiftBox.prefab` 루트의 `FinalBossFaceController`에서 Face Atlas와
   Face Material을 연결한다. Atlas Grid는 `(4,4)`로 고정한다.
5. Face Local Euler `(90,0,0)`은 기존 모델 pitch `-90`을 보정한다.
   얼굴은 모델 로컬 -Y 방향(기존 보스 회전 보정 후 앞쪽)을 향한다.
   카메라를 바라보지 않고 회전/점프/스케일을 부모와 함께 따른다.

초기값: Face Offset `(0,-0.08,0)`, Face Size `(0.8,0.65)`, Surface Offset `0.015`.
위치/크기는 모델을 얼굴 좌표계로 변환한 bounds의 비율이다. 리본과 겹치면
Face Offset의 X/Y 또는 Face Size를 조정하고, 표면에 묻히면 Surface Offset을 조정한다.
실제 Atlas 연결 후 정면 및 리본 겹침을 플레이 화면에서 검수해야 한다.

## 동작 및 연결 위치

- `EnemyHealth.NormalizedHealth` 읽기: HP > 2/3 IdlePhase1,
  1/3 < HP <= 2/3 IdlePhase2, HP <= 1/3 IdlePhase3.
- `EnemyHealth.HitRegistered`: Hit 0.12초. `Died`: DeathShock 0.35초 후 DeathWeak.
- `FinalBossAttackController.PhaseAdvanceRoutine`: 첫/두 번째 접근 시작과 종료.
- `SlamAttackRoutine`: 준비 시작 및 실제 착지 이펙트 위치에서 SlamImpact 0.12초.
- `SpinAttackRoutine`: 준비부터 공격 종료까지 Spin.
- `FinishAttack`, `StopOwnedRoutines`: 공격 표정 해제. 새 접근은 기존 공격 표정을 덮는다.
- `FinalBossDirector.SpawnNextBossMinion`: spawned != null일 때만 Summon 0.6초.
  등장/접근 시 재사용되는 PlaySummonPulse에는 연결하지 않는다.
- `BossDefeatRoutine`: 기존 축소/상승 정화 연출 시작 시 CleanseTransition,
  해당 연출 진행률 50% 이상에서 CleanseGentle. 기존 연출 시간은 늘리지 않는다.
  정화가 즉시 시작되거나 연출 시간이 0이면 일부 사망/정화 표정은 보이지 않을 수 있다.
- 우선순위: Cleanse/Death > SlamImpact/Approach/Attack/Summon > Hit > HP Idle.
  낮은 우선순위 타이머도 실제 시간에 만료되므로 뒤늦게 재생하지 않는다.

새 얼굴은 런타임 자식 `BossFace`, MeshRenderer 1개, Material 인스턴스 1개,
Quad mesh 1개를 사용하며 종료 시 정리한다. Collider/Light는 생성하지 않는다.
새 컴포넌트가 있는 보스에서는 기존 Billboard 눈과 EyeGlowLight를 생성하지 않는다.
Atlas 또는 Material이 비어 있거나 URP/Unlit이 아니면 얼굴만 숨긴다.
모델 전용 bounds/색상/히트박스 순회에서는 얼굴을 제외한다.
RPC나 Networked 필드를 추가하지 않는다. 기존 로컬 보스 이벤트와 EnemyHealth의
기존 동기화 결과를 표시하며 새로운 공격 이벤트 네트워크 전달을 제공하지 않는다.

## 플레이 검수

- PNG 알파 채널을 확인하고 유색 배경 위에서 눈/입만 보이는지 확인한다.
- 14개 셀의 위치와 표정이 위 표와 맞고 15/16이 비어 있는지 확인한다.
- 보스 정면, 리본 겹침, 회전 시 부모 추종, 그림자/빛 미생성을 확인한다.
- 소환 성공/실패, 두 접근, Slam/Spin, 피격, 사망/정화를 실행한다.
- 공격 중 Hit가 공격 표정을 덮지 않고 정화 중 다른 이벤트가 덮지 않는지 확인한다.
- Atlas/Material을 각각 비웠을 때 예외 없이 얼굴만 숨는지 확인한다.
