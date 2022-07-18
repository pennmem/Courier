#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static KrisDevelopment.ERMG.EasyRoadsMeshGen_Array;

namespace KrisDevelopment.ERMG
{
	[CustomEditor(typeof(EasyRoadsMeshGen_Array))]
	public class ERMG_Array_Editor : Editor
	{
		//static bool expandArray = true;

		EasyRoadsMeshGen_Array script;

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();

			script = (EasyRoadsMeshGen_Array)target;
			SerializedObject _so_ermgArr = new SerializedObject(script);
			SerializedProperty
				_so_ermgArr_arrayObject = _so_ermgArr.FindProperty(nameof(EasyRoadsMeshGen_Array.arrayObjects)),
				_so_ermgArr_length = _so_ermgArr.FindProperty(nameof(EasyRoadsMeshGen_Array.length)),
				_so_ermgArr_combineMeshes = _so_ermgArr.FindProperty(nameof(EasyRoadsMeshGen_Array.combineMeshes)),
				_so_ermgArr_ignoreParentSize = _so_ermgArr.FindProperty(nameof(EasyRoadsMeshGen_Array.ignoreParentSize)),
				_so_ermgArr_suspend = _so_ermgArr.FindProperty(nameof(EasyRoadsMeshGen_Array.suspend));

			// Since for now there won't be multiple objects per array component,
			// set the array to arraySize 1
			_so_ermgArr_arrayObject.arraySize = 1;
			_so_ermgArr.ApplyModifiedPropertiesWithoutUndo();
			_so_ermgArr.Update();


			EditorGUILayout.PropertyField(_so_ermgArr_suspend);

			EditorGUI.BeginDisabledGroup(_so_ermgArr_suspend.boolValue);
			{
				EditorGUILayout.PropertyField(_so_ermgArr_length, new GUIContent("Fit Length", "[EasyRoadsMeshGen_Array.length]\nIf an array object uses FitType.FitLength, this value will serve as the reference length."));
				EditorGUILayout.PropertyField(_so_ermgArr_combineMeshes);
				EditorGUILayout.PropertyField(_so_ermgArr_ignoreParentSize);

				for (int i = 0; i < script.arrayObjects.Count; i++)
				{
					SerializedProperty
						_ermgArrEl = _so_ermgArr_arrayObject.GetArrayElementAtIndex(i),
						_prefab = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.prefab)),
						_fitType = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.fitType)),
						_amount = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.amount)),
						_elementLength = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.length)),
						_verticalOffset = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.verticalOffset)),
						_horizontalOffset = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.horizontalOffset)),
						_pathOffset = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.pathOffset)),
						_rotation = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.rotation)),
						_invert = _ermgArrEl.FindPropertyRelative(nameof(ArrayObject.invert));

					GUILayout.BeginVertical(EditorStyles.helpBox);

					EditorGUILayout.PropertyField(_prefab);
					GUILayout.BeginHorizontal();
					{
						EditorGUILayout.PropertyField(_elementLength, new GUIContent("Object Length"));
						if (GUILayout.Button(new GUIContent("Auto", "Automatically determine the object length."), EditorStyles.miniButton))
						{
							_elementLength.floatValue = script.GetAutoLength(i);
						}
					}
					GUILayout.EndHorizontal();
					EditorGUILayout.PropertyField(_fitType);
					if (script.arrayObjects[i].fitType == EasyRoadsMeshGen_Array.FitType.FixedAmount)
					{
						EditorGUILayout.PropertyField(_amount);
					}
					EditorGUILayout.PropertyField(_verticalOffset);
					EditorGUILayout.PropertyField(_horizontalOffset);
					EditorGUILayout.PropertyField(_pathOffset);
					EditorGUILayout.PropertyField(_rotation);
					EditorGUILayout.PropertyField(_invert);

					GUILayout.EndVertical();
				}
			}
			EditorGUI.EndDisabledGroup();

			if (EditorGUI.EndChangeCheck())
			{
				_so_ermgArr.ApplyModifiedProperties();
				script.RequestUpdate();
			}
		}
	}
}
#endif
