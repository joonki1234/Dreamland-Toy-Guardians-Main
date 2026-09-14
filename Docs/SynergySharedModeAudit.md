# Shared Mode 시너지 조사 및 수정 기록 (2026-09-14)

## 최신 코드에서 확인된 원인

기존의 ‘A에는 Police, B에는 Firefighter 기록만 남는다’는 가설은 **현재 네트워크 일반 적에는 해당하지 않는다**. EnemyHealth는 이미 NetworkBehaviour이며 원격 피해 RPC에 role과 allowSynergy를 실어 State Authority의 ApplyDamageAuthoritative → RoleSynergyTracker.RegisterHit으로 전달한다. Tracker가 MonoBehaviour라는 사실 자체는 기록 분산의 증거가 아니다.

실제 확인된 결함은 다음과 같다.

1. RoleSynergyTracker가 추가 피해와 스턴을 판정한 뒤 로컬 SFX와 static DreamGameEvents만 실행했다. 다른 Peer의 MissionBannerUI와 TutorialStage1Director는 결과를 받지 못한다.
2. 총알·음식·흙의 원격 연출은 enabled=false로만 차단했다. Unity는 비활성 MonoBehaviour에도 물리 콜백을 전달하므로 실제로 중복 공격/장판 생성 경로가 열려 있었다. WaterParticleHit에는 소유권 가드가 없었다.
3. DamageInfo.playerId는 실제 플레이어가 아니라 POLICE_BULLET_PROJECTILE 같은 무기 상수였다. 적의 중복 키는 playerId:shotId여서 서로 다른 프로세스에서 같은 직업의 shotId가 겹치면 정상 공격이 중복으로 버려질 수 있다. Police와 Firefighter의 서로 다른 무기 상수가 직접 충돌하는 문제는 아니다.
4. DirtProjectile은 MudSplat을 로컬 Instantiate했다. 각 Peer의 흙·음식 궤적은 독립적인 랜덤 물리다. MudSplatSynergy는 원격 연출용 음식인지 확인하지 않아 잘못 활성화하거나, 실제 Chef의 음식과 장판이 같은 Peer에 없어 누락될 수 있었다.
5. Mud의 ApplyLure/ApplyStun/ApplyKnockback은 적의 로컬 컴포넌트에만 적용됐다. 적의 실제 이동은 State Authority가 수행하므로 원격 Builder의 유인은 권한 Peer에 전달되지 않았다.
6. RoleSynergyProgression은 Peer별 static이었다. Stage1WaveController의 해금 연출 및 Stage2 직접 테스트에서 각각 Unlock하며, TutorialStage1Director의 Begin과 Stage1 초기화에서 Lock한다. 진행 속도가 다르면 서로 다른 잠금 상태를 사용할 수 있었다.

## 실제 연결 경로

| 대상 | 생성/공격 경로 | 판정 위치 |
|---|---|---|
| 일반/Stage1 적 | DreamEnemySpawner → master 확인 → Runner.Spawn → ConfigureSpawnedEnemy | 스폰한 Shared master의 EnemyHealth |
| Stage2 적 | Stage2WaveController → SpawnDirectionalMixedGroup → 같은 DreamEnemySpawner 스폰 경로 | 같은 master |
| 경찰 기본 공격 | 01_Player / PlayerJobController RPC → GunController → PoliceBulletProjectile → TakeDamage | 공격자 충돌 → 적 State Authority |
| 경찰 스킬 | PlayerJobSkillController → PoliceFocusedFireEffect(dealsDamage) → TakeDamage | 공격자 Tick → 적 State Authority |
| 소방관 기본 공격 | PlayerJobController 물 활성화 RPC → FireHoseController → WaterParticleHit → TakeDamage | 수정 후 소방관 소유자만 보고 → 적 State Authority |
| 소방차 | FireTruckSkillMover(dealsDamage) → TakeDamage | 공격자 감지 → 적 State Authority |
| Builder | 01_Player의 DirtBlock → DirtProjectile / DirtShotContext → MudSplat | 수정 후 Builder의 플레이어 State Authority |
| Chef | ChefWeaponController → ChefFoodProjectile → Mud trigger | 실제 Chef의 음식만 활성화 요청 |
| 보스 | FinalBossDirector.SpawnBossObject → Instantiate 또는 prototype 생성 → GetOrAdd EnemyHealth/Tracker | **네트워크 Spawn이 없는 로컬 보스**, Peer마다 별도 체력·기록 |

Enemy_ToyRobot, RangedEnemy_Minigun, DroneEnemy_Waspy Variant의 NetworkObject / EnemyHealth / EnemyCoreMover 부착을 확인했다. Tracker는 스폰 콜백에서 일반 MonoBehaviour로 추가한다. 01_Player의 DirtBlock 참조, DirtBlock의 MudSplat 참조, MudSplat의 폭발 VFX/활성화 SFX/폭발 SFX 참조도 연결되어 있다.

## 수정 범위

- EnemyHealth의 기존 피해 RPC 유지. 실제 Runner.LocalPlayer 또는 RPC Source로 중복 키의 플레이어 영역을 나눈다.
- Tracker 판정은 State Authority만 실행하고 Fusion SimulationTime을 사용한다. 피해/스턴과 결과 연출을 분리하여 결과 RPC가 다시 피해를 주지 않게 한다.
- 결과 RPC는 각 Peer의 기존 DreamGameEvents 소비자에게 전달한다. 원격 Tracker가 없으면 일반 MonoBehaviour로 생성하고 스포너의 SFX 설정을 적용한다.
- 비활성 투사체의 콜백 가드와 WaterParticleHit의 소유권 가드를 추가한다.
- PlayerJobController의 기존 NetworkBehaviour에 SharedMud NetworkDictionary를 추가한다. 위치, 회전, 단계, Fusion 시간 기준 만료 시각을 복제한다. 신규 NetworkBehaviour/네트워크 프리팹 부착은 없다.
- Chef 소유자의 음식만 소비하고 Builder State Authority에 활성화를 요청한다. 활성화는 idle → activated 전이로 한 번만 승인한다. 유인/폭발은 Builder Authority에서 한 번 실행하며, 적의 상태 변화는 각 적 Authority에 RPC로 전달한다.
- 각 Peer는 복제된 장판 상태로 VFX/SFX를 표시한다. 늦게 입장한 Peer도 살아 있는 장판을 복원한다. transient UI/튜토리얼 이벤트는 과거 이력으로 재생하지 않는다.
- 기존 DreamEnemySpawner가 master의 잠금 상태를 Networked 값으로 공유한다. 오프라인 경로는 기존 static 값을 사용한다.

## 검증 및 한계

- 최종 runtime C# 프로젝트 dotnet build: 오류 0, 기존 경고 71. Unity Logs/Editor.log에서도 수정 후 Fusion IL post-processing 및 어셈블리 재로드를 확인했다. 실제 방 통신은 별도 확인 대상이다.
- `Tools > Dream Guardians > Validate Synergy Regression`: 판정창 경계, 단일 직업, 쿨다운, 플레이어별 중복 키, 연출용 충돌 차단, 연출용 음식 차단을 검사한다. 결과는 Temp/synergy-regression-results.txt에 기록된다. 메뉴 실행은 이번 세션에서 확인하지 못했다.
- 두 프로세스 실제 Shared 방 플레이, 지연/패킷 손실, master 이전, VR 실기기 검증은 수행하지 않았다. 소스상 결함 확인과 실기 재현 완료를 구분해야 한다.
- **보스 협동 전투 문제는 남아 있다.** 보스 자체가 로컬 Instantiate 구조여서 시너지 RPC만 추가해 해결할 수 없다. 보스 생성·체력·페이즈·공격·사망 전반의 네트워크화는 이번 최소 수정 범위에서 변경하지 않았다.
- Police/Firefighter 공식 Tracker에는 별도 VFX 생성 코드가 없었다. 기존 StatusReceiver의 원소 연출은 별개 시스템이다. 이번 변경은 기존 공식 SFX/UI 전달을 복구하며 새 VFX 에셋을 만들지 않는다.
- 장판은 플레이어당 최대 64개이고 Builder 플레이어 오브젝트가 despawn되면 정리된다. 장판 소유자 퇴장 후 유지 및 authority migration 중 정확히 한 번 실행 보장은 추가 설계가 필요하다. Police의 최근 hit/cooldown도 authority 교체 시 이전되지 않는다.

## 실제 멀티 확인 절차

1. 두 프로세스로 Shared 방에 입장하고 시너지 해금을 완료한다. A Police, B Firefighter로 같은 일반 적을 3초 이내 공격한다. 양쪽 HP/UI/SFX 및 적 스턴이 일치하고 보너스 피해가 한 번 적용되는지 본다.
2. master가 공격하지 않는 3인 방에서도 두 non-master의 공격으로 같은 결과가 나오는지 본다. 공격 순서를 뒤집고 3초 초과, 쿨다운 중 추가 공격, 서로 다른 적 공격도 확인한다.
3. 동일 직업 두 명의 첫 공격이 shotId 충돌로 누락되지 않는지, 인원수를 늘려도 물 피해가 증가하지 않는지 본다.
4. B Builder가 만든 장판 위치가 A Chef에게 동일하게 보이는지 확인한다. A의 실제 음식으로 활성화한 뒤 양쪽에서 유인/폭발/SFX가 한 번 보이는지, HP와 넉백이 같은지 본다. master 직업을 서로 바꿔 반복한다.
5. 음식 여러 개 동시 접촉, 콜라이더 여러 개인 적, 시너지 재귀 금지(폭발 allowSynergy=false), 자연 만료, 장판 생성 뒤 늦은 입장을 확인한다.
6. Stage1/Stage2 일반 적 모두 반복한다. 보스는 위의 별도 미해결 항목으로 취급한다.

참고: [Unity OnCollisionEnter](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnCollisionEnter.html), [Fusion 2 RPC](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs), [Fusion 2 state replication](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-transfer).
