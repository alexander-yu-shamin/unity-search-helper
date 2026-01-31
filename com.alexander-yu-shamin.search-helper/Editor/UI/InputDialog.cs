using System;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.UI
{
    public class InputDialog : EditorWindow
    {
        private static string _inputText = "";
        private static Action<string> _onConfirm;

        public static void Show(string title, string initialValue, Action<string> callback)
        {
            _inputText = initialValue;
            _onConfirm = callback;

            var window = CreateInstance<InputDialog>();
            window.titleContent = new GUIContent(title);
            var size = new Vector2(400, 60);
            window.minSize = size;
            window.maxSize = size;
            window.position = new Rect(Screen.width / 2, Screen.height / 2, size.x, size.y);

            window.ShowModalUtility();
        }

        void OnGUI()
        {
            GUILayout.Label("Input:", EditorStyles.boldLabel);
            _inputText = EditorGUILayout.TextField(_inputText);

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("OK", GUILayout.Width(100)))
            {
                _onConfirm?.Invoke(_inputText);
                Close();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}