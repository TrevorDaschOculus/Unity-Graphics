using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class OculusMotionVectorWindow : EditorWindow
{
    [MenuItem ("Oculus/Debug Motion Vectors")]
    public static void  ShowWindow ()
    {
        EditorWindow.GetWindow(typeof(OculusMotionVectorWindow));
    }

    void OnGUI ()
    {
        UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.enableDebugMotionVectors =
            EditorGUILayout.Toggle("Debug Motion Vectors",
                UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.enableDebugMotionVectors);

        if (!UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.enableDebugMotionVectors)
        {
            if (UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget != null) {
                GameObject.DestroyImmediate(UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget);
                UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget = null;
                UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.renderTargetDesc = default;
            }

            return;
        }

        if (UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget == null)
        {
            UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.renderTargetDesc = new RenderTextureDescriptor(512, 512, RenderTextureFormat.RGB111110Float, 24);
            UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget = new RenderTexture(UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.renderTargetDesc);
        }

        GUI.DrawTexture(EditorGUILayout.GetControlRect(false, GUILayout.Height(512), GUILayout.Width(512)), UnityEngine.Rendering.Universal.DebugOculusMotionVectorSettings.motionVectorRenderTarget);
    }
}
