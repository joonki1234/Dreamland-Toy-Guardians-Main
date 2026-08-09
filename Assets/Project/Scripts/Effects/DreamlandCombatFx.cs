using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 외부 VFX 프리팹 없이 전투 피드백을 강화하기 위한 런타임 이펙트 유틸리티입니다.
    /// 짧은 파티클만 생성하며 생성 즉시 자동 정리됩니다.
    /// </summary>
    public static class DreamlandCombatFx
    {
        private static Material additiveMaterial;

        public static void SpawnHeadbuttImpact(
            Vector3 position,
            Vector3 incomingDirection)
        {
            Vector3 direction = incomingDirection.sqrMagnitude > 0.0001f
                ? incomingDirection.normalized
                : Vector3.up;

            SpawnBurst(
                "Core_HeadbuttImpact",
                position,
                direction,
                new Color(1f, 0.12f, 0.03f, 1f),
                new Color(1f, 0.82f, 0.18f, 0.9f),
                46,
                0.07f,
                0.22f,
                1.7f,
                4.2f,
                0.18f,
                0.46f,
                0.28f,
                1.1f);

            SpawnBurst(
                "Core_HeadbuttShockRing",
                position,
                Vector3.up,
                new Color(1f, 0.30f, 0.05f, 0.8f),
                new Color(1f, 0.04f, 0.02f, 0.15f),
                28,
                0.10f,
                0.26f,
                2.0f,
                3.8f,
                0.18f,
                0.38f,
                0.45f,
                1.0f,
                ParticleSystemShapeType.Circle);
        }

        public static void SpawnMuzzleFlash(
            Vector3 position,
            Vector3 forward,
            Color color)
        {
            Vector3 direction = forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;

            SpawnBurst(
                "Enemy_MuzzleFlash",
                position,
                direction,
                color,
                new Color(1f, 0.85f, 0.28f, 0.85f),
                16,
                0.035f,
                0.12f,
                1.2f,
                3.2f,
                0.06f,
                0.16f,
                0.06f,
                0.8f);
        }

        public static void SpawnCoreProjectileImpact(
            Vector3 position,
            Color color)
        {
            SpawnBurst(
                "Core_RangedImpact",
                position,
                Vector3.up,
                color,
                Color.white,
                28,
                0.04f,
                0.13f,
                0.9f,
                2.6f,
                0.10f,
                0.28f,
                0.18f,
                0.9f,
                ParticleSystemShapeType.Sphere);
        }

        public static void SpawnDroneLaserImpact(
            Vector3 position,
            Color color)
        {
            SpawnBurst(
                "Core_DroneLaserImpact",
                position,
                Vector3.up,
                color,
                new Color(0.75f, 1f, 1f, 0.95f),
                34,
                0.045f,
                0.15f,
                0.7f,
                2.2f,
                0.12f,
                0.34f,
                0.22f,
                0.95f,
                ParticleSystemShapeType.Sphere);
        }

        public static void SpawnChargeDust(Vector3 position)
        {
            SpawnBurst(
                "Enemy_ChargeDust",
                position,
                Vector3.up,
                new Color(0.95f, 0.65f, 0.30f, 0.65f),
                new Color(0.40f, 0.22f, 0.12f, 0.25f),
                18,
                0.05f,
                0.15f,
                0.3f,
                1.2f,
                0.18f,
                0.40f,
                0.20f,
                0.8f,
                ParticleSystemShapeType.Hemisphere);
        }

        private static void SpawnBurst(
            string objectName,
            Vector3 position,
            Vector3 forward,
            Color startColor,
            Color endColor,
            int count,
            float sizeMin,
            float sizeMax,
            float speedMin,
            float speedMax,
            float lifeMin,
            float lifeMax,
            float radius,
            float lifetime,
            ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone)
        {
            GameObject effectObject = new GameObject(objectName);
            effectObject.transform.position = position;

            if (forward.sqrMagnitude > 0.0001f)
            {
                effectObject.transform.rotation =
                    Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            ParticleSystem particles =
                effectObject.AddComponent<ParticleSystem>();
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.12f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, count + 4);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(
                    0f,
                    (short)Mathf.Clamp(count, 1, short.MaxValue))
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = Mathf.Max(0.01f, radius);

            if (shapeType == ParticleSystemShapeType.Cone)
            {
                shape.angle = 24f;
                shape.length = 0.08f;
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer =
                effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetAdditiveMaterial();
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            particles.Play();
            Object.Destroy(effectObject, Mathf.Max(0.5f, lifetime));
        }

        private static Material GetAdditiveMaterial()
        {
            if (additiveMaterial != null)
            {
                return additiveMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            shader ??= Shader.Find("Particles/Standard Unlit");
            shader ??= Shader.Find("Sprites/Default");
            shader ??= Shader.Find("Unlit/Color");

            if (shader == null)
            {
                return null;
            }

            additiveMaterial = new Material(shader)
            {
                name = "DreamlandCombatFx_Runtime",
                hideFlags = HideFlags.DontSave
            };

            return additiveMaterial;
        }
    }
}
