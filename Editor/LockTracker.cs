using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Needle.EditorGUIUtility
{
  [Serializable]
  internal class EditorLockTracker
  {
    [HideInInspector]
    internal EditorGUIUtility.EditorLockTracker.LockStateEvent lockStateChanged =
      new EditorGUIUtility.EditorLockTracker.LockStateEvent();

    private const string k_LockMenuText = "Lock";
    private static readonly GUIContent k_LockMenuGUIContent = new GUIContent("Lock", "Lock");
    [HideInInspector] [SerializeField] private bool m_IsLocked;

    internal virtual bool isLocked
    {
      get => this.m_IsLocked;
      set
      {
        bool isLocked = this.m_IsLocked;
        this.m_IsLocked = value;
        if (isLocked == this.m_IsLocked)
          return;
        this.lockStateChanged.Invoke(this.m_IsLocked);
      }
    }

    internal virtual void AddItemsToMenu(GenericMenu menu, bool disabled = false)
    {
      if (disabled)
        menu.AddDisabledItem(EditorGUIUtility.EditorLockTracker.k_LockMenuGUIContent);
      else
        menu.AddItem(EditorGUIUtility.EditorLockTracker.k_LockMenuGUIContent, this.isLocked,
          new GenericMenu.MenuFunction(this.FlipLocked));
    }

    internal void ShowButton(Rect position, GUIStyle lockButtonStyle, bool disabled = false)
    {
      using (new EditorGUI.DisabledScope(disabled))
      {
        EditorGUI.BeginChangeCheck();
        bool flag = GUI.Toggle(position, this.isLocked, GUIContent.none, lockButtonStyle);
        if (!EditorGUI.EndChangeCheck() || flag == this.isLocked)
          return;
        this.FlipLocked();
      }
    }

    private void FlipLocked() => this.isLocked = !this.isLocked;

    [Serializable]
    public class LockStateEvent : UnityEvent<bool>
    {
    }
  }
}