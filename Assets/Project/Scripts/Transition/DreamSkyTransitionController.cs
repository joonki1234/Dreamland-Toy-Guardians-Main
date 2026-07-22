using System.Collections;
using UnityEngine;

namespace DreamGuardians
{
    [DisallowMultipleComponent]
    public sealed class DreamSkyTransitionController : MonoBehaviour
    {
        [Header("하늘 머티리얼")]
        [Tooltip("가상 세계가 처음 드러날 때 사용할 파란 하늘")]
        [SerializeField] private Material blueSkyMaterial;

        [Tooltip("꿈나라가 완성됐을 때 사용할 분홍 하늘")]
        [SerializeField] private Material pinkSkyMaterial;

        [Header("전환 설정")]
        [Tooltip("파란 하늘에서 분홍 하늘로 변하는 시간")]
        [SerializeField, Min(0.1f)]
        private float transitionDuration = 5f;

        [Tooltip("게임 시작 시 파란 하늘을 바로 적용할지 여부")]
        [SerializeField]
        private bool applyBlueSkyOnStart = true;

        private Material runtimeSkyMaterial;
        private Coroutine transitionRoutine;

        private void Start()
        {
            if (applyBlueSkyOnStart)
            {
                ApplyBlueSkyImmediately();
            }
        }

        /// <summary>
        /// 파란 하늘을 즉시 적용한다.
        /// </summary>
        public void ApplyBlueSkyImmediately()
        {
            if (blueSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] 파란 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            CreateRuntimeMaterialIfNeeded(blueSkyMaterial);

            runtimeSkyMaterial.CopyPropertiesFromMaterial(blueSkyMaterial);
            RenderSettings.skybox = runtimeSkyMaterial;

            RefreshLighting();

            Debug.Log("[DreamSkyTransition] 파란 하늘 적용 완료", this);
        }

        /// <summary>
        /// 현재 하늘에서 분홍 하늘로 천천히 전환한다.
        /// </summary>
        public void TransitionToPinkSky()
        {
            if (blueSkyMaterial == null || pinkSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] 파란 하늘 또는 분홍 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            transitionRoutine = StartCoroutine(
                TransitionRoutine(
                    pinkSkyMaterial,
                    transitionDuration)
            );
        }

        /// <summary>
        /// 분홍 하늘을 즉시 적용한다.
        /// 테스트 또는 최종 상태 복구에 사용한다.
        /// </summary>
        public void ApplyPinkSkyImmediately()
        {
            if (pinkSkyMaterial == null)
            {
                Debug.LogWarning(
                    "[DreamSkyTransition] 분홍 하늘 머티리얼이 연결되지 않았습니다.",
                    this);

                return;
            }

            StopCurrentTransition();

            CreateRuntimeMaterialIfNeeded(pinkSkyMaterial);

            runtimeSkyMaterial.CopyPropertiesFromMaterial(pinkSkyMaterial);
            RenderSettings.skybox = runtimeSkyMaterial;

            RefreshLighting();

            Debug.Log("[DreamSkyTransition] 분홍 하늘 적용 완료", this);
        }

        private IEnumerator TransitionRoutine(
            Material targetMaterial,
            float duration)
        {
            Material currentSky = RenderSettings.skybox;

            if (currentSky == null)
            {
                currentSky = blueSkyMaterial;
            }

            /*
             * 전환 중 원본 에셋 머티리얼이 바뀌지 않도록
             * 시작 상태를 별도의 임시 머티리얼에 복사한다.
             */
            Material startMaterial = new Material(currentSky);

            CreateRuntimeMaterialIfNeeded(startMaterial);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float ratio = Mathf.Clamp01(
                    elapsed / duration
                );

                float easedRatio = Mathf.SmoothStep(
                    0f,
                    1f,
                    ratio
                );

                runtimeSkyMaterial.Lerp(
                    startMaterial,
                    targetMaterial,
                    easedRatio
                );

                RenderSettings.skybox = runtimeSkyMaterial;

                yield return null;
            }

            runtimeSkyMaterial.CopyPropertiesFromMaterial(targetMaterial);
            RenderSettings.skybox = runtimeSkyMaterial;

            Destroy(startMaterial);

            transitionRoutine = null;

            RefreshLighting();

            Debug.Log("[DreamSkyTransition] 분홍 하늘 전환 완료", this);
        }

        private void CreateRuntimeMaterialIfNeeded(Material sourceMaterial)
        {
            if (runtimeSkyMaterial != null)
            {
                return;
            }

            runtimeSkyMaterial = new Material(sourceMaterial)
            {
                name = "DreamSky_Runtime"
            };

            RenderSettings.skybox = runtimeSkyMaterial;
        }

        private void StopCurrentTransition()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        private void RefreshLighting()
        {
            /*
             * Skybox 변경 결과를 환경광에 다시 반영한다.
             * 실시간으로 자주 호출하면 무거울 수 있으므로
             * 전환 시작·종료 시점에만 호출한다.
             */
            DynamicGI.UpdateEnvironment();
        }

        private void OnDestroy()
        {
            if (runtimeSkyMaterial != null)
            {
                Destroy(runtimeSkyMaterial);
            }
        }

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(
                0.1f,
                transitionDuration
            );
        }

        #if UNITY_EDITOR
        [ContextMenu("테스트 - 분홍 하늘로 전환")]
        private void TestTransitionToPink()
        {
            TransitionToPinkSky();
        }

        [ContextMenu("테스트 - 파란 하늘 즉시 적용")]
        private void TestApplyBlue()
        {
            ApplyBlueSkyImmediately();
        }
        #endif
    }
}