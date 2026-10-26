// Generated from the player-facing option table and registrations in owned renderer test-build-c55fb3f.
// Only option names, value bounds, choices and preset values are mirrored; no DLL hash is used.
using System;
using System.Collections.Generic;
using System.Linq;

namespace EIStarter
{
    internal enum OwnedValueType { Boolean, Integer, Float }

    internal sealed class OwnedRendererOption
    {
        internal readonly string Key, English, Russian;
        internal readonly int Category;
        internal readonly OwnedValueType Type;
        internal readonly decimal Default, Minimum, Maximum, Step;
        internal readonly decimal[] Presets;
        internal readonly string[] Choices;
        internal bool InPreset => Presets != null;
        internal string Label => UiText.Language == "ru" ? Russian : English;
        internal OwnedRendererOption(string key, int category, OwnedValueType type, decimal initial,
            decimal minimum, decimal maximum, decimal step, decimal[] presets, string[] choices,
            string english, string russian)
        { Key = key; Category = category; Type = type; Default = initial; Minimum = minimum;
          Maximum = maximum; Step = step; Presets = presets; Choices = choices; English = english; Russian = russian; }
    }

    internal static class OwnedRendererSchema
    {
#if RESTARTER64
        internal const int PlayerSettingCount = 51;
#else
        internal const int PlayerSettingCount = 49;
#endif
        internal static readonly OwnedRendererOption[] Options = {
#if RESTARTER64
            // Renderer settings_version 3: dlss_frame.h and register_config_options.
            new OwnedRendererOption("upscaler", 0, OwnedValueType.Integer, 0m, 0m, 2m, 1m, null, new string[] { "Off", "DLSS", "FSR" }, "Upscaling", "Масштабирование"),
            new OwnedRendererOption("upscaler_quality", 0, OwnedValueType.Integer, 0m, 0m, 4m, 1m, null, new string[] { "Quality", "Balanced", "Performance", "Ultra Performance", "DLAA / FSR Native AA" }, "Upscaling quality", "Качество масштабирования"),
#endif
            new OwnedRendererOption("display_mode", 0, OwnedValueType.Integer, 0m, 0m, 1m, 1m, null, new string[] { "Borderless fullscreen", "Windowed" }, "Display mode", "Режим окна"),
            new OwnedRendererOption("window_width", 0, OwnedValueType.Integer, 0m, 0m, 16384m, 1m, null, null, "Window size", "Ширина окна"),
            new OwnedRendererOption("window_height", 0, OwnedValueType.Integer, 0m, 0m, 16384m, 1m, null, null, "Window height", "Высота окна"),
            new OwnedRendererOption("render_scale", 0, OwnedValueType.Integer, 100m, 50m, 100m, 1m, new decimal[] { 67m, 85m, 100m, 100m }, null, "Render scale", "Масштаб рендера (%)"),
            new OwnedRendererOption("smaa", 0, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 1m, 1m, 1m, 1m }, null, "Anti-aliasing (SMAA)", "Сглаживание (SMAA)"),
            new OwnedRendererOption("anisotropy", 0, OwnedValueType.Integer, 3m, 0m, 4m, 1m, new decimal[] { 2m, 3m, 3m, 4m }, new string[] { "1x", "2x", "4x", "8x", "16x" }, "Texture filtering", "Фильтрация текстур"),
            new OwnedRendererOption("vsync", 0, OwnedValueType.Boolean, 0m, 0m, 1m, 1m, null, null, "VSync", "Вертикальная синхронизация"),
            new OwnedRendererOption("frame_limit", 0, OwnedValueType.Integer, 0m, 0m, 500m, 1m, null, null, "Frame rate limit", "Ограничение FPS"),
            new OwnedRendererOption("shadows", 1, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 1m, 1m, 1m, 1m }, null, "Sun shadows", "Тени солнца"),
            new OwnedRendererOption("shadow_cascade_quality", 1, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Low", "Medium", "High" }, "Shadow resolution", "Разрешение теней солнца"),
            new OwnedRendererOption("shadow_close_cascades", 1, OwnedValueType.Integer, 3m, 1m, 3m, 1m, new decimal[] { 2m, 3m, 3m, 3m }, new string[] { "1", "2", "3" }, "Shadow layers near the camera", "Ближние каскады теней"),
            new OwnedRendererOption("shadow_far", 1, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 1m, 1m, 1m }, null, "Distant shadows", "Дальние тени"),
            new OwnedRendererOption("shadow_softness", 1, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Off", "Low", "High" }, "Soft shadows", "Мягкость теней"),
            new OwnedRendererOption("point_shadows", 1, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 1m, 1m, 1m }, null, "Lamp and torch shadows", "Тени от ламп и факелов"),
            new OwnedRendererOption("point_shadow_quality", 1, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Low", "Medium", "High" }, "Lamp shadow resolution", "Разрешение теней источников света"),
            new OwnedRendererOption("ao_quality", 1, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Off", "Low", "High" }, "Ambient occlusion", "Затенение в углублениях (AO)"),
            new OwnedRendererOption("emissive_lights", 1, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Off", "On", "Fine" }, "Lava glow lighting", "Свечение лавы и материалов"),
            new OwnedRendererOption("cave_brightness", 1, OwnedValueType.Float, 0.7m, 0.25m, 1.25m, 0.05m, null, null, "Cave brightness", "Яркость пещер"),
            new OwnedRendererOption("terrain_quality", 2, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Low", "Medium", "High" }, "Terrain detail", "Детализация рельефа"),
            new OwnedRendererOption("terrain_blend", 2, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Off", "Fast", "Full" }, "Terrain blending", "Смешивание поверхностей"),
            new OwnedRendererOption("terrain_cliffs", 2, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 1m, 1m, 1m }, null, "Cliff texturing", "Текстуры обрывов"),
            new OwnedRendererOption("contact_blend", 2, OwnedValueType.Float, 0.7m, 0m, 1m, 0.05m, new decimal[] { 0m, 0.7m, 0.7m, 0.7m }, null, "Ground contact blend", "Смешивание объектов с землёй"),
            new OwnedRendererOption("terrain_tessellation", 2, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 0m, 1m, 1m }, new string[] { "Off", "On", "Fine" }, "Smooth cliff shapes", "Сглаживание формы обрывов"),
            new OwnedRendererOption("water_reflections", 3, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 0m, 1m, 1m }, null, "Water reflections", "Отражения на воде"),
            new OwnedRendererOption("water_physical", 3, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 0m, 1m, 1m }, null, "Realistic water", "Реалистичная вода"),
            new OwnedRendererOption("ground_cover", 4, OwnedValueType.Integer, 2m, 0m, 3m, 1m, new decimal[] { 0m, 1m, 2m, 3m }, new string[] { "Off", "Low", "Normal", "High" }, "Grass and ground cover", "Трава и мелкие детали"),
            new OwnedRendererOption("ground_cover_distance", 4, OwnedValueType.Integer, 200m, 50m, 1000m, 25m, new decimal[] { 100m, 100m, 200m, 300m }, null, "Ground cover distance (m)", "Дальность травы (м)"),
            new OwnedRendererOption("tree_sway", 4, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, null, null, "Swaying trees", "Покачивание деревьев"),
            new OwnedRendererOption("sky_clouds", 5, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Original", "Low", "High" }, "Sky and clouds", "Небо и облака"),
            new OwnedRendererOption("fog_preset", 5, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Faithful", "Atmospheric", "Volumetric" }, "Fog", "Туман"),
            new OwnedRendererOption("fog_volume_quality", 5, OwnedValueType.Integer, 1m, 0m, 1m, 1m, new decimal[] { 0m, 0m, 1m, 1m }, new string[] { "Low", "High" }, "Volumetric fog quality", "Качество объёмного тумана"),
            new OwnedRendererOption("sun_shafts", 5, OwnedValueType.Integer, 2m, 0m, 3m, 1m, new decimal[] { 0m, 1m, 2m, 3m }, new string[] { "Off", "Simple", "Volumetric", "Both" }, "Sun shafts", "Солнечные лучи"),
            new OwnedRendererOption("fog_strength", 5, OwnedValueType.Float, 1m, 0m, 3m, 0.05m, null, null, "Fog strength", "Плотность тумана"),
            new OwnedRendererOption("weather_wind_variation", 5, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, null, null, "Changing wind", "Изменчивый ветер"),
            new OwnedRendererOption("bloom", 6, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Off", "Low", "High" }, "Bloom", "Свечение ярких объектов"),
            new OwnedRendererOption("depth_of_field", 6, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 2m, 2m }, new string[] { "Off", "Low", "High" }, "Depth of field", "Глубина резкости"),
            new OwnedRendererOption("depth_of_field_strength", 6, OwnedValueType.Float, 1m, 0m, 3m, 0.05m, null, null, "Depth of field strength", "Сила размытия"),
            new OwnedRendererOption("ambient_particles", 6, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Off", "Low", "High" }, "Ambient particles", "Частицы окружения"),
            new OwnedRendererOption("wind_trails", 6, OwnedValueType.Integer, 1m, 0m, 2m, 1m, new decimal[] { 0m, 1m, 1m, 2m }, new string[] { "Off", "Normal", "Dense" }, "Wind streaks", "Порывы ветра"),
            new OwnedRendererOption("effect_distance", 6, OwnedValueType.Integer, 300m, 100m, 1000m, 25m, new decimal[] { 100m, 200m, 300m, 500m }, null, "Effect distance (m)", "Дальность эффектов (м)"),
            new OwnedRendererOption("neighbour_maps", 7, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, new decimal[] { 0m, 1m, 1m, 1m }, null, "Neighbouring maps", "Соседние карты"),
            new OwnedRendererOption("draw_distance", 7, OwnedValueType.Integer, 300m, 100m, 1000m, 25m, new decimal[] { 100m, 200m, 300m, 500m }, null, "Draw distance (m)", "Дальность прорисовки (м)"),
            new OwnedRendererOption("neighbour_fog_range", 7, OwnedValueType.Float, 1m, 0.5m, 4m, 0.25m, null, null, "Neighbour haze depth", "Глубина дымки соседних карт"),
            new OwnedRendererOption("critters", 7, OwnedValueType.Integer, 2m, 0m, 2m, 1m, new decimal[] { 1m, 1m, 2m, 2m }, new string[] { "Off", "Low", "High" }, "Critters", "Мелкие животные"),
            new OwnedRendererOption("cursor_scale", 8, OwnedValueType.Integer, 100m, 100m, 400m, 25m, null, null, "Cursor size (%)", "Размер курсора (%)"),
            new OwnedRendererOption("confine_cursor", 8, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, null, null, "Keep cursor in the window", "Удерживать курсор в окне"),
            new OwnedRendererOption("highlight_briefing", 8, OwnedValueType.Boolean, 0m, 0m, 1m, 1m, null, null, "Highlight briefing characters", "Подсветка персонажей на базе"),
            new OwnedRendererOption("highlight_selected", 8, OwnedValueType.Boolean, 0m, 0m, 1m, 1m, null, null, "Highlight selected characters", "Подсветка выбранных персонажей"),
            new OwnedRendererOption("highlight_hover_characters", 8, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, null, null, "Highlight characters under the cursor", "Подсветка персонажей под курсором"),
            new OwnedRendererOption("highlight_hover_objects", 8, OwnedValueType.Boolean, 1m, 0m, 1m, 1m, null, null, "Highlight objects under the cursor", "Подсветка предметов под курсором"),
        };
        internal static readonly Dictionary<string, OwnedRendererOption> ByKey =
            Options.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);
    }
}
