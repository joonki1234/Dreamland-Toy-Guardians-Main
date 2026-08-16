using Fusion;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Local-authority VR hand tracking helper.
/// - 현재 활성 직업 모델을 기준으로 양팔 IK를 다시 바인딩한다.
/// - 매 프레임 전체 hierarchy 검색을 하지 않는다.
/// - 기존 무기/공격 기능은 건드리지 않는다.
/// - Remote Player 동기화는 이 파일에서 처리하지 않는다.
/// </summary>
public class VRHandTargetFollower : NetworkBehaviour
{
    [Header("IK targets")]
    [SerializeField]
    private Transform handTarget;

    [SerializeField]
    private Transform leftHandTarget;

    [SerializeField]
    private Transform rightControllerTarget;

    [SerializeField]
    private Transform leftControllerTarget;

    [Header("Controller target auto resolve")]
    [SerializeField]
    private bool autoFindControllerTargets = true;

    [SerializeField]
    private string rightControllerTargetName = "Right Controller Target";

    [SerializeField]
    private string leftControllerTargetName = "Left Controller Target";

    [Header("IK Objects")]
    [SerializeField]
    private string rightHandIkName = "RightHand_IK";

    [SerializeField]
    private string leftHandIkName = "LeftHand_IK";

    private TwoBoneIKConstraint rightHandIkConstraint;
    private TwoBoneIKConstraint leftHandIkConstraint;
    private Transform rigRoot;
    private PlayerJobController jobController;
    private PlayerJob lastObservedJob;
    private GameObject currentActiveModel;

    private bool _warnedMissingHandTarget;
    private bool _warnedMissingLeftHandTarget;
    private bool networkSpawned;

    private void Awake()
    {
        // Prepare only local, non-networked structures here.
        InitializeRuntimeOnce();
    }

    private void Start()
    {
        // Keep runtime local init only; networked init happens in Spawned().
        InitializeRuntimeOnce();
    }

    private void OnEnable()
    {
        // Keep runtime local init only; networked init happens in Spawned().
        InitializeRuntimeOnce();
    }

    private void LateUpdate()
    {
        if (!networkSpawned)
        {
            return;
        }

        if (Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        RefreshJobBindingIfNeeded();

        if (rightControllerTarget == null || leftControllerTarget == null)
        {
            TryResolveControllerTargets();
        }

        if (handTarget != null && rightControllerTarget != null)
        {
            SetTargetPose(handTarget, rightControllerTarget);
        }
        else if (!_warnedMissingHandTarget)
        {
            Debug.LogWarning("[VRHandTargetFollower] Right hand target is still missing.");
            _warnedMissingHandTarget = true;
        }

        if (leftHandTarget != null && leftControllerTarget != null)
        {
            SetTargetPose(leftHandTarget, leftControllerTarget);
        }
        else if (!_warnedMissingLeftHandTarget)
        {
            Debug.LogWarning("[VRHandTargetFollower] Left hand target is still missing.");
            _warnedMissingLeftHandTarget = true;
        }
    }

    private void InitializeRuntimeOnce()
    {
        if (jobController == null)
        {
            // Do not fetch networked PlayerJobController here; defer to Spawned().
        }

        if (rigRoot == null)
        {
            rigRoot = GetRigRoot();
        }

        if (handTarget == null)
        {
            handTarget = FindOrCreateTarget("HandTarget_R", rigRoot != null ? rigRoot : transform);
        }

        if (leftHandTarget == null)
        {
            leftHandTarget = FindOrCreateTarget("HandTarget_L", rigRoot != null ? rigRoot : transform);
        }

        // autoFindControllerTargets and job binding are handled in Spawned().
    }

    public override void Spawned()
    {
        // Called by Fusion when this NetworkBehaviour has been spawned and
        // networked properties (PlayerJobController.CurrentJob, etc.) are safe to access.
        networkSpawned = true;

        jobController = GetJobController();

        if (autoFindControllerTargets)
        {
            TryResolveControllerTargets();
        }

        RefreshJobBindingIfNeeded();
    }

    private void RefreshJobBindingIfNeeded()
    {
        if (jobController == null)
        {
            jobController = GetJobController();
        }

        if (jobController == null)
        {
            return;
        }

        if (jobController.CurrentJob != lastObservedJob || currentActiveModel == null)
        {
            lastObservedJob = jobController.CurrentJob;
            BindCurrentJobModelIK();
        }
    }

    private void BindCurrentJobModelIK()
    {
        GameObject activeModel = GetActiveModelForCurrentJob();
        if (activeModel == null)
        {
            return;
        }

        currentActiveModel = activeModel;

        if (rigRoot == null)
        {
            rigRoot = GetRigRoot();
        }

        if (rigRoot == null)
        {
            return;
        }

        string[] leftUpperArmNames =
        {
            "CC_Base_L_Upperarm",
            "L_Upperarm",
            "LeftUpperArm",
            "upperarm_left",
            "Left_Upperarm"
        };

        string[] leftForearmNames =
        {
            "CC_Base_L_Forearm",
            "L_Forearm",
            "LeftForearm",
            "forearm_left",
            "Left_Forearm"
        };

        string[] leftHandNames =
        {
            "CC_Base_L_Hand",
            "L_Hand",
            "LeftHand",
            "hand_left",
            "Left_Hand"
        };

        string[] rightUpperArmNames =
        {
            "CC_Base_R_Upperarm",
            "R_Upperarm",
            "RightUpperArm",
            "upperarm_right",
            "Right_Upperarm"
        };

        string[] rightForearmNames =
        {
            "CC_Base_R_Forearm",
            "R_Forearm",
            "RightForearm",
            "forearm_right",
            "Right_Forearm"
        };

        string[] rightHandNames =
        {
            "CC_Base_R_Hand",
            "R_Hand",
            "RightHand",
            "hand_right",
            "Right_Hand"
        };

        RebindTwoBoneIK(
            ref leftHandIkConstraint,
            leftHandIkName,
            currentActiveModel,
            leftHandTarget,
            leftUpperArmNames,
            leftForearmNames,
            leftHandNames
        );

        RebindTwoBoneIK(
            ref rightHandIkConstraint,
            rightHandIkName,
            currentActiveModel,
            handTarget,
            rightUpperArmNames,
            rightForearmNames,
            rightHandNames
        );
    }

    private void RebindTwoBoneIK(
        ref TwoBoneIKConstraint constraint,
        string constraintName,
        GameObject activeModel,
        Transform targetTransform,
        string[] upperArmNames,
        string[] forearmNames,
        string[] handNames)
    {
        if (activeModel == null || targetTransform == null)
        {
            return;
        }

        if (constraint == null)
        {
            Transform parent = rigRoot != null ? rigRoot : transform;
            TwoBoneIKConstraint foundConstraint = null;

            if (parent != null)
            {
                Transform[] all = parent.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in all)
                {
                    if (t != null && t.name == constraintName)
                    {
                        foundConstraint = t.GetComponent<TwoBoneIKConstraint>();
                        if (foundConstraint == null)
                        {
                            // If GameObject exists but constraint missing, add component to reuse GameObject.
                            foundConstraint = t.gameObject.AddComponent<TwoBoneIKConstraint>();
                        }
                        break;
                    }
                }
            }

            if (foundConstraint != null)
            {
                constraint = foundConstraint;
            }
            else
            {
                GameObject ikObject = new GameObject(constraintName);
                ikObject.transform.SetParent(parent != null ? parent : transform, false);
                constraint = ikObject.AddComponent<TwoBoneIKConstraint>();
            }
        }

        constraint.weight = 1f;
        constraint.data.target = targetTransform;
        constraint.data.hint = null;
        constraint.data.targetPositionWeight = 1f;
        constraint.data.targetRotationWeight = 1f;
        constraint.data.hintWeight = 1f;

        Transform root = FindBoneInModel(activeModel, upperArmNames);
        Transform mid = FindBoneInModel(activeModel, forearmNames);
        Transform tip = FindBoneInModel(activeModel, handNames);

        if (root != null && mid != null && tip != null)
        {
            constraint.data.root = root;
            constraint.data.mid = mid;
            constraint.data.tip = tip;
        }
    }

    private Transform FindBoneInModel(GameObject model, params string[] names)
    {
        if (model == null)
        {
            return null;
        }

        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        foreach (string name in names)
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            foreach (Transform bone in bones)
            {
                if (bone != null && bone.name == name)
                {
                    return bone;
                }
            }
        }

        return null;
    }

    private GameObject GetActiveModelForCurrentJob()
    {
        if (jobController == null)
        {
            jobController = GetJobController();
        }

        if (jobController == null)
        {
            return null;
        }

        GameObject model = null;

        switch (jobController.CurrentJob)
        {
            case PlayerJob.Police:
                model = jobController.modelPolice;
                break;
            case PlayerJob.Firefighter:
                model = jobController.modelFirefighter;
                break;
            case PlayerJob.Chef:
                model = jobController.modelChef;
                break;
            case PlayerJob.Builder:
                model = jobController.modelBuilder;
                break;
            default:
                model = null;
                break;
        }

        if (model != null && model.activeInHierarchy)
        {
            return model;
        }

        return null;
    }

    private PlayerJobController GetJobController()
    {
        if (GetComponent<PlayerJobController>() != null)
        {
            return GetComponent<PlayerJobController>();
        }

        return GetComponentInChildren<PlayerJobController>(true);
    }

    private Transform GetRigRoot()
    {
        if (rigRoot != null)
        {
            return rigRoot;
        }

        Rig rig = GetComponentInChildren<Rig>(true);
        if (rig != null)
        {
            rigRoot = rig.transform;
            return rigRoot;
        }

        rigRoot = transform;
        return rigRoot;
    }

    private void TryResolveControllerTargets()
    {
        if (rightControllerTarget == null)
        {
            TryResolveSingleControllerTarget(ref rightControllerTarget, rightControllerTargetName, "<XRController>{RightHand}");
        }

        if (leftControllerTarget == null)
        {
            TryResolveSingleControllerTarget(ref leftControllerTarget, leftControllerTargetName, "<XRController>{LeftHand}");
        }
    }

    private void TryResolveSingleControllerTarget(ref Transform target, string targetName, string controllerPath)
    {
        if (target != null || string.IsNullOrEmpty(targetName))
        {
            return;
        }

        GameObject foundObject = GameObject.Find(targetName);
        if (foundObject != null)
        {
            target = foundObject.transform;
            AttachTrackedPoseDriverIfNeeded(foundObject, controllerPath);
            return;
        }

        Transform parent = GetControllerParentTransform();
        if (parent == null)
        {
            return;
        }

        GameObject created = new GameObject(targetName);
        created.transform.SetParent(parent, false);
        created.transform.localPosition = Vector3.zero;
        created.transform.localRotation = Quaternion.identity;
        AttachTrackedPoseDriverIfNeeded(created, controllerPath);
        target = created.transform;
    }

    private Transform GetControllerParentTransform()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (VR)");
        if (xrOrigin == null)
        {
            xrOrigin = GameObject.Find("XR Origin");
        }

        if (xrOrigin == null)
        {
            return null;
        }

        Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffset != null)
        {
            return cameraOffset;
        }

        return xrOrigin.transform;
    }

    private void AttachTrackedPoseDriverIfNeeded(GameObject targetObject, string controllerPath)
    {
        if (targetObject == null || string.IsNullOrEmpty(controllerPath))
        {
            return;
        }

        TrackedPoseDriver driver = targetObject.GetComponent<TrackedPoseDriver>();
        if (driver == null)
        {
            driver = targetObject.AddComponent<TrackedPoseDriver>();
        }

        var positionAction = new InputAction("Position", InputActionType.Value, expectedControlType: "Vector3");
        positionAction.AddBinding(controllerPath + "/devicePosition");

        var rotationAction = new InputAction("Rotation", InputActionType.Value, expectedControlType: "Quaternion");
        rotationAction.AddBinding(controllerPath + "/deviceRotation");

        driver.positionInput = new InputActionProperty(positionAction);
        driver.rotationInput = new InputActionProperty(rotationAction);
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
    }

    private Transform FindOrCreateTarget(string targetName, Transform parent)
    {
        if (parent == null)
        {
            parent = transform;
        }

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == targetName)
            {
                return child;
            }
        }

        GameObject targetObject = new GameObject(targetName);
        targetObject.transform.SetParent(parent, false);
        targetObject.transform.localPosition = Vector3.zero;
        targetObject.transform.localRotation = Quaternion.identity;
        return targetObject.transform;
    }

    private void SetTargetPose(Transform target, Transform source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.SetPositionAndRotation(source.position, source.rotation);
    }
}
