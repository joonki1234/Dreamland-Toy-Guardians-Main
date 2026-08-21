using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// Animator 없이 장난감 로봇 파츠를 직접 회전시켜
    /// 걷는 팔/다리와 좀비처럼 까딱이는 머리를 표현합니다.
    ///
    /// 프리팹의 기존 직렬화 값이나 Inspector 연결에 의존하지 않고
    /// 런타임에 파츠를 다시 찾고 Pivot을 준비합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToyRobotMotion : MonoBehaviour
    {
        public const string MotionVersion =
            "ZombieWalk V5 - Forced Runtime Initialization";

        private static bool versionLogged;

        [Header("Visual Parts")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform body;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        [Header("Motion")]
        [SerializeField, Min(0.1f)]
        private float walkFrequency = 6.5f;

        [SerializeField, Range(0f, 60f)]
        private float armSwingAngle = 32f;

        [SerializeField, Range(0f, 45f)]
        private float legSwingAngle = 20f;

        [SerializeField, Range(0f, 20f)]
        private float bodySwayAngle = 2f;

        [SerializeField, Range(0f, 30f)]
        private float headForwardLean = 12f;

        [SerializeField, Range(0f, 30f)]
        private float headNodAngle = 8f;

        [SerializeField, Range(0f, 20f)]
        private float headTiltAngle = 5f;

        [SerializeField, Min(0.1f)]
        private float smoothing = 14f;

        [Header("Body Bounce (modelRoot only, never the AI root)")]
        [SerializeField, Range(0f, 0.2f)]
        private float bounceHeight = 0.05f;

        [SerializeField, Range(0f, 0.1f)]
        private float swayAmount = 0.025f;

        [Header("Runtime Check")]
        [SerializeField]
        private string runtimeStatus = "Not initialized";

        private EnemyHealth health;
        private EnemyCoreMover mover;

        private Quaternion headStartRotation;
        private Quaternion bodyStartRotation;
        private Quaternion leftArmStartRotation;
        private Quaternion rightArmStartRotation;
        private Quaternion leftLegStartRotation;
        private Quaternion rightLegStartRotation;

        private Vector3 modelRootStartLocalPosition;

        private float phase;
        private bool initialized;

        private void Awake()
        {
            ForceInitialize();
        }

        private void OnEnable()
        {
            ForceInitialize();
        }

        private void Start()
        {
            ForceInitialize();
            CacheRuntimeComponents();
        }

        /// <summary>
        /// 스포너에서도 호출합니다.
        /// 프리팹 연결이 끊겨 있어도 파츠를 찾아 동작을 준비합니다.
        /// </summary>
        public void ForceInitialize()
        {
            if (initialized)
            {
                enabled = true;
                return;
            }

            // 기존 프리팹에 저장된 약한 값보다 확실하게 보이도록 보정합니다.
            walkFrequency = Mathf.Max(walkFrequency, 6.5f);
            armSwingAngle = Mathf.Max(armSwingAngle, 32f);
            legSwingAngle = Mathf.Max(legSwingAngle, 20f);
            bodySwayAngle = Mathf.Max(bodySwayAngle, 2f);
            headForwardLean = Mathf.Max(headForwardLean, 12f);
            headNodAngle = Mathf.Max(headNodAngle, 8f);
            headTiltAngle = Mathf.Max(headTiltAngle, 5f);
            smoothing = Mathf.Max(smoothing, 14f);

            FindRobotParts();
            PrepareRuntimePivots();
            CaptureInitialPose();
            CacheRuntimeComponents();

            phase = Random.Range(0f, Mathf.PI * 2f);
            initialized = true;
            enabled = true;

            runtimeStatus =
                "READY | Head:" + (head != null) +
                " Arms:" + (leftArm != null && rightArm != null) +
                " Legs:" + (leftLeg != null && rightLeg != null);

            if (!versionLogged)
            {
                versionLogged = true;
                Debug.LogWarning(
                    "[ToyRobotMotion] " + MotionVersion +
                    " / " + runtimeStatus,
                    this);
            }

            if (head == null ||
                leftArm == null ||
                rightArm == null ||
                leftLeg == null ||
                rightLeg == null)
            {
                Debug.LogWarning(
                    "[ToyRobotMotion] 일부 로봇 파츠를 찾지 못했습니다. " +
                    runtimeStatus,
                    this);
            }
        }

        private void CacheRuntimeComponents()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (mover == null)
            {
                mover = GetComponent<EnemyCoreMover>();
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                ForceInitialize();
            }

            CacheRuntimeComponents();

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            bool isDead =
                health != null &&
                health.IsDead;

            if (isDead)
            {
                ApplyNeutralPose(deltaTime);
                return;
            }

            // EnemyCoreMover가 활성화된 동안은 확실하게 걷기 동작을 실행합니다.
            // 이동 거리 감지에 의존하지 않으므로 Rigidbody/FixedUpdate 방식과도 충돌하지 않습니다.
            bool isApproaching =
                mover == null ||
                mover.enabled;

            float frequency =
                isApproaching
                    ? walkFrequency
                    : walkFrequency * 0.22f;

            phase += deltaTime * frequency;

            float walkWave = Mathf.Sin(phase);
            float unevenHeadWave =
                Mathf.Sin(phase * 0.63f + 0.8f);

            float armAngle =
                isApproaching
                    ? walkWave * armSwingAngle
                    : walkWave * 2f;

            float legAngle =
                isApproaching
                    ? walkWave * legSwingAngle
                    : 0f;

            float bodyAngle =
                isApproaching
                    ? Mathf.Sin(phase * 0.5f) *
                      bodySwayAngle
                    : 0f;

            float headPitch =
                headForwardLean +
                unevenHeadWave *
                (isApproaching
                    ? headNodAngle
                    : headNodAngle * 0.25f);

            float headTilt =
                Mathf.Sin(phase * 0.37f + 1.4f) *
                (isApproaching
                    ? headTiltAngle
                    : headTiltAngle * 0.35f);

            // 왼팔-오른다리 / 오른팔-왼다리가 서로 반대로 움직입니다.
            ApplyRotation(
                leftArm,
                leftArmStartRotation,
                Vector3.right,
                armAngle,
                deltaTime);

            ApplyRotation(
                rightArm,
                rightArmStartRotation,
                Vector3.right,
                -armAngle,
                deltaTime);

            ApplyRotation(
                leftLeg,
                leftLegStartRotation,
                Vector3.right,
                -legAngle,
                deltaTime);

            ApplyRotation(
                rightLeg,
                rightLegStartRotation,
                Vector3.right,
                legAngle,
                deltaTime);

            ApplyRotation(
                body,
                bodyStartRotation,
                Vector3.forward,
                bodyAngle,
                deltaTime);

            ApplyCombinedRotation(
                head,
                headStartRotation,
                Vector3.right,
                headPitch,
                Vector3.forward,
                headTilt,
                deltaTime);

            // 한 걸음(팔/다리 스윙 반 주기)마다 두 번 튀도록 phase를 그대로 재사용한다.
            // EnemyCoreMover가 읽는 AI 루트 transform.position은 절대 건드리지 않고
            // modelRoot(시각 전용 자식)의 localPosition에만 오프셋을 준다.
            float bounce =
                isApproaching
                    ? Mathf.Abs(Mathf.Sin(phase)) * bounceHeight
                    : 0f;

            float sway =
                isApproaching
                    ? Mathf.Sin(phase * 0.5f) * swayAmount
                    : 0f;

            ApplyModelRootOffset(
                bounce,
                sway,
                deltaTime);
        }

        /// <summary>
        /// modelRoot의 localPosition에 위아래 바운스(y)와 좌우 흔들림(x)을
        /// 부드럽게 적용한다. modelRoot가 없으면 아무 것도 하지 않는다.
        /// </summary>
        private void ApplyModelRootOffset(
            float bounce,
            float sway,
            float deltaTime)
        {
            if (modelRoot == null)
            {
                return;
            }

            Vector3 desiredLocalPosition =
                modelRootStartLocalPosition +
                new Vector3(sway, bounce, 0f);

            float blend =
                1f -
                Mathf.Exp(
                    -smoothing *
                    deltaTime);

            modelRoot.localPosition =
                Vector3.Lerp(
                    modelRoot.localPosition,
                    desiredLocalPosition,
                    blend);
        }

        private void FindRobotParts()
        {
            modelRoot = FindDeepChild(
                transform,
                "BrickToy_3D_LP_Warrior_Robots_2_1");

            Transform searchRoot =
                modelRoot != null
                    ? modelRoot
                    : transform;

            head = FindDeepChild(
                searchRoot,
                "Brick_Robots_2_005");

            body = FindDeepChild(
                searchRoot,
                "Brick_Robots_2_008");

            leftArm = FindDeepChild(
                searchRoot,
                "LeftArmPivot");

            rightArm = FindDeepChild(
                searchRoot,
                "RightArmPivot");

            leftLeg = FindDeepChild(
                searchRoot,
                "Brick_Robots_2_001");

            rightLeg = FindDeepChild(
                searchRoot,
                "Brick_Robots_2_002");
        }

        private void PrepareRuntimePivots()
        {
            Bounds bodyBounds;

            if (TryGetCombinedRendererBounds(
                    body,
                    out bodyBounds))
            {
                RepositionArmPivot(
                    leftArm,
                    bodyBounds);

                RepositionArmPivot(
                    rightArm,
                    bodyBounds);
            }

            head = CreatePartPivotAtBoundsEdge(
                head,
                "HeadMotionPivot_Runtime",
                false);

            leftLeg = CreatePartPivotAtBoundsEdge(
                leftLeg,
                "LeftLegMotionPivot_Runtime",
                true);

            rightLeg = CreatePartPivotAtBoundsEdge(
                rightLeg,
                "RightLegMotionPivot_Runtime",
                true);
        }

        private void RepositionArmPivot(
            Transform armPivot,
            Bounds bodyBounds)
        {
            Bounds armBounds;

            if (armPivot == null ||
                !TryGetCombinedRendererBounds(
                    armPivot,
                    out armBounds))
            {
                return;
            }

            bool positiveX =
                armBounds.center.x >=
                bodyBounds.center.x;

            float shoulderX =
                positiveX
                    ? bodyBounds.max.x
                    : bodyBounds.min.x;

            float shoulderY =
                Mathf.Lerp(
                    bodyBounds.min.y,
                    bodyBounds.max.y,
                    0.72f);

            Vector3 shoulderPosition =
                new Vector3(
                    shoulderX,
                    shoulderY,
                    armBounds.center.z);

            MovePivotWithoutMovingChildren(
                armPivot,
                shoulderPosition);
        }

        private static Transform CreatePartPivotAtBoundsEdge(
            Transform part,
            string pivotName,
            bool useTopEdge)
        {
            if (part == null ||
                part.parent == null)
            {
                return part;
            }

            if (part.name == pivotName)
            {
                return part;
            }

            Bounds bounds;

            if (!TryGetCombinedRendererBounds(
                    part,
                    out bounds))
            {
                return part;
            }

            Transform oldParent = part.parent;

            GameObject pivotObject =
                new GameObject(pivotName);

            Transform pivot =
                pivotObject.transform;

            pivot.SetParent(
                oldParent,
                false);

            pivot.position =
                new Vector3(
                    bounds.center.x,
                    useTopEdge
                        ? bounds.max.y
                        : bounds.min.y,
                    bounds.center.z);

            pivot.rotation =
                oldParent.rotation;

            pivot.localScale =
                Vector3.one;

            part.SetParent(
                pivot,
                true);

            return pivot;
        }

        private static void MovePivotWithoutMovingChildren(
            Transform pivot,
            Vector3 newWorldPosition)
        {
            if (pivot == null)
            {
                return;
            }

            int childCount =
                pivot.childCount;

            Vector3[] positions =
                new Vector3[childCount];

            Quaternion[] rotations =
                new Quaternion[childCount];

            for (int i = 0;
                 i < childCount;
                 i++)
            {
                Transform child =
                    pivot.GetChild(i);

                positions[i] =
                    child.position;

                rotations[i] =
                    child.rotation;
            }

            pivot.position =
                newWorldPosition;

            for (int i = 0;
                 i < childCount;
                 i++)
            {
                Transform child =
                    pivot.GetChild(i);

                child.SetPositionAndRotation(
                    positions[i],
                    rotations[i]);
            }
        }

        private void CaptureInitialPose()
        {
            modelRootStartLocalPosition =
                modelRoot != null
                    ? modelRoot.localPosition
                    : Vector3.zero;

            headStartRotation =
                GetLocalRotation(head);

            bodyStartRotation =
                GetLocalRotation(body);

            leftArmStartRotation =
                GetLocalRotation(leftArm);

            rightArmStartRotation =
                GetLocalRotation(rightArm);

            leftLegStartRotation =
                GetLocalRotation(leftLeg);

            rightLegStartRotation =
                GetLocalRotation(rightLeg);
        }

        private void ApplyNeutralPose(
            float deltaTime)
        {
            ApplyRotation(
                leftArm,
                leftArmStartRotation,
                Vector3.right,
                0f,
                deltaTime);

            ApplyRotation(
                rightArm,
                rightArmStartRotation,
                Vector3.right,
                0f,
                deltaTime);

            ApplyRotation(
                leftLeg,
                leftLegStartRotation,
                Vector3.right,
                0f,
                deltaTime);

            ApplyRotation(
                rightLeg,
                rightLegStartRotation,
                Vector3.right,
                0f,
                deltaTime);

            ApplyRotation(
                body,
                bodyStartRotation,
                Vector3.forward,
                0f,
                deltaTime);

            ApplyCombinedRotation(
                head,
                headStartRotation,
                Vector3.right,
                0f,
                Vector3.forward,
                0f,
                deltaTime);

            ApplyModelRootOffset(
                0f,
                0f,
                deltaTime);
        }

        private void ApplyRotation(
            Transform target,
            Quaternion startRotation,
            Vector3 localAxis,
            float angle,
            float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            Quaternion desiredRotation =
                startRotation *
                Quaternion.AngleAxis(
                    angle,
                    localAxis.normalized);

            float blend =
                1f -
                Mathf.Exp(
                    -smoothing *
                    deltaTime);

            target.localRotation =
                Quaternion.Slerp(
                    target.localRotation,
                    desiredRotation,
                    blend);
        }

        private void ApplyCombinedRotation(
            Transform target,
            Quaternion startRotation,
            Vector3 firstAxis,
            float firstAngle,
            Vector3 secondAxis,
            float secondAngle,
            float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            Quaternion desiredRotation =
                startRotation *
                Quaternion.AngleAxis(
                    firstAngle,
                    firstAxis.normalized) *
                Quaternion.AngleAxis(
                    secondAngle,
                    secondAxis.normalized);

            float blend =
                1f -
                Mathf.Exp(
                    -smoothing *
                    deltaTime);

            target.localRotation =
                Quaternion.Slerp(
                    target.localRotation,
                    desiredRotation,
                    blend);
        }

        private static Quaternion GetLocalRotation(
            Transform target)
        {
            return target != null
                ? target.localRotation
                : Quaternion.identity;
        }

        private static Transform FindDeepChild(
            Transform root,
            string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform child =
                    root.GetChild(i);

                Transform result =
                    FindDeepChild(
                        child,
                        targetName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static bool TryGetCombinedRendererBounds(
            Transform root,
            out Bounds bounds)
        {
            bounds = default(Bounds);

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            bool found = false;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer targetRenderer =
                    renderers[i];

                if (targetRenderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds =
                        targetRenderer.bounds;

                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        targetRenderer.bounds);
                }
            }

            return found;
        }
    }
}
