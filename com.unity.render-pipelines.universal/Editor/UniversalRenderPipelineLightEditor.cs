using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal
{
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(UniversalRenderPipelineAsset))]
    class UniversalRenderPipelineLightEditor : LightEditor
    {
        AnimBool m_AnimSpotOptions = new AnimBool();
        AnimBool m_AnimPointOptions = new AnimBool();
        AnimBool m_AnimDirOptions = new AnimBool();
        AnimBool m_AnimAreaOptions = new AnimBool();
        AnimBool m_AnimRuntimeOptions = new AnimBool();
        AnimBool m_AnimShadowOptions = new AnimBool();
        AnimBool m_AnimShadowAngleOptions = new AnimBool();
        AnimBool m_AnimShadowRadiusOptions = new AnimBool();
        AnimBool m_AnimShadowResolutionOptions = new AnimBool();
        AnimBool m_AnimLightBounceIntensity = new AnimBool();

        class Styles
        {
            public readonly GUIContent SpotAngle = EditorGUIUtility.TrTextContent("Spot Angle", "Controls the angle in degrees at the base of a Spot light's cone.");

            public readonly GUIContent BakingWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. Enable Baked Global Illumination to use Mixed or Baked light modes.");
            public readonly GUIContent DisabledLightWarning = EditorGUIUtility.TrTextContent("Lighting has been disabled in at least one Scene view. Any changes applied to lights in the Scene will not be updated in these views until Lighting has been enabled again.");
            public readonly GUIContent SunSourceWarning = EditorGUIUtility.TrTextContent("This light is set as the current Sun Source, which requires a directional light. Go to the Lighting Window's Environment settings to edit the Sun Source.");

            public static readonly GUIContent ShadowRealtimeSettings = EditorGUIUtility.TrTextContent("Realtime Shadows", "Settings for realtime direct shadows.");
            public static readonly GUIContent ShadowStrength = EditorGUIUtility.TrTextContent("Strength", "Controls how dark the shadows cast by the light will be.");
            public static readonly GUIContent ShadowNearPlane = EditorGUIUtility.TrTextContent("Near Plane", "Controls the value for the near clip plane when rendering shadows. Currently clamped to 0.1 units or 1% of the lights range property, whichever is lower.");
            public static readonly GUIContent ShadowNormalBias = EditorGUIUtility.TrTextContent("Normal", "Controls the distance shadow caster vertices are offset along their normals when rendering shadow maps. Currently ignored for Point Lights.");

            // Resolution (default or custom)
            public static readonly GUIContent ShadowResolution = EditorGUIUtility.TrTextContent("Resolution", "Sets the rendered resolution of the shadow maps. A higher resolution increases the fidelity of shadows at the cost of GPU performance and memory usage. Rounded to the next power of two, and clamped to be at least 128.");
            public static readonly int[] ShadowResolutionDefaultValues =
            {
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium,
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh
            };
            public static readonly GUIContent[] ShadowResolutionDefaultOptions =
            {
                new GUIContent("Custom"),
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[0],
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[1],
                UniversalRenderPipelineAssetEditor.Styles.additionalLightsShadowResolutionTierNames[2],
            };

            // Bias (default or custom)
            public static GUIContent shadowBias = EditorGUIUtility.TrTextContent("Bias", "Select if the Bias should use the settings from the Pipeline Asset or Custom settings.");
            public static int[] optionDefaultValues = { 0, 1 };
            public static GUIContent[] displayedDefaultOptions =
            {
                new GUIContent("Custom"),
                new GUIContent("Use Pipeline Settings")
            };

            public static readonly GUIContent LightCookieSize = EditorGUIUtility.TrTextContent("Cookie Size", "Controls the size of the cookie mask currently assigned to the light.");
            public static readonly GUIContent LightCookieOffset = EditorGUIUtility.TrTextContent("Cookie Offset", "Controls the offset of the cookie mask currently assigned to the light.");
        }

        static Styles s_Styles;

        public bool typeIsSame { get { return !settings.lightType.hasMultipleDifferentValues; } }
        public bool shadowTypeIsSame { get { return !settings.shadowsType.hasMultipleDifferentValues; } }
        public bool lightmappingTypeIsSame { get { return !settings.lightmapping.hasMultipleDifferentValues; } }
        public Light lightProperty { get { return target as Light; } }

        public bool spotOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Spot; } }
        public bool pointOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Point; } }
        public bool dirOptionsValue { get { return typeIsSame && lightProperty.type == LightType.Directional; } }
        public bool areaOptionsValue { get { return typeIsSame && (lightProperty.type == LightType.Rectangle || lightProperty.type == LightType.Disc); } }
        public bool shadowResolutionOptionsValue  { get { return spotOptionsValue || pointOptionsValue; } } // Currently only additional punctual lights can specify per-light shadow resolution

        //  Area light shadows not supported
        public bool runtimeOptionsValue { get { return typeIsSame && (lightProperty.type != LightType.Rectangle && !settings.isCompletelyBaked); } }
        public bool bakedShadowRadius { get { return typeIsSame && (lightProperty.type == LightType.Point || lightProperty.type == LightType.Spot) && settings.isBakedOrMixed; } }
        public bool bakedShadowAngle { get { return typeIsSame && lightProperty.type == LightType.Directional && settings.isBakedOrMixed; } }
        public bool shadowOptionsValue { get { return shadowTypeIsSame && lightProperty.shadows != LightShadows.None; } }
