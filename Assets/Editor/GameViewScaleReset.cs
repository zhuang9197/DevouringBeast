#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DevouringBeast.EditorTools
{
    /// <summary>
    /// The Game View remembers its editor zoom independently of the game camera.
    /// Reset it on play transitions so a saved 1.5x preview cannot crop the view.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewScaleReset
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static GameViewScaleReset()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ResetAllGameViews();
        }

        [MenuItem("DevouringBeast/Reset Game View Scale")]
        private static void ResetFromMenu() => ResetAllGameViews();

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += ResetAllGameViews;
        }

        private static void ResetAllGameViews()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window == null || window.GetType() != gameViewType) continue;
                object zoomArea = gameViewType.GetField("m_ZoomArea", Flags)?.GetValue(window);
                if (zoomArea == null) continue;
                Type zoomType = zoomArea.GetType();
                // The public-looking property reports CanWrite in some 2022.3 revisions but
                // has no callable setter. The serialized backing field is stable in this editor.
                zoomType.GetField("m_Scale", Flags)?.SetValue(zoomArea, Vector2.one);
                window.Repaint();
            }
        }
    }
}
#endif
