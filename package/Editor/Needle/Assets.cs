using UnityEditor;
using UnityEngine;

namespace Needle.Editors
{
	internal static class Assets
	{
		private const string _fullLogoGuid = "a812020de410d68468da1e6638a3c827";
		private static Texture2D _fullLogo;
		public static Texture2D FullLogo
		{
			get
			{
				if (_fullLogo) return _fullLogo;
				var path = AssetDatabase.GUIDToAssetPath(_fullLogoGuid);
				if (path != null)
				{
					return _fullLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				}

				_fullLogo = Texture2D.blackTexture;
				return _fullLogo;
			}
		}
		
		private const string _fullLogoDarkModeGuid = "0c1bf168a578f9a41bd7d1297d323633";
		private static Texture2D _fullLogoDarkMode;
		public static Texture2D FullLogoDarkMode
		{
			get
			{
				if (_fullLogoDarkMode) return _fullLogoDarkMode;
				var path = AssetDatabase.GUIDToAssetPath(_fullLogoDarkModeGuid);
				if (path != null)
				{
					return _fullLogoDarkMode = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				}

				_fullLogoDarkMode = Texture2D.blackTexture;
				return _fullLogoDarkMode;
			}
		}
		
		
		
		private const string _iconGuid = "6eacf41911247604289974de9a598c0b";
		private static Texture2D _logo;
		public static Texture2D Logo
		{
			get
			{
				if (_logo) return _logo;
				var path = AssetDatabase.GUIDToAssetPath(_iconGuid);
				if (path != null)
				{
					return _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				}

				_logo = Texture2D.blackTexture;
				return _logo;
			}
		}

		private const string _iconButtonGuid = "25371de7ee20b134897499abf153e9f9";
		private static Texture2D _logo_button;

		public static Texture2D LogoButton
		{
			get
			{
				if (_logo) return _logo;
				var path = AssetDatabase.GUIDToAssetPath(_iconButtonGuid);
				if (path != null)
				{
					return _logo_button = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
				}

				_logo_button = Texture2D.blackTexture;
				return _logo_button;
			}
		}
		
		public static void DrawGUIFullLogo(float maxHeight = 32)
		{
			var logo = UnityEditor.EditorGUIUtility.isProSkin ? FullLogoDarkMode : FullLogo;
			if (logo)
			{
//				EditorGUILayout.BeginHorizontal();
//				GUILayout.FlexibleSpace();
				var rect = GUILayoutUtility.GetRect(maxHeight*2f + 10, maxHeight);
				rect.width = maxHeight * 2f;
				rect.height = maxHeight;
				if (Event.current.type == EventType.Repaint)
					GUI.DrawTexture(rect, logo, ScaleMode.ScaleToFit);
				GUI.Label(rect, new GUIContent(string.Empty, "Compilation Visualizer by\nneedle.tools"), GUIStyle.none);

				UnityEditor.EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
				if (Event.current.type == EventType.MouseUp && rect.Contains(Event.current.mousePosition))
					Application.OpenURL("https://needle.tools");
			//	EditorGUILayout.EndHorizontal();
			}
		}
		

		public static void DrawGUILogo()
		{
			float maxHeight = 15;
			var logo = Assets.Logo;
			if (logo)
			{
				//EditorGUILayout.BeginHorizontal();
//				GUILayout.FlexibleSpace();
				var rect = GUILayoutUtility.GetRect(maxHeight+5, maxHeight);
				rect.height = maxHeight;
				rect.y += 2f;
				rect.width = maxHeight;
				if (Event.current.type == EventType.Repaint)
					GUI.DrawTexture(rect, logo, ScaleMode.ScaleToFit);
				GUI.Label(rect, new GUIContent(string.Empty, "Compilation Visualizer by\nneedle.tools"), GUIStyle.none);

				UnityEditor.EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
				if (Event.current.type == EventType.MouseUp && rect.Contains(Event.current.mousePosition))
					Application.OpenURL("https://needle.tools");
				//EditorGUILayout.EndHorizontal();
			}
		}
	}
}