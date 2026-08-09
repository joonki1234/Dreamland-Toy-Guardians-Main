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
