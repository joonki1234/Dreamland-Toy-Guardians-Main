using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#if UNITY_EDITOR && (ENABLE_VR || UNITY_GAMECORE)
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.InputSystem;
#endif

#pragma warning disable CS0618 // This project intentionally uses the Classic simulator.
// Match the original Classic simulator's execution order. All simulation stays in base.
[DefaultExecutionOrder(-29991)]
public sealed class XRDeviceSimulatorDiagnostics : XRDeviceSimulator
{
#if UNITY_EDITOR && (ENABLE_VR || UNITY_GAMECORE)
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo Selection = typeof(XRDeviceSimulator).GetField("m_TargetedDeviceInput", PrivateInstance);
    static readonly FieldInfo YInput = typeof(XRDeviceSimulator).GetField("m_KeyboardYTranslateInput", PrivateInstance);
    static readonly FieldInfo MouseInput = typeof(XRDeviceSimulator).GetField("m_MouseDeltaInput", PrivateInstance);
    static readonly FieldInfo RotateOverride = typeof(XRDeviceSimulator).GetField("m_RotateModeOverrideInput", PrivateInstance);
    static readonly FieldInfo RightState = typeof(XRDeviceSimulator).GetField("m_RightControllerState", PrivateInstance);
    static readonly FieldInfo Lifecycle = typeof(XRDeviceSimulator).GetField("m_DeviceLifecycleManager", PrivateInstance);
    static readonly FieldInfo OwnedRight = typeof(SimulatedDeviceLifecycleManager).GetField("m_RightControllerDevice", PrivateInstance);

    float nextInputLog;
    bool lastRightPressed;
    object lastSelection;
    bool wasSpaceMouseMoving;
    [SerializeField, Tooltip("Read-only pose snapshots around Space and mouse input. Editor only; enable for reproduction.")]
    bool tracePoseChain;
    string previousRenderedPose;
    int traceThroughFrame = -1;
    float nextPoseLog;
    bool explicitFpsPoseInput;

    [ContextMenu("Start Space Down Pose Trace")]
    void StartSpaceDownPoseTrace()
    {
        tracePoseChain = true;
        previousRenderedPose = null;
        traceThroughFrame = -1;
    }

    protected override void ProcessPoseInput()
    {
        // Classic defaults to FPS, including after releasing Space/Shift.
        // That mode orbits both controllers on ordinary mouse movement.
        // Only explicitly selected controllers/HMD should receive pose input.
        // Keep base.Update running so device state and controller buttons still update.
        // U explicitly selects body/FPS manipulation; Tab may explicitly cycle to it.
        if (toggleManipulateBodyAction?.action?.WasPerformedThisFrame() == true ||
            cycleDevicesAction?.action?.WasPerformedThisFrame() == true)
            explicitFpsPoseInput = manipulatingFPS;
        if (!manipulatingFPS) explicitFpsPoseInput = false;
        if (!manipulatingFPS || explicitFpsPoseInput)
            base.ProcessPoseInput();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        lastRightPressed = manipulateRightAction != null && manipulateRightAction.action != null &&
            manipulateRightAction.action.IsPressed();
        lastSelection = Selection?.GetValue(this);
        wasSpaceMouseMoving = false;
        nextInputLog = 0f;
        nextPoseLog = 0f;
        explicitFpsPoseInput = false;
        Application.onBeforeRender += TraceRenderedPose;
    }

    protected override void OnDisable()
    {
        Application.onBeforeRender -= TraceRenderedPose;
        previousRenderedPose = null;
        traceThroughFrame = -1;
        base.OnDisable();
    }

    protected override void Update()
    {
        if (tracePoseChain)
        {
            var space = Keyboard.current?.spaceKey;
            bool edge = space != null && (space.wasPressedThisFrame || space.wasReleasedThisFrame);
            bool mouseStarted = Mouse.current != null && Mouse.current.delta.ReadValue() != Vector2.zero &&
                Time.unscaledTime >= nextPoseLog;
            if (edge || mouseStarted)
            {
                traceThroughFrame = Time.frameCount + 1;
                nextPoseLog = Time.unscaledTime + 0.5f;
                Debug.Log($"[XRSimPose] previous-render {previousRenderedPose ?? "unavailable"}", this);
            }
            if (Time.frameCount <= traceThroughFrame) LogPoseChain("before-simulator");
        }
        bool baseUpdateCompleted = false;
        try
        {
            base.Update();
            baseUpdateCompleted = true;
        }
        finally
        {
            // Observe after the original state application, also if base throws.
            // Do not catch/replace its exception or modify any Action/device state.
            LogInputFrame(baseUpdateCompleted);
            if (tracePoseChain && Time.frameCount <= traceThroughFrame) LogPoseChain("after-simulator");
        }
    }

    [BeforeRenderOrder(int.MaxValue)]
    void TraceRenderedPose()
    {
        if (!tracePoseChain) return;
        previousRenderedPose = PoseChain();
        if (Time.frameCount <= traceThroughFrame)
            Debug.Log($"[XRSimPose] after-lateupdate-before-render {previousRenderedPose}", this);
    }

    void LogPoseChain(string phase) => Debug.Log($"[XRSimPose] {phase} {PoseChain()}", this);

    string PoseChain()
    {
        var entries = new List<string>();
        object internalState = RightState?.GetValue(this);
        if (internalState is XRSimulatedControllerState rightPose)
            entries.Add($"internalRight=[trackingP={rightPose.devicePosition.ToString("F5")} " +
                $"trackingR={rightPose.deviceRotation.ToString("F5")} isTracked={rightPose.isTracked} trackingState={rightPose.trackingState}]");
        foreach (var device in InputSystem.devices)
        {
            if (device is XRSimulatedController controller)
                entries.Add($"simulated=[{DevicePose(controller)}]");
            if (device is UnityEngine.InputSystem.XR.XRHMD hmd)
                entries.Add($"HMDDevice=[deviceId={hmd.deviceId} enabled={hmd.enabled} added={hmd.added} " +
                    $"trackingP={hmd.devicePosition.ReadValue().ToString("F5")} trackingR={hmd.deviceRotation.ReadValue().ToString("F5")} " +
                    $"isTracked={hmd.isTracked.isPressed} trackingState={hmd.trackingState.ReadValue()}]");
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Right Controller Target" || t.name == "Left Controller Target")
                entries.Add($"candidate=[{TransformPose(t)}]");
        foreach (var follower in Object.FindObjectsByType<VRHandTargetFollower>(FindObjectsInactive.Include))
        {
            if (follower.Object == null || !follower.Object.IsValid || !follower.Object.HasInputAuthority) continue;
            entries.Add($"player=[{TransformPose(follower.transform)}]");
            foreach (string field in new[] { "hmdTransform", "controllerTrackingOrigin", "rightControllerTarget", "leftControllerTarget", "handTarget", "leftHandTarget" })
            {
                var t = typeof(VRHandTargetFollower).GetField(field, PrivateInstance)?.GetValue(follower) as Transform;
                entries.Add($"{field}=[{TransformPose(t)}]");
            }
            var job = follower.GetComponent<PlayerJobController>();
            if (job != null)
            {
                var anchor = typeof(PlayerJobController).GetField("localWeaponAnchor", PrivateInstance)?.GetValue(job) as Transform;
                var weapon = typeof(PlayerJobController).GetField("activeLocalWeapon", PrivateInstance)?.GetValue(job) as GameObject;
                entries.Add($"anchor=[{TransformPose(anchor)}] weapon=[{TransformPose(weapon != null ? weapon.transform : null)}]");
            }
        }
        return $"frame={Time.frameCount} spaceDown={Keyboard.current?.spaceKey.wasPressedThisFrame} " +
            $"simulatorId={InstanceId(this)} simulatorEnabled={isActiveAndEnabled} mouseSpace={mouseTranslateSpace} keyboardSpace={keyboardTranslateSpace} " +
            $"mode={mouseTransformationMode} selected={Read(Selection, this)} camera=[{TransformPose(cameraTransform)}] " +
            string.Join(" | ", entries);
    }

    static string TransformPose(Transform t)
    {
        if (t == null) return "missing";
        string path = t.name;
        for (var parent = t.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        var driver = t.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        var controls = new List<string>();
        var action = driver != null ? driver.positionInput.action : null;
        if (action != null)
            foreach (var control in action.controls) controls.Add($"{control.path}:deviceId={control.device.deviceId}");
        return $"id={InstanceId(t)} scene={t.gameObject.scene.name} path={path} parentId={(t.parent != null ? InstanceId(t.parent) : 0)} " +
            $"activeSelf={t.gameObject.activeSelf} activeInHierarchy={t.gameObject.activeInHierarchy} " +
            $"cameraEnabled={(t.GetComponent<Camera>() != null ? t.GetComponent<Camera>().enabled.ToString() : "none")} " +
            $"worldP={t.position.ToString("F5")} worldR={t.rotation.ToString("F5")} " +
            $"localP={t.localPosition.ToString("F5")} localR={t.localRotation.ToString("F5")} " +
            $"TPD={(driver != null ? driver.enabled.ToString() : "none")} " +
            $"ignoreTrackingState={(driver != null ? driver.ignoreTrackingState.ToString() : "none")} " +
            $"positionControls=[{string.Join(",", controls)}]";
    }

    void LogInputFrame(bool baseUpdateCompleted)
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var space = keyboard?.spaceKey;
        Vector2 rawMouse = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        var right = manipulateRightAction != null ? manipulateRightAction.action : null;
        bool pressed = right != null && right.IsPressed();
        object selected = Selection?.GetValue(this);
        bool rightChanged = pressed != lastRightPressed;
        bool selectionChanged = !Equals(selected, lastSelection);
        bool spaceDown = space != null && space.wasPressedThisFrame;
        bool spaceUp = space != null && space.wasReleasedThisFrame;
        bool spaceMouseMoving = space != null && space.isPressed && rawMouse != Vector2.zero;
        bool mouseStarted = spaceMouseMoving && !wasSpaceMouseMoving;
        lastRightPressed = pressed;
        lastSelection = selected;
        wasSpaceMouseMoving = spaceMouseMoving;

        // Transitions and the first Space+mouse frame are immediate; sustained motion is sampled at 2 Hz.
        if (!spaceDown && !spaceUp && !rightChanged && !selectionChanged &&
            !(spaceMouseMoving && (mouseStarted || Time.unscaledTime >= nextInputLog))) return;
        nextInputLog = Time.unscaledTime + 0.5f;
        Trace($"Input baseUpdateCompleted={baseUpdateCompleted} spaceDown={spaceDown} spaceUp={spaceUp} " +
            $"rightChanged={rightChanged} selectionChanged={selectionChanged} spaceMouseMoving={spaceMouseMoving} " +
            $"rawMouseDelta={rawMouse.ToString("F3")}");
    }

    void Trace(string phase)
    {
        var simulators = Object.FindObjectsByType<XRDeviceSimulator>(FindObjectsInactive.Include);
        var instances = new List<string>();
        int activeCount = 0;
        foreach (var simulator in simulators)
        {
            if (simulator.isActiveAndEnabled) ++activeCount;
            instances.Add($"{InstanceId(simulator)}:{(simulator.isActiveAndEnabled ? "active" : "inactive")}");
        }

        var right = manipulateRightAction != null ? manipulateRightAction.action : null;
        var toggle = toggleManipulateRightAction != null ? toggleManipulateRightAction.action : null;
        var y = keyboardYTranslateAction != null ? keyboardYTranslateAction.action : null;
        var mouse = mouseDeltaAction != null ? mouseDeltaAction.action : null;
        var space = Keyboard.current?.spaceKey;
        var controls = new List<string>();
        var bindings = new List<string>();
        if (right != null)
        {
            foreach (var control in right.controls)
                controls.Add(control.path);
            foreach (var binding in right.bindings)
                bindings.Add($"path={binding.path},effectivePath={binding.effectivePath}," +
                    $"interactions={binding.interactions},processors={binding.processors}");
        }
        var manager = Lifecycle?.GetValue(this) as SimulatedDeviceLifecycleManager;
        var ownedRight = manager != null ? OwnedRight?.GetValue(manager) as XRSimulatedController : null;
        var rightDevices = new List<string>();
        foreach (var device in InputSystem.devices)
            if (device is XRSimulatedController controller && HasRightUsage(controller))
                rightDevices.Add(DevicePose(controller));

        object selected = Selection?.GetValue(this);
        string hmdSelected = selected is System.Enum targets
            ? targets.HasFlag((System.Enum)System.Enum.Parse(targets.GetType(), "HMD")).ToString() : "unavailable";
        string camera = ReferenceEquals(cameraTransform, null) ? "null"
            : cameraTransform == null ? "destroyed" : $"valid:{InstanceId(cameraTransform)}";
        object state = RightState?.GetValue(this);
        string statePose = state is XRSimulatedControllerState pose
            ? $"position={pose.devicePosition.ToString("F5")},rotation={pose.deviceRotation.ToString("F5")}"
            : "unavailable";

        Debug.Log($"[XRSimDiag] {phase} frame={Time.frameCount} instance={InstanceId(this)} " +
            $"classicCount={simulators.Length} activeCount={activeCount} ids=[{string.Join(",", instances)}] " +
            $"simulatorEnabled={enabled} activeInHierarchy={gameObject.activeInHierarchy} focused={Application.isFocused} " +
            $"simulatorActions={AssetState(deviceSimulatorActionAsset)} controllerActions={AssetState(controllerActionAsset)} " +
            $"manipulateRightEnabled={right?.enabled} manipulateRightPressed={right?.IsPressed()} " +
            $"space.isPressed={space?.isPressed} space.wasPressedThisFrame={space?.wasPressedThisFrame} space.wasReleasedThisFrame={space?.wasReleasedThisFrame} " +
            $"manipulateRightPhase={right?.phase} manipulateRightTriggered={right?.triggered} manipulateRightValue={right?.ReadValue<float>()} " +
            $"action.name={right?.name} action.id={right?.id} action.actionMap.name={right?.actionMap?.name} " +
            $"controls=[{string.Join(";", controls)}] bindings=[{string.Join(";", bindings)}] " +
            $"toggleRightEnabled={toggle?.enabled} toggleRightPerformed={toggle?.WasPerformedThisFrame()} " +
            $"yTranslateEnabled={y?.enabled} yTranslate={(y != null && y.enabled ? y.ReadValue<float>() : 0f)} internalY={Read(YInput, this)} " +
            $"mouseActionEnabled={mouse?.enabled} mouseAction={(mouse != null && mouse.enabled ? mouse.ReadValue<Vector2>().ToString("F3") : "unavailable")} internalMouse={Read(MouseInput, this)} " +
            $"selectedDevice={selected ?? "unavailable"} HMD={hmdSelected} leftDevice={manipulatingLeftDevice} rightDevice={manipulatingRightDevice} " +
            $"leftController={(manager != null ? manipulatingLeftController.ToString() : "unavailable")} rightController={(manager != null ? manipulatingRightController.ToString() : "unavailable")} " +
            $"deviceMode={(manager != null ? manager.deviceMode.ToString() : "missing")} positionTarget={(axis2DTargets & Axis2DTargets.Position) != 0} " +
            $"mouseMode={mouseTransformationMode} rotateOverride={Read(RotateOverride, this)} camera={camera} " +
            $"internalRight=[{statePose}] ownedRight=[{DevicePose(ownedRight)}] " +
            $"rightHandDeviceCount={rightDevices.Count} inputSystemRight=[{string.Join(";", rightDevices)}]", this);
    }

    static string AssetState(InputActionAsset asset) => asset == null ? "missing"
        : $"{asset.name}(id={InstanceId(asset)},enabled={asset.enabled})";

    // Same legacy ID representation as Unity's obsolete GetInstanceID wrapper.
    static int InstanceId(Object value) => unchecked((int)value.GetEntityId().GetRawData());

    static object Read(FieldInfo field, object target) => field?.GetValue(target) ?? "unavailable";

    static bool HasRightUsage(InputDevice device)
    {
        foreach (var usage in device.usages)
            if (usage == UnityEngine.InputSystem.CommonUsages.RightHand) return true;
        return false;
    }

    static string DevicePose(XRSimulatedController device) => device == null ? "missing"
        : $"deviceId={device.deviceId},name={device.name},added={device.added},enabled={device.enabled},rightUsage={HasRightUsage(device)}," +
          $"isTracked={device.isTracked.isPressed},trackingState={device.trackingState.ReadValue()}," +
          $"position={device.devicePosition.ReadValue().ToString("F5")},rotation={device.deviceRotation.ReadValue().ToString("F5")}";
#endif
}
#pragma warning restore CS0618
