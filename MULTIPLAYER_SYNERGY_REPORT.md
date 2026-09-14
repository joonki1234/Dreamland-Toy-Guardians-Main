# 일반 적 Shared Mode 시너지 수정 보고

작업 브랜치: `fix/multiplayer-synergy` · 시작 시 tracked/untracked 변경 없음.
환경: 프로젝트의 `ProjectVersion.txt` 기준 Unity `6000.5.2f1`, 설치된 Fusion `2.1.1`.
범위: 일반 적과 플레이어의 Police + Firefighter, Chef + Builder MudSplat.
**Unity Editor 내 검증과 실제 두 클라이언트 통신 검증을 구분한다. 실제 2~4인 멀티 테스트는 수행하지 않았다.**

## 1. 실제 원인

- 공격 기록은 이미 `EnemyHealth.RPC_RequestDamage`를 통해 같은 적 State Authority에 모였다. 별도 시너지 Hit RPC를 만들 필요가 없었다.
- `RoleSynergyTracker.RegisterHit`가 스턴, SFX, `DreamGameEvents.RaiseSynergyTriggered`를 모두 로컬에서 실행했다. 보너스 피해는 Authority에서 계산되지만 다른 Peer의 UI/SFX 이벤트는 발생하지 않았다.
- `DreamEnemySpawner.ConfigureSpawnedEnemy`의 tracker 추가와 Audio 설정도 스폰한 Peer에서만 실행됐다. 따라서 결과 RPC뿐 아니라 원격 tracker와 Audio 설정 경로도 필요했다.
- 기존 중복 키는 `DamageInfo.playerId + shotId`였다. 하지만 `playerId`는 `POLICE_BULLET_PROJECTILE` 같은 **무기 경로별 고정 문자열**이며 실제 PlayerRef가 아니었다. 같은 무기를 사용하는 다른 클라이언트의 독립적인 static 카운터가 충돌할 수 있었다. Police와 Firefighter는 문자열이 서로 달라, 이 문제만으로 두 직업의 모든 시너지 실패를 설명하지는 않는다.
- 원격 Police/Chef/Dirt는 `enabled = false`만 설정하고 충돌 함수 내부에는 검사가 없었다. Unity는 비활성 MonoBehaviour에도 충돌 콜백을 보낼 수 있다. [Unity 충돌 콜백 문서](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnCollisionEnter.html)
- `WaterParticleHit.OnParticleCollision`에는 소유권 검사가 없었다. `FireHoseController.Update`의 검사만으로는 별도 컴포넌트의 Particle 충돌을 막지 못했다.
- MudSplat은 NetworkObject 없는 로컬 Instantiate 객체였다. `synergyActivated`도 로컬 bool이며, 음식 컴포넌트의 enabled 여부를 검사하지 않았다. 폭발은 로컬 실행 후 적 Damage RPC를 보냈고, 유인/넉백은 호출 Peer의 mover에 직접 적용했다.
- `RoleSynergyProgression`은 static/local 상태였으며 Networked 해금 상태가 없었다.

## 2. 기존 Network Damage 흐름

`PlayerJobController`의 공격/스킬 RPC는 모든 Peer에서 연출을 생성한다. `dealsDamage`는 해당 플레이어의 InputAuthority에서만 true다.

| 공격 | 실제 명중/피해 요청 위치 | 적 피해 진입점 |
|---|---|---|
| Police 일반 | 소유자 총알의 Collision/Trigger | `EnemyHealth.TakeDamage` |
| Police Focused Fire | 소유자 효과의 피해 tick | 동일 |
| Firefighter 물 | 소유자 물 파티클 Collision | 동일 |
| Fire Truck | 소유자 차량의 적 Overlap 판정 | 동일 |
| Chef 일반/주변 피해 | 소유자 음식 Collision/Overlap | 동일 |
| Chef Special Menu | 소유자 효과의 폭발 Overlap | 동일 |
| Builder Dirt | 소유자 파편 Collision, `DirtShotContext`로 파편 누적량 제한 | 동일 |

일반 적은 `DreamEnemySpawner`가 Shared Master에서 `Runner.Spawn`한다. 정상 스폰 경로의 적 State Authority는 이 스폰 Peer다. 적 소유자가 공격하면 직접 `ApplyDamageAuthoritative`, 다른 공격자는 기존 `RPC_RequestDamage`로 같은 메서드에 도달한다. `NetworkedHealth`와 기존 체력/사망 이벤트 동기화는 유지했다.

`StatusReceiver.TakeDamage(float)`도 확인했다. 현재 이 메서드는 로그를 남기며 `EnemyHealth` HP를 감소시키지 않는다. 별도의 일반 적 HP 계산 경로를 새로 만들지 않았다.

## 3. 수정 파일

| 파일 | 수정 이유 |
|---|---|
| `Assets/DreamlandTutorialStage1/Runtime/EnemyHealth.cs` | 실제 RPC 발신 PlayerRef로 중복 키 구분, Authority 계산 가드, 시너지 결과 RPC, 기존 Damage RPC에 MudSplat 충격 정보 전달, 유인 요청을 적 Authority로 전달 |
| 같은 폴더 `RoleSynergyTracker.cs` | Authority 판정과 결과 Presentation 분리, 실제 공격자 로그, 원격 Audio 설정 |
| 같은 폴더 `DreamGameEvents.cs` | Editor/Development Build 전용 `SynergyNetLog` |
| 같은 폴더 `DreamGameTypes.cs` | 기존 DamageInfo에 선택적 MudSplat 충격 정보, 기존 해금 API의 네트워크 연결 |
| 같은 폴더 `DreamEnemySpawner.cs` | 기존 Audio 설정을 원격 Presentation에서도 사용, 해금 연결 정리 |
| 같은 폴더 `DreamEnemySpawner.Synergy.cs` 및 meta (신규) | 기존 spawner NetworkBehaviour에 Networked 해금 상태와 해금 요청 추가. 보스 partial/lifecycle 코드는 수정하지 않음 |
| `Assets/Project/Scripts/Battle/PoliceBulletProjectile.cs` | Collision/Trigger 내부에서 비활성 원격 연출 제외 |
| 같은 폴더 `WaterParticleHit.cs` | Particle 충돌에서 실제 공격 소유자 확인 |
| 같은 폴더 `ChefFoodProjectile.cs` | 원격 Collision 제외, 음식의 시너지 진입 자격 및 1회 소비 처리 |
| 같은 폴더 `MudSplatSynergy.cs` | NetworkBehaviour, 활성화/유인/폭발 단계와 타이머, 결과 Presentation, 기존 적 Damage 경로 이용 |
| `Assets/DirtProjectile.cs` | 원격 Collision 제외, 소유자의 Runner로 MudSplat Spawn, 기존 오프라인 fallback 유지 |
| `Assets/PlayerJobController.cs` | Dirt 초기화 시 기존 Runner 전달 한 곳 |
| `Assets/MudSplat.prefab` 및 meta | Unity Editor에서 NetworkObject 추가/bake 및 FusionPrefab 등록. 기존 Collider/외형/수치/음원 참조 보존 |
| `Assets/Editor/SynergyNetworkValidation.cs` 및 meta (신규) | 재실행 가능한 Editor 검사와 MudSplat bake/등록 검사 |

`DirtShotContext.cs`, `DirtBlock.prefab`, `01_Player.prefab`, 일반 스킬 구현은 조사 후 그대로 유지했다. `DirtBlock.prefab`의 기존 MudSplat 참조 GUID도 유지했다.

## 4. Police + Firefighter

```text
Police 소유자 명중 ── TakeDamage / 기존 Damage RPC ─┐
                                                ├─ Enemy State Authority
Firefighter 소유자 명중 ── 같은 경로 ──────────────┘
    → 해당 적의 RoleSynergyTracker
    → Trigger Window / Cooldown
    → 기존 Bonus Damage + Stun
    → 결과 Presentation RPC
```

기본 설정은 Trigger Window 3초, Cooldown 2.5초, Bonus 30, Stun 1초이며 변경하지 않았다. 적마다 tracker가 있으므로 다른 적의 기록은 합쳐지지 않는다. 두 입력 순서 모두 같은 조건을 검사한다. `CurrentDamageMultiplier`는 기존대로 1이며 새로운 Vulnerability를 추가하지 않았다.

## 5. Shot ID

일반 적의 중복 키는 **`Attacker PlayerRef + 기존 공격 경로 문자열 + ShotId`**다. 로컬 요청은 `Runner.LocalPlayer`, 원격 요청은 Fusion이 제공하는 `RpcInfo.Source`를 사용한다. 같은 플레이어의 서로 다른 공격 경로도 구분한다. 다른 PlayerRef의 동일 ShotId는 허용하고, 같은 플레이어/경로/ShotId는 제거한다.

MudSplat은 추가로 NetworkObject Id를 공격 경로에 포함한다. 기존 `shotId < 0` fallback과 기억하는 샷 수의 제한은 보존했다. 보스 대상의 기존 중복 키 분기는 유지했다.

## 6. Remote Attack

Police/음식/흙은 기존 RPC의 `dealsDamage` 분기를 유지하고 실제 Unity 충돌 함수에서도 `isActiveAndEnabled`를 확인한다. 물은 저장한 플레이어 NetworkObject의 유효성과 InputAuthority를 검사한다. 기존 Focused Fire/Fire Truck/Chef Special Menu는 이미 `dealsDamage`로 피해 함수를 제한하므로 변경하지 않았다.

원격 음식은 `CanActivateMudSplat`도 false다. 실제 Chef 소유자 음식만 1회 소비 후 활성화를 요청한다. 일반 Chef 스킬은 기존대로 기본 음식 컴포넌트를 비활성화하고 전용 폭발을 사용한다.

## 7. MudSplat

- 같은 Builder 공격의 파편은 기존 `DirtShotContext`를 공유한다. 최초 바닥 충돌 파편만 Spawn을 요청한다.
- Builder 소유자의 Runner가 기존 MudSplat 프리팹을 Spawn하며 Builder가 장판 State Authority다. 배치 위치/회전, Phase, 단계 타이머와 만료 타이머를 동기화한다.
- Chef Peer에서 자신의 음식이 네트워크 장판에 진입하면 장판 Authority로 활성화 RPC를 보낸다. Authority만 `Phase 0 → 1`을 확정하므로 동시 음식 요청도 한 번만 수락한다.
- Phase 1 이후 지연 → Phase 2에서 유인 요청 → 지연 → Phase 3에서 폭발. 단계 변경을 먼저 확정하고 효과를 적용한다.
- 유인은 `EnemyHealth.RequestMudSplatLure`가 적 State Authority에 전달한다. 폭발 Damage는 기존 `TakeDamage → RPC_RequestDamage → ApplyDamageAuthoritative`만 사용한다.
- 적 단위 HashSet으로 여러 Collider를 합친다. 스턴/넉백 정보도 같은 Damage 요청에 실려 적 Authority에서 적용되며, 죽은 적에게 추가 충격을 실행하지 않는다.
- 다른 Peer는 장판 배치/표시 및 활성화/폭발 Presentation만 처리한다. 장판과 적의 State Authority가 서로 달라도 Damage 경로는 같다.
- 비활성 장판은 기존 30초 lifetime을 사용한다. 활성화 후에는 유인/폭발 시퀀스가 끝나도록 한다. 폭발 직후 장판을 숨기고 기존 이펙트 lifetime 후 Despawn한다.
- 이번 일반 적 시너지 경로는 FinalBossAttackController가 붙은 대상을 제외한다. 보스 구현 자체는 변경하지 않았다.

새 프리팹은 Editor에서 실제 bake했다. 동기화할 장기 상태는 Networked 속성, 일회성 연출은 RPC로 구분했다. [Fusion NetworkObject](https://doc.photonengine.com/fusion/v2/manual/network-object), [Networked 속성](https://doc.photonengine.com/fusion/v2/manual/data-transfer/networked-properties)

## 8. Synergy Presentation

Police + Firefighter는 `EnemyHealth.RPC_PresentSynergy(StateAuthority → All)`에서 `RoleSynergyTracker.Present`만 호출한다. 이 함수는 기존 SFX 및 `DreamGameEvents.SynergyTriggered`를 재생하며 보너스 계산/스턴을 다시 실행하지 않는다. 원격 tracker가 없으면 일반 MonoBehaviour만 추가하고 기존 spawner의 동일 Audio 설정을 사용한다.

MudSplat 활성화/폭발도 StateAuthority → All RPC로 전달한다. 로컬 재생 플래그로 중복 재생을 방지한다. 원격 늦은 스폰이 이미 폭발한 장판의 외형을 다시 표시하지 않도록 Phase 3 표시를 숨긴다. 기존 UI, VFX 프리팹, SFX 파일은 변경하지 않았다.

## 9. 기존 기능 영향

- 일반/스킬 피해량, Enemy HP, 발사 간격, VR 입력, XR, 이동, 무기 Parent/Position/Rotation을 변경하지 않았다.
- 체력 계산, 사망 동기화, 개인 튜토리얼 대상 소유권, 튜토리얼 Hit Count 경로는 유지했다. 시너지 충격 정보가 없는 일반 공격은 기존 피해 계산을 사용한다.
- 해금은 기존 `RoleSynergyProgression.Lock/Unlock` 호출을 유지하면서 spawner의 `NetworkedSynergyUnlocked`로 공유한다. 기존 Stage1/Stage2 코드, Skip/Test 호출 위치와 연출은 수정하지 않았다.
- Stage1 초기화의 Lock은 Authority만 공유 상태를 바꾼다. 늦게 도착한 Peer의 로컬 Lock이 방 전체 해금을 취소하지 않는다. Skip/Stage2 직접 테스트의 Unlock은 다른 Peer에서도 Authority에 요청할 수 있다.
- 네트워크 스폰 전 로컬 상태를 보존하고 스폰 후 바인딩한다. 해금 상태는 RPC 이력만이 아니라 Networked snapshot으로 전달한다.
- 보스 전용 파일, 보스 HP/패턴/얼굴/Network 로직, Core Damage는 수정하지 않았다. 보스 문제는 검증 범위에도 포함하지 않았다.

## 10. 컴파일 및 수행한 검증

현재 열려 있는 Unity 6000.5.2f1 Editor에서 Refresh 후 C# runtime/editor 어셈블리 생성과 Fusion `ILWeaverBindings` 후처리, domain reload를 확인했다. 마지막 변경 이후 검증 결과는 `Library/SynergyNetworkQA/result.txt`, 원본 로그는 `Logs/Editor.log`에 있다. 수정 중 자동 Refresh가 남긴 이전 임시 오류와 마지막 성공한 컴파일을 구분해야 한다. 프로젝트의 기존 obsolete/unused 경고는 남아 있다.

재실행 메뉴: **Tools → Dream Guardians → Validate Multiplayer Synergy** (Edit Mode).

| 요청 테스트 | 수행한 확인 | 실제 두 클라이언트 |
|---|---|---|
| 1 Police → Firefighter | 실제 tracker 및 로컬 EnemyHealth 함수 검사 통과 | 미실행 |
| 2 Firefighter → Police | 실제 tracker 검사 통과 | 미실행 |
| 3 서로 다른 적 | 별도 tracker에 입력하여 미발동 확인 | 미실행 |
| 4 Window 초과 | 기록 시각을 4초 전으로 설정해 미발동 확인 | 미실행 |
| 5 Cooldown | 발동 직후 재공격의 미발동 확인 | 미실행 |
| 6 동일 ShotId/다른 PlayerRef | 실제 키/RememberShot/중복 검사 함수 확인 | 미실행 |
| 7 Remote 연출 | 비활성 총알/음식/흙/물 실제 콜백 호출 시 피해 없음 확인 | 미실행 |
| 8 Builder/Chef 별도 Peer | Network Spawn/활성화 RPC/Authority Phase 경로 코드 확인, 원격 음식 거부 검사 | 미실행 |
| 9 MudSplat 적당 1회 피해 | Physics Overlap에서 Collider 2개인 적에게 30 피해 1회 확인 | 미실행 |
| 10 2~4인 중복 방지 | Authority 가드/기존 단일 Damage RPC/Presentation 분리 코드 확인 | 미실행 |

추가로 일반 10+10 피해에 시너지 Bonus 30이 한 번 더해져 1000→950이 되고, 중복 샷이나 Presentation 호출로 HP가 더 줄지 않는 것을 확인했다. 이는 격리된 검사 오브젝트의 초기 HP이며 게임 Enemy HP 에셋은 변경하지 않았다.

## 11. 실제 2인 테스트 방법

1. **두 실행 환경 모두 같은 변경본으로 준비한다.** Unity Console의 컴파일/Fusion 오류가 없는지 확인하고 Development Build를 새로 만든다. 기존 Build는 RPC 서명과 새 프리팹이 다르므로 재사용하지 않는다.
2. Editor와 Build를 같은 Shared 방에 입장시킨다. 우선 A(Editor)를 먼저 입장시켜 Master로 만들고 A=Police, B(Build)=Firefighter를 선택한다. 개인 튜토리얼 표적이 아닌 **동일한 일반 전투 적**을 사용한다.
3. Stage1에서 기존 해금 단계까지 진행한다. 양쪽의 해금 상태/로그가 같고, 해금 전에는 발동하지 않는지 확인한다. 별도 회차에서 기존 `DreamlandGameFlowController`의 **테스트 - Stage 2부터 시작** 경로도 사용한다. 자동 검사에서는 이 시나리오를 실행하지 않았다.
4. A가 적 X를 맞히고 3초 이내에 B가 X를 맞힌다. 적 Authority 로그에 서로 다른 Attacker의 Police/Firefighter 기록과 `EmergencySuppression TRIGGERED` 1개가 있어야 한다. 각 Peer에는 `Presentation=EmergencySuppression`이 1개씩 있어야 한다. 일반 피해에 기존 Bonus가 한 번만 추가되고 약 1초 스턴이 보이는지 확인한다.
5. 새 적에서 Firefighter → Police 순서도 반복한다. 서로 다른 적을 공격하거나 두 입력 사이를 3초보다 길게 두면 발동하지 않아야 한다. 발동 직후 2.5초 이내 재공격은 추가 발동하지 않아야 한다. 실제 Inspector 값이 다르면 그 Window/Cooldown을 기준으로 한다.
6. 새 세션에서 양쪽을 같은 Police 직업으로 선택해 같은 적에 첫 발을 각각 발사한다. 로그의 ShotId가 같아도 PlayerRef가 다르면 두 피해가 모두 처리돼야 한다. 같은 샷의 중복 충돌은 한 번만 처리돼야 한다.
7. B를 Builder, A를 Chef로 바꾼다. B가 바닥에 만든 **하나의 MudSplat이 양쪽에서 같은 위치에 보이는지** 확인한다. A의 음식으로 활성화한다. 장판 Authority는 B, 적 Authority는 A인 상황을 먼저 검사한다.
8. B 로그의 `MudSplat TRIGGERED`는 1개, 각 Peer의 activation/explosion presentation은 각각 1개여야 한다. 유인 후 범위 안 일반 적별로 기존 폭발 피해 30이 한 번, 스턴/넉백도 한 번 적용돼야 한다. 여러 음식이 동시에 들어가도 폭발이 늘어나면 안 된다.
9. 역할과 방장을 뒤집어 반복한다. 미활성 장판은 lifetime 후 양쪽에서 제거되고, 활성화된 장판은 폭발 후 양쪽에서 사라지는지 확인한다. 기존 일반 공격과 각 직업 스킬도 한 명씩 사용해 관전자 수에 따라 HP 감소량이 달라지지 않는지 확인한다.
10. C/D를 관전 가능한 추가 플레이어로 입장시켜 3~4인에서 같은 절차를 반복한다. Police+Firefighter Bonus와 MudSplat 피해가 인원수 배수로 증가하면 실패다. 각 Peer의 UI/SFX/VFX, Stage1→Stage2 전환 및 기존 Skip/Test 경로는 이 실제 세션에서 최종 확인한다.

로그에서 `TRIGGERED`는 Gameplay 확정, `Presentation`은 각 화면의 결과 재생을 의미한다. Editor의 로컬 검사 로그(`SynergyQA_*`, `Authority=LOCAL`)를 실제 Shared 세션 결과로 세면 안 된다. 새 `[SynergyNet]` 로그는 Editor/Development Build에만 포함된다.
