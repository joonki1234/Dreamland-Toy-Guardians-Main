using Fusion;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 적(몬스터) 이동/공격 스크립트들이 NetworkBehaviour가 아니어도
    /// "지금 이 클라이언트가 이 적을 시뮬레이션해도 되는지"를 간단히
    /// 물어볼 수 있게 해주는 헬퍼입니다.
    ///
    /// - 적 프리팹에 아직 NetworkObject가 없으면(=예전처럼 완전히
    ///   로컬로만 도는 상태) 항상 true를 반환해 기존 동작을 그대로
    ///   유지합니다(하위 호환).
    /// - NetworkObject가 붙어 있으면 그 오브젝트의 State Authority를
    ///   가진 클라이언트에서만 true를 반환합니다. 이동/공격처럼
    ///   "위치를 실제로 바꾸는" 로직은 State Authority에서만 실행하고,
    ///   나머지 클라이언트는 NetworkTransform이 그 결과를 그대로
    ///   따라오게 해서 몬스터가 모두에게 같은 자리에 보이게 합니다.
    /// </summary>
    public static class EnemyNetworkAuthority
    {
        public static bool HasAuthority(Component component)
        {
            if (component == null)
            {
                return true;
            }

            NetworkObject networkObject =
                component.GetComponentInParent<NetworkObject>();

            if (networkObject == null)
            {
                // 아직 네트워크 오브젝트가 아님(프리팹에 NetworkObject
                // 미부착) → 예전처럼 로컬 전용으로 동작.
                return true;
            }

            return networkObject.HasStateAuthority;
        }
    }
}
