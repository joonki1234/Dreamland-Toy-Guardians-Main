using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class PrototypeRayWeapon : MonoBehaviour
    {
        [Header("Prototype Input")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private MissionBannerUI missionUI;
        [SerializeField] private bool enableMouseInput = true;

        [Header("Shot")]
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.1f)] private float range = 50f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private string playerId = "LOCAL";
        [SerializeField] private PlayerRole role = PlayerRole.Police;

        private int nextShotId;
        private PrototypeAimReticle aimReticle;

        public PlayerRole Role => role;
        public string PlayerId => playerId;
        public Camera AimCamera => aimCamera;

        private void Start()
        {
            if (aimCamera == null)
            {
                aimCamera = GetComponent<Camera>();
                aimCamera ??= Camera.main;
            }

            ResolveReticle();
            missionUI?.SetRole(role);
        }

        private void Update()
        {
            HandleRoleDebugKeys();

            if (enableMouseInput && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Fire();
            }
        }

        public void Configure(Camera camera, MissionBannerUI ui)
        {
            aimCamera = camera;
            missionUI = ui;

            ResolveReticle();

            if (Application.isPlaying)
            {
                missionUI?.SetRole(role);
            }
        }

        public void SetPlayerIdentity(string id, PlayerRole playerRole)
        {
            playerId = string.IsNullOrWhiteSpace(id) ? "LOCAL" : id;
            SetRole(playerRole);
        }

        public void SetRole(PlayerRole newRole)
        {
            role = newRole;
            missionUI?.SetRole(role);
            Debug.Log("[Dreamland] Prototype role: " + DreamGameText.GetRoleName(role));
        }

        public bool Fire()
        {
            if (aimCamera == null)
            {
                return false;
            }

            Ray ray = new Ray(aimCamera.transform.position, aimCamera.transform.forward);
            bool hitEnemy = FireRay(ray, playerId, role, ++nextShotId);
            aimReticle?.NotifyShot(hitEnemy);
            return hitEnemy;
        }

        public bool FireFromTransform(
            Transform fireTransform,
            string networkPlayerId,
            PlayerRole networkRole,
            int networkShotId)
        {
            if (fireTransform == null)
            {
                return false;
            }

            Ray ray = new Ray(fireTransform.position, fireTransform.forward);
            return FireRay(ray, networkPlayerId, networkRole, networkShotId);
        }

        public bool FireRay(
            Ray ray,
            string sourcePlayerId,
            PlayerRole sourceRole,
            int shotId)
        {
            Debug.DrawRay(ray.origin, ray.direction * range, Color.cyan, 0.25f);

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy == null)
                {
                    continue;
                }

                DamageInfo info = new DamageInfo(
                    damage,
                    sourcePlayerId,
                    sourceRole,
                    shotId,
                    hit.point,
                    true);

                return enemy.TakeDamage(info);
            }

            return false;
        }

        private void ResolveReticle()
        {
            aimReticle = GetComponent<PrototypeAimReticle>();
            if (aimReticle == null)
            {
                aimReticle = UnityEngine.Object.FindAnyObjectByType<PrototypeAimReticle>();
            }

            aimReticle?.Configure(aimCamera);
        }

        private void HandleRoleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SetRole(PlayerRole.Police);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SetRole(PlayerRole.Firefighter);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SetRole(PlayerRole.Astronomer);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                SetRole(PlayerRole.Architect);
            }
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
        }
    }
}
