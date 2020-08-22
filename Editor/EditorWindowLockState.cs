using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Needle.EditorGUIUtility
{
    // based on the internal class EditorGUIUtility.EditorLockTracker
    // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorGUIUtility.cs#L340
    [Serializable]
    internal class EditorWindowLockState
    {
        private static readonly GUIContent LockMenuGUIContent = new GUIContent("Lock compilation results", "Lock compilation results");
        internal LockStateEvent lockStateChanged = new LockStateEvent();
        [HideInInspector, SerializeField]
        private bool isLocked;
        
        internal virtual bool IsLocked {
            get => isLocked;
            set {
                var wasLocked = isLocked;
                isLocked = value;
                if (wasLocked == isLocked)
                    return;
                lockStateChanged.Invoke(isLocked);
            }
        }

        internal virtual void AddItemsToMenu(GenericMenu menu, bool disabled = false) {
            if (disabled)
                menu.AddDisabledItem(LockMenuGUIContent);
            else
                menu.AddItem(LockMenuGUIContent, IsLocked, FlipLocked);
        }

        internal void ShowButton(Rect position, GUIStyle lockButtonStyle, bool disabled = false) {
            using (new EditorGUI.DisabledScope(disabled)) {
                EditorGUI.BeginChangeCheck();
                var newState = GUI.Toggle(position, IsLocked, GUIContent.none, lockButtonStyle);
                if (!EditorGUI.EndChangeCheck() || newState == IsLocked)
                    return;
                FlipLocked();
            }
        }

        private void FlipLocked() => IsLocked = !IsLocked;

        [Serializable]
        public class LockStateEvent : UnityEvent<bool> { }
    }
}