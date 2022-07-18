////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System;
using U = UnityEngine;

#if UNITY_EDITOR
using E = UnityEditor;
#endif

//SETUtil.Common contains class names that might overlap with system or unity namespaces
namespace SETUtil.Common.Attributes
{
	/// <summary>
	/// Property drawer attribute for enum masks
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class DrawEnumFlagsAttribute : U.PropertyAttribute { }

#if UNITY_EDITOR
	/// <summary>
	/// Draws the enum mask field in the inspector
	/// </summary>
	[E.CustomPropertyDrawer(typeof(DrawEnumFlagsAttribute))]
	internal class EnumFlagsPropertyDrawer : E.PropertyDrawer
	{
		public override void OnGUI(U.Rect position, E.SerializedProperty property, U.GUIContent label)
		{
			E.EditorGUI.BeginProperty(position, label, property);
			var _enumNames = property.enumNames;
			var _value = property.intValue;
			property.intValue = E.EditorGUI.MaskField(position, label, _value, _enumNames);
			E.EditorGUI.EndProperty();
		}
	}
#endif
}