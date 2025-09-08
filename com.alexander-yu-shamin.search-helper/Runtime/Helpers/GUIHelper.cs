using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Search.Helper.Runtime.Helpers
{
    public static class GUIHelper
    {
        public static void Color(Color color, Action action)
        {
            InnerChangeValue(() => GUI.color, value => GUI.color = value, color, action);
        }

        public static void Enabled(bool enable, Action action)
        {
            InnerChangeValue(() => GUI.enabled, value => GUI.enabled = value, enable, action);
        }

        public static void InnerChangeValue<T>(Func<T> getter, Action<T> setter, T newValue, Action innerAction)
        {
            if (getter == null || setter == null || innerAction == null)
            {
                return;
            }

            var oldValue = getter();
            setter.Invoke(newValue);
            innerAction.Invoke();
            setter.Invoke(oldValue);
        }

        public static void Horizontal(Action action)
        {
            Horizontal(GUIStyle.none, action);
        }

        public static void Horizontal(GUIStyle style, Action action)
        {
            GUILayout.BeginHorizontal(style);
            action?.Invoke();
            GUILayout.EndHorizontal();
        }

        public static void Vertical(Action action)
        {
            Vertical(GUIStyle.none, action);
        }

        public static void Vertical(GUIStyle style, Action action)
        {
            GUILayout.BeginVertical(style);
            action?.Invoke();
            GUILayout.EndVertical();
        }

        public static Vector2 ScrollView(Vector2 scrollPosition, Action action)
        {
            var position = GUILayout.BeginScrollView(scrollPosition);
            action?.Invoke();
            GUILayout.EndScrollView();
            return position;
        }

        public static void Button(string text, Action action, GUIStyle style, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(text, style, options))
            {
                action?.Invoke();
            }
        }

        public static void Button(string text, Action action, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(text, options))
            {
                action?.Invoke();
            }
        }

        public static void Button(string text, Action action)
        {
            if (GUILayout.Button(text))
            {
                action?.Invoke();
            }
        }

        public static void Toggle(string text, bool value, Action<bool> action)
        {
            var result = GUILayout.Toggle(value, text);
            action?.Invoke(result);
        }
    }
}