#pragma warning disable 618
        public bool bakingWarningValue { get { return !UnityEditor.Lightmapping.bakedGI && lightmappingTypeIsSame && settings.isBakedOrMixed; } }
#pragma warning restore 618
        public bool showLightBounceIntensity { get { return true; } }

        public bool isShadowEnabled { get { return settings.shadowsType.intValue != 0; } }

        UniversalAdditionalLightData m_AdditionalLightData;
        SerializedObject m_AdditionalLightDataSO;

        SerializedProperty m_UseAdditionalDataProp;                     // Does light use shadow bias settings defined in UniversalRP asset file?
        SerializedProperty m_AdditionalLightsShadowResolutionTierProp;  // Index of the AdditionalLights ShadowResolution Tier
        SerializedProperty m_LightCookieSizeProp;                       // Multi dimensional light cookie size replacing `cookieSize` in legacy light.
        SerializedProperty m_LightCookieOffsetProp;                     // Multi dimensional light cookie offset.

        protected override void OnEnable()
        {
            MultipleAdditionalLightDataCheck();
            settings.OnEnable();
            UpdateShowOptions(true);
        }

        void init(List<Object> additionalLightData)
        {
            if (additionalLightData == null)
                return;
            m_AdditionalLightDataSO = new SerializedObject(additionalLightData.ToArray());
            m_UseAdditionalDataProp = m_AdditionalLightDataSO.FindProperty("m_UsePipelineSettings");
            m_AdditionalLightsShadowResolutionTierProp = m_AdditionalLightDataSO.FindProperty("m_AdditionalLightsShadowResolutionTier");
            m_LightCookieSizeProp = m_AdditionalLightDataSO.FindProperty("m_LightCookieSize");
            m_LightCookieOffsetProp = m_AdditionalLightDataSO.FindProperty("m_LightCookieOffset");

            settings.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            settings.Update();

            // Update AnimBool options. For properties changed they will be smoothly interpolated.
            UpdateShowOptions(false);

            settings.DrawLightType();

            Light light = target as Light;
            if (LightType.Directional != light.type && light == RenderSettings.sun)
            {
                EditorGUILayout.HelpBox(s_Styles.SunSourceWarning.text, MessageType.Warning);
            }

            EditorGUILayout.Space();

            // When we are switching between two light types that don't show the range (directional and area lights)
            // we want the fade group to stay hidden.
            using (var group = new EditorGUILayout.FadeGroupScope(1.0f - m_AnimDirOptions.faded))
                if (group.visible)
                #if UNITY_2020_1_OR_NEWER
                    settings.DrawRange();
                #else
                    settings.DrawRange(m_AnimAreaOptions.target);
                #endif

            // Spot angle
            using (var group = new EditorGUILayout.FadeGroupScope(m_AnimSpotOptions.faded))
                if (group.visible)
                    DrawSpotAngle();

            // Area width & height
            using (var group = new EditorGUILayout.FadeGroupScope(m_AnimAreaOptions.faded))
                if (group.visible)
                    settings.DrawArea();

            settings.DrawColor();

            EditorGUILayout.Space();

            CheckLightmappingConsistency();
            using (var group = new EditorGUILayout.FadeGroupScope(1.0f - m_AnimAreaOptions.faded))
                if (group.visible)
                {
                    if (light.type != LightType.Disc)
                    {
                        settings.DrawLightmapping();
                    }
                }

            settings.DrawIntensity();

            using (var group = new EditorGUILayout.FadeGroupScope(m_AnimLightBounceIntensity.faded))
                if (group.visible)
                    settings.DrawBounceIntensity();

            ShadowsGUI();
            LightCookieGUI();

            settings.DrawRenderMode();
            settings.DrawCullingMask();

            EditorGUILayout.Space();

            if (SceneView.lastActiveSceneView != null)
            {
#if UNITY_2019_1_OR_NEWER
                var sceneLighting = SceneView.lastActiveSceneView.sceneLighting;
#else
                var sceneLighting = SceneView.lastActiveSceneView.m_SceneLighting;
#endif
                if (!sceneLighting)
                    EditorGUILayout.HelpBox(s_Styles.DisabledLightWarning.text, MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void CheckLightmappingConsistency()
        {
            //Universal render-pipeline only supports baked area light, enforce it as this inspector is the universal one.
            if (settings.isAreaLightType && settings.lightmapping.intValue != (int)LightmapBakeType.Baked)
            {
                settings.lightmapping.intValue = (int)LightmapBakeType.Baked;
                serializedObject.ApplyModifiedProperties();
            }
        }

        void SetOptions(AnimBool animBool, bool initialize, bool targetValue)
        {
            if (initialize)
            {
                animBool.value = targetValue;
                animBool.valueChanged.AddListener(Repaint);
            }
            else
            {
                animBool.target = targetValue;
            }
        }

        void UpdateShowOptions(bool initialize)
        {
            SetOptions(m_AnimSpotOptions, initialize, spotOptionsValue);
            SetOptions(m_AnimPointOptions, initialize, pointOptionsValue);
            SetOptions(m_AnimDirOptions, initialize, dirOptionsValue);
            SetOptions(m_AnimAreaOptions, initialize, areaOptionsValue);
            SetOptions(m_AnimShadowOptions, initialize, shadowOptionsValue);
            SetOptions(m_AnimRuntimeOptions, initialize, runtimeOptionsValue);
            SetOptions(m_AnimShadowAngleOptions, initialize, bakedShadowAngle);
            SetOptions(m_AnimShadowRadiusOptions, initialize, bakedShadowRadius);
            SetOptions(m_AnimShadowResolutionOptions, initialize, shadowResolutionOptionsValue);
            SetOptions(m_AnimLightBounceIntensity, initialize, showLightBounceIntensity);
        }

        void DrawSpotAngle()
        {
            settings.DrawInnerAndOuterSpotAngle();
        }

        void DrawAdditionalShadowData()
        {
            bool hasChanged = false;
            int selectedUseAdditionalData; // 0: Custom bias - 1: Bias values defined in Pipeline settings

            if (m_AdditionalLightDataSO == null)
            {
                selectedUseAdditionalData = 1;
            }
            else
            {
                m_AdditionalLightDataSO.Update();
                selectedUseAdditionalData = !m_AdditionalLightData.usePipelineSettings ? 0 : 1;
            }

            // Bias
            Rect controlRectAdditionalData = EditorGUILayout.GetControlRect(true);
            if (m_AdditionalLightDataSO != null)
                EditorGUI.BeginProperty(controlRectAdditionalData, Styles.shadowBias, m_UseAdditionalDataProp);
            EditorGUI.BeginChangeCheck();

            selectedUseAdditionalData = EditorGUI.IntPopup(controlRectAdditionalData, Styles.shadowBias, selectedUseAdditionalData, Styles.displayedDefaultOptions, Styles.optionDefaultValues);
            if (EditorGUI.EndChangeCheck())
            {
                hasChanged = true;
            }
            if (m_AdditionalLightDataSO != null)
                EditorGUI.EndProperty();

            // Check mixed values
            if (!m_UseAdditionalDataProp.hasMultipleDifferentValues)
            {
                if (selectedUseAdditionalData != 1 && m_AdditionalLightDataSO != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Slider(settings.shadowsBias, 0f, 10f, "Depth");
                    EditorGUILayout.Slider(settings.shadowsNormalBias, 0f, 10f, Styles.ShadowNormalBias);
                    EditorGUI.indentLevel--;

                    m_AdditionalLightDataSO.ApplyModifiedProperties();
                }
            }

            if (hasChanged)
            {
                if (m_AdditionalLightDataSO == null)
                {
                    MultipleAdditionalLightDataCheck();
                    SetupSettings();
                }
                else
                {
                    foreach (var lightTarget in targets)
                    {
                        var additionData = (lightTarget as Component).gameObject.GetComponent<UniversalAdditionalLightData>();
                        if (additionData == null)
                            break;

                        additionData.usePipelineSettings = selectedUseAdditionalData != 0;
                    }
                }
                m_AdditionalLightDataSO.ApplyModifiedProperties();
            }
        }

        void MultipleAdditionalLightDataCheck()
        {
            var additionalLightList = new List<Object>();
            foreach (var lightTarget in targets)
            {
                var additionData = (lightTarget as Component).gameObject.GetComponent<UniversalAdditionalLightData>();
                if (additionData == null)
                    additionData = (lightTarget as Component).gameObject.AddComponent<UniversalAdditionalLightData>();

                additionalLightList.Add(additionData);
            }
            m_AdditionalLightData = (target as Component).gameObject.GetComponent<UniversalAdditionalLightData>();

            init(additionalLightList);
        }

        void SetupSettings()
        {
            var asset = UniversalRenderPipeline.asset;
            settings.shadowsBias.floatValue = asset.shadowDepthBias;
            settings.shadowsNormalBias.floatValue = asset.shadowNormalBias;
            settings.shadowsResolution.intValue = UniversalAdditionalLightData.AdditionalLightsShadowDefaultCustomResolution;
        }

        void DrawShadowsResolutionGUI()
        {
            int shadowResolutionTier;

            if (m_AdditionalLightDataSO == null)
            {
                shadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowDefaultResolutionTier;
            }
            else
            {
                m_AdditionalLightDataSO.Update();
                shadowResolutionTier = m_AdditionalLightData.additionalLightsShadowResolutionTier;
            }

            Rect controlRectAdditionalData = EditorGUILayout.GetControlRect(true);

            if (m_AdditionalLightDataSO != null)
                EditorGUI.BeginProperty(controlRectAdditionalData, Styles.ShadowResolution, m_AdditionalLightsShadowResolutionTierProp);

            EditorGUI.BeginChangeCheck();

            // UI code adapted from HDRP LevelFieldGUI in com.unity.render-pipelines.high-definition/Editor/RenderPipeline/Settings/SerializedScalableSettingValue.cs
            const int k_IndentPerLevel = 15;
            const int k_PrefixPaddingRight = 2;
            const int k_ValueUnitSeparator = 2;
            const int k_EnumWidth = 70;
            float indent = k_IndentPerLevel * EditorGUI.indentLevel;

            Rect labelRect = controlRectAdditionalData;
            Rect levelRect = controlRectAdditionalData;
            Rect fieldRect = controlRectAdditionalData;

            labelRect.width = EditorGUIUtility.labelWidth;
            // Dealing with indentation add space before the actual drawing
            // Thus resize accordingly to have a coherent aspect
            levelRect.x += labelRect.width - indent + k_PrefixPaddingRight;
            levelRect.width = k_EnumWidth + indent;
            fieldRect.x = levelRect.x + levelRect.width + k_ValueUnitSeparator - indent;
            fieldRect.width -= fieldRect.x - controlRectAdditionalData.x;

            EditorGUI.LabelField(labelRect, Styles.ShadowResolution);

            shadowResolutionTier = EditorGUI.IntPopup(levelRect, GUIContent.none, shadowResolutionTier, Styles.ShadowResolutionDefaultOptions, Styles.ShadowResolutionDefaultValues);

            bool hasResolutionTierChanged = EditorGUI.EndChangeCheck();

            if (m_AdditionalLightDataSO != null)
                EditorGUI.EndProperty();

            // Same logic as in DrawAdditionalShadowData
            if (shadowResolutionTier == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom && m_AdditionalLightDataSO != null)
            {
                // show the custom value field GUI.
                var newResolution = EditorGUI.IntField(fieldRect, settings.shadowsResolution.intValue);
                settings.shadowsResolution.intValue = Mathf.Max(128, Mathf.NextPowerOfTwo(newResolution));

                m_AdditionalLightDataSO.ApplyModifiedProperties();
            }
            if (shadowResolutionTier != UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom)
            {
                // show resolution tier values defined in pipeline settings
                UniversalRenderPipelineAsset urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
                EditorGUI.LabelField(fieldRect, $"{urpAsset.GetAdditionalLightsShadowResolution(shadowResolutionTier)} ({urpAsset.name})");
            }

            if (hasResolutionTierChanged)
            {
                if (m_AdditionalLightDataSO == null)
                {
                    MultipleAdditionalLightDataCheck();
                    SetupSettings();
                }

                m_AdditionalLightsShadowResolutionTierProp.intValue = shadowResolutionTier;
                m_AdditionalLightDataSO.ApplyModifiedProperties();
            }
        }

        void ShadowsGUI()
        {
            // Shadows drop-down. Area lights can only be baked and always have shadows.
            float show = 1.0f - m_AnimAreaOptions.faded;

            settings.DrawShadowsType();

            EditorGUI.indentLevel += 1;
            show *= m_AnimShadowOptions.faded;
            // Baked Shadow radius
            using (var group = new EditorGUILayout.FadeGroupScope(show * m_AnimShadowRadiusOptions.faded))
                if (group.visible)
                    settings.DrawBakedShadowRadius();

            // Baked Shadow angle
            using (var group = new EditorGUILayout.FadeGroupScope(show * m_AnimShadowAngleOptions.faded))
                if (group.visible)
                    settings.DrawBakedShadowAngle();

            // Runtime shadows - shadow strength, resolution and near plane offset
            // Bias is handled differently in UniversalRP
            using (var group = new EditorGUILayout.FadeGroupScope(show * m_AnimRuntimeOptions.faded))
            {
                if (group.visible)
                {
                    EditorGUILayout.LabelField(Styles.ShadowRealtimeSettings);
                    EditorGUI.indentLevel += 1;

                    // Resolution
                    using (var resolutionGroup = new EditorGUILayout.FadeGroupScope(show * m_AnimShadowResolutionOptions.faded))
                        if (resolutionGroup.visible)
                            DrawShadowsResolutionGUI();

                    EditorGUILayout.Slider(settings.shadowsStrength, 0f, 1f, Styles.ShadowStrength);

                    // Bias
                    DrawAdditionalShadowData();

                    // this min bound should match the calculation in SharedLightData::GetNearPlaneMinBound()
                    float nearPlaneMinBound = Mathf.Min(0.01f * settings.range.floatValue, 0.1f);
                    EditorGUILayout.Slider(settings.shadowsNearPlane, nearPlaneMinBound, 10.0f, Styles.ShadowNearPlane);
                    EditorGUI.indentLevel -= 1;
                }
            }

            EditorGUI.indentLevel -= 1;

            if (bakingWarningValue)
                EditorGUILayout.HelpBox(s_Styles.BakingWarning.text, MessageType.Warning);

            EditorGUILayout.Space();
        }

        void LightCookieGUI()
        {
            settings.DrawCookie();

            // Draw 2D cookie size for directional lights
            bool isDirectionalLight = settings.light.type == LightType.Directional;
            if (isDirectionalLight)
            {
                if (settings.cookie != null)
                {
                    if (m_AdditionalLightData == null)
                    {
                        CreateAdditionalLightData();
                    }

                    bool isChanged = false;
                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(m_LightCookieSizeProp, Styles.LightCookieSize, (GUILayoutOption[])System.Array.Empty<GUILayoutOption>());
                    }
                    isChanged |= EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(m_LightCookieOffsetProp, Styles.LightCookieOffset, (GUILayoutOption[])System.Array.Empty<GUILayoutOption>());
                    }
                    isChanged |= EditorGUI.EndChangeCheck();
                    if (isChanged)
                    {
                        m_AdditionalLightDataSO.ApplyModifiedProperties();
                    }
                }
            }

            EditorGUILayout.Space();
        }

        protected override void OnSceneGUI()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return;

            Light light = target as Light;

            switch (light.type)
            {
                case LightType.Spot:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawSpotLightGizmo(light);
                    }
                    break;

                case LightType.Point:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, Quaternion.identity, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawPointLightGizmo(light);
                    }
                    break;

                case LightType.Rectangle:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawRectangleLightGizmo(light);
                    }
                    break;

                case LightType.Disc:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDiscLightGizmo(light);
                    }
                    break;

                case LightType.Directional:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        CoreLightEditorUtilities.DrawDirectionalLightGizmo(light);
                    }
                    break;

                default:
                    base.OnSceneGUI();
                    break;
            }
        }
    }
}
