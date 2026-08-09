using System.Collections.Generic;
using UnityEngine;

namespace DreamGuardians
{
    /// <summary>
    /// 사용자가 프로젝트에 추가한 Sci-Fi GUI Skin / Strategic Warfare UI의
    /// 일부 그래픽을 Resources 기반 HUD 스킨으로 제공합니다.
    /// </summary>
    public static class DreamlandUiSkin
    {
        private static readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>();

        public static Sprite SciFiWindow =>
            LoadSprite("scifi_window", new Vector4(95f, 95f, 95f, 95f));

        public static Sprite SciFiBarBackground =>
            LoadSprite("scifi_bar_bg", new Vector4(65f, 55f, 65f, 55f));

        public static Sprite SciFiBarGreen =>
            LoadSprite("scifi_bar_green", new Vector4(40f, 32f, 40f, 32f));

        public static Sprite SciFiBarRed =>
            LoadSprite("scifi_bar_red", new Vector4(40f, 32f, 40f, 32f));

        public static Sprite SciFiBarPurple =>
            LoadSprite("scifi_bar_purple", new Vector4(40f, 32f, 40f, 32f));

        public static Sprite SciFiBullets =>
            LoadSprite("scifi_bullets", Vector4.zero);

        public static Sprite SciFiRocket =>
            LoadSprite("scifi_rocket", Vector4.zero);

        public static Sprite StrategicMissionPanel =>
            LoadSprite("strategic_mission_panel", new Vector4(70f, 70f, 70f, 70f));

        public static Sprite StrategicWarningPanel =>
            LoadSprite("strategic_warning_panel", new Vector4(60f, 55f, 60f, 55f));

        public static Sprite StrategicCoreStrip =>
            LoadSprite("strategic_core_strip", new Vector4(55f, 45f, 55f, 45f));

        public static Sprite StrategicEnemyStrip =>
            LoadSprite("strategic_enemy_strip", new Vector4(55f, 45f, 55f, 45f));

        public static Sprite StrategicRoleStrip =>
            LoadSprite("strategic_role_strip", new Vector4(55f, 45f, 55f, 45f));

        public static Sprite StrategicShield =>
            LoadSprite("strategic_shield_icon", Vector4.zero);

        public static Sprite StrategicLock =>
            LoadSprite("strategic_lock_icon", Vector4.zero);

        public static Sprite StrategicLightning =>
            LoadSprite("strategic_lightning_icon", Vector4.zero);

        // Kenney UI Pack - Sci-Fi (Blue theme)
        // Editor importer copies only the selected source PNGs into Resources/KenneySciFiUI.
        public static Sprite KenneyMissionPanel =>
            LoadRelativeSprite("KenneySciFiUI/mission_panel", 0.16f) ?? SciFiWindow;

        public static Sprite KenneyCounterPanel =>
            LoadRelativeSprite("KenneySciFiUI/counter_panel", 0.16f) ?? SciFiWindow;

        public static Sprite KenneyBossPanel =>
            LoadRelativeSprite("KenneySciFiUI/boss_panel", 0.16f) ?? SciFiWindow;

        public static Sprite KenneyCoreBarBlue =>
            LoadRelativeSprite("KenneySciFiUI/core_bar_blue", 0.30f) ?? SciFiBarBackground;

        public static Sprite KenneyCoreBarGreen =>
            LoadRelativeSprite("KenneySciFiUI/core_bar_green", 0.30f) ?? SciFiBarGreen;

        public static Sprite KenneyCoreBarRed =>
            LoadRelativeSprite("KenneySciFiUI/core_bar_red", 0.30f) ?? SciFiBarRed;

        public static Sprite KenneyBossBarBlue =>
            LoadRelativeSprite("KenneySciFiUI/boss_bar_blue", 0.30f) ?? SciFiBarBackground;

        public static Sprite KenneyBossBarRed =>
            LoadRelativeSprite("KenneySciFiUI/boss_bar_red", 0.30f) ?? SciFiBarRed;


        private static Sprite LoadRelativeSprite(string resourcePath, float borderRatio)
        {
            string key = resourcePath + ":relative:" + borderRatio;
            if (spriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            float maxBorder = Mathf.Max(0f, Mathf.Min(texture.width, texture.height) * 0.45f);
            float border = Mathf.Clamp(
                Mathf.Min(texture.width, texture.height) * Mathf.Clamp01(borderRatio),
                0f,
                maxBorder);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));

            sprite.name = texture.name + "_RuntimeSprite";
            spriteCache[key] = sprite;
            return sprite;
        }

        private static Sprite LoadSprite(string resourceName, Vector4 border)
        {
            string key = resourceName + ":" + border;
            if (spriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(
                "DreamlandUI/" + resourceName);

            if (texture == null)
            {
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);

            sprite.name = resourceName + "_RuntimeSprite";
            spriteCache[key] = sprite;
            return sprite;
        }
    }
}
