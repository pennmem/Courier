using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SETUtil.ResourceLoader;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KrisDevelopment.DistributedInternalUtilities
{
	public static class BugReporting
	{
		public const string BUG_REPORT_URL = "https://krisdevelopment.wordpress.com/bug-report/";


		public static void ToolbarBugReportButton()
		{
			GUIContent _bugContent;
#if UNITY_EDITOR
			_bugContent = new GUIContent(EditorTextureResource.Get("kd_bug_report_icon"));
#else
			_bugContent = new GUIContent("[!]");
#endif
			_bugContent.tooltip = "Report a bug!";

			GUIStyle _style = new GUIStyle("Button");
			
#if UNITY_EDITOR
			_style = EditorStyles.toolbarButton;
#else
			SETUtil.EditorUtil.BeginColorPocket(new Color(1, 0.5f, 0.5f, 1));
#endif
			if (GUILayout.Button(_bugContent, _style, GUILayout.ExpandWidth(false), GUILayout.Width(28)))
			{
				Application.OpenURL(BUG_REPORT_URL);
			}

#if !UNITY_EDITOR
			SETUtil.EditorUtil.EndColorPocket();
#endif
		}

		public static void SmallBugReportButton()
		{
			GUIContent _bugContent;
#if UNITY_EDITOR
			_bugContent = new GUIContent(EditorTextureResource.Get("kd_bug_report_icon"));
			_bugContent.text = " Report a bug!";
#else
			_bugContent = new GUIContent("Report a bug!");
#endif
			GUIStyle _style = new GUIStyle("Button");

#if UNITY_EDITOR
			_style = EditorStyles.miniButton;
#endif

			if (GUILayout.Button(_bugContent, _style, GUILayout.ExpandWidth(false)))
			{
				Application.OpenURL(BUG_REPORT_URL);
			}
		}

		public static void StandardBugReportButton()
		{
			GUIContent _bugContent;
#if UNITY_EDITOR
			_bugContent = new GUIContent(EditorTextureResource.Get("kd_bug_report_icon"));
			_bugContent.text = " Report a bug!";
#else
			_bugContent = new GUIContent("Report a bug!");
#endif

			if (GUILayout.Button(_bugContent, GUILayout.ExpandWidth(false)))
			{
				Application.OpenURL(BUG_REPORT_URL);
			}
		}
	}
}
