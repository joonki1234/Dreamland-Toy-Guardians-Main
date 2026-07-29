using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class DreamSkyTransitionController : MonoBehaviour
    {
        [Header("하늘 머티리얼")]

        [Tooltip("가상 세계가 처음 드러날 때 사용할 파란 하늘")]
        [SerializeField]
        private Material blueSkyMaterial;

        [Tooltip("꿈나라가 완성됐을 때 사용할 분홍 하늘")]
        [SerializeField]
        private Material pinkSkyMaterial;


        [Header("전환 설정")]

        [Tooltip("파란 하늘에서 분홍 하늘로 변하는 시간")]
        [SerializeField, Min(0.1f)]
        private float transitionDuration = 5f;


        [Header("시작 하늘 설정")]

        [Tooltip(
            "게임 시작 시 Skybox를 제거해 하늘이 보이지 않게 합니다. " +
            "현재 튜토리얼과 Stage 1에서는 체크하는 것이 맞습니다.")]
        [SerializeField]
        private bool hideSkyOnStart = true;

        [Tooltip(
            "게임 시작 시 파란 하늘을 즉시 적용합니다. " +
            "현재 진행에서는 체크를 해제합니다.")]
        [SerializeField]
        private bool applyBlueSkyOnStart = false;


        private Material runtimeSkyMaterial;
        private Coroutine transitionRoutine;


        private void Start()
        {
            /*
             * 시작 시 하늘 숨김이 우선입니다.
             *
             * 실수로 Hide Sky On Start와
             * Apply Blue Sky On Start를 둘 다 체크해도
             * 하늘을 숨긴 상태로 시작합니다.
             */
            if (hideSkyOnStart)
            {
                HideSkyImmediately();
                return;
            }

            if (applyBlueSkyOnStart)
            {
                ApplyBlueSkyImmediately();
            }
        }


        /// <summary>
        /// 현재 Skybox를 제거해 하늘이 보이지 않게 합니다.
        ///
        /// 게임 시작, 튜토리얼, Stage 1 현실 상태에 사용합니다.
        /// </summary>
        public void HideSkyImmediately()
        {
            StopCurrentTransition();

            RenderSettings.skybox = null;

            RefreshLighting();

            Debug.Log(
                "[DreamSkyTransition] 하늘 숨김 완료",
                this);
        }


        /// <summary>
        /// 파란 하늘을 즉시 적용합니다.
        /// Stage 2 진입 시 사용합니다.
        /// </summary>
        public void ApplyBlueSkyImmediately()
        {
            if (blueSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] " +
                    "파란 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            CreateRuntimeMaterialIfNeeded(
                blueSkyMaterial);

            runtimeSkyMaterial.CopyPropertiesFromMaterial(
                blueSkyMaterial);

            RenderSettings.skybox =
                runtimeSkyMaterial;

            RefreshLighting();

            Debug.Log(
                "[DreamSkyTransition] 파란 하늘 적용 완료",
                this);
        }


        /// <summary>
        /// 현재 하늘에서 분홍 하늘로 천천히 전환합니다.
        /// 완전 꿈나라 전환 시 사용합니다.
        /// </summary>
        public void TransitionToPinkSky()
        {
            if (pinkSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] " +
                    "분홍 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            transitionRoutine =
                StartCoroutine(
                    TransitionRoutine(
                        pinkSkyMaterial,
                        transitionDuration));
        }


        /// <summary>
        /// 분홍 하늘을 즉시 적용합니다.
        /// 테스트 또는 최종 상태 복구에 사용합니다.
        /// </summary>
        public void ApplyPinkSkyImmediately()
        {
            if (pinkSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] " +
                    "분홍 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            CreateRuntimeMaterialIfNeeded(
                pinkSkyMaterial);

            runtimeSkyMaterial.CopyPropertiesFromMaterial(
                pinkSkyMaterial);

            RenderSettings.skybox =
                runtimeSkyMaterial;

            RefreshLighting();

            Debug.Log(
                "[DreamSkyTransition] 분홍 하늘 적용 완료",
                this);
        }


        private IEnumerator TransitionRoutine(
            Material targetMaterial,
            float duration)
        {
            /*
             * 현재 Skybox가 없으면 파란 하늘을
             * 분홍 전환의 시작점으로 사용합니다.
             *
             * 정상 게임 흐름에서는 Stage 2에서 이미
             * 파란 하늘이 적용되어 있습니다.
             */
            Material currentSky =
                RenderSettings.skybox;

            if (currentSky == null)
            {
                if (blueSkyMaterial != null)
                {
                    currentSky =
                        blueSkyMaterial;
                }
                else
                {
                    currentSky =
                        targetMaterial;
                }
            }

            /*
             * 원본 에셋 머티리얼이 변경되지 않도록
             * 시작 상태를 별도의 임시 머티리얼에 복사합니다.
             */
            Material startMaterial =
                new Material(currentSky)
                {
                    name =
                        "DreamSky_Start_Runtime"
                };

            CreateRuntimeMaterialIfNeeded(
                startMaterial);

            float safeDuration =
                Mathf.Max(
                    0.1f,
                    duration);

            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed +=
                    Time.deltaTime;

                float ratio =
                    Mathf.Clamp01(
                        elapsed /
                        safeDuration);

                float easedRatio =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        ratio);

                runtimeSkyMaterial.Lerp(
                    startMaterial,
                    targetMaterial,
                    easedRatio);

                RenderSettings.skybox =
                    runtimeSkyMaterial;

                yield return null;
            }

            runtimeSkyMaterial.CopyPropertiesFromMaterial(
                targetMaterial);

            RenderSettings.skybox =
                runtimeSkyMaterial;

            Destroy(
                startMaterial);

            transitionRoutine = null;

            RefreshLighting();

            Debug.Log(
                "[DreamSkyTransition] 분홍 하늘 전환 완료",
                this);
        }


        private void CreateRuntimeMaterialIfNeeded(
            Material sourceMaterial)
        {
            if (runtimeSkyMaterial != null)
            {
                return;
            }

            runtimeSkyMaterial =
                new Material(sourceMaterial)
                {
                    name =
                        "DreamSky_Runtime"
                };

            RenderSettings.skybox =
                runtimeSkyMaterial;
        }


        private void StopCurrentTransition()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            StopCoroutine(
                transitionRoutine);

            transitionRoutine = null;
        }


        private void RefreshLighting()
        {
            DynamicGI.UpdateEnvironment();
        }


        private void OnDestroy()
        {
            StopCurrentTransition();

            if (runtimeSkyMaterial != null)
            {
                Destroy(
                    runtimeSkyMaterial);

                runtimeSkyMaterial = null;
            }
        }


        private void OnValidate()
        {
            transitionDuration =
                Mathf.Max(
                    0.1f,
                    transitionDuration);

            /*
             * 하늘 숨김을 선택했다면
             * 시작 시 파란 하늘 적용은 자동으로 해제합니다.
             */
            if (hideSkyOnStart)
            {
                applyBlueSkyOnStart = false;
            }
        }


#if UNITY_EDITOR

        [ContextMenu("테스트 - 하늘 숨기기")]
        private void TestHideSky()
        {
            HideSkyImmediately();
        }


        [ContextMenu("테스트 - 파란 하늘 즉시 적용")]
        private void TestApplyBlue()
        {
            ApplyBlueSkyImmediately();
        }


        [ContextMenu("테스트 - 분홍 하늘로 전환")]
        private void TestTransitionToPink()
        {
            TransitionToPinkSky();
        }


        [ContextMenu("테스트 - 분홍 하늘 즉시 적용")]
        private void TestApplyPink()
        {
            ApplyPinkSkyImmediately();
        }

#endif
    }
}