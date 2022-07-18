#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace KrisDevelopment.ERMG {
	[CustomEditor(typeof(ERPointSnap))]
	public class ERPointSnapEditor : Editor
	{
		static string[] snapModeStr = {"None", "X,Y,Z", "Only X", "Only Y", "Only Z", "X,Y", "X,Z", "Y,Z"};
		ERPointSnap snpScript;
		SerializedObject snp;
		SerializedProperty snp_snapModeInt;
		
		void OnEnable () {
			snpScript = (ERPointSnap)target;
			snp = new SerializedObject(snpScript);
			snp_snapModeInt = snp.FindProperty(nameof(ERPointSnap.snapModeInt));
		}
		
		public override void OnInspectorGUI(){
			DrawDefaultInspector();
			
			if(!snpScript)
				return;
			
			if(snpScript.passive)
			return;
		
			if(!snpScript.snapped){
				snp_snapModeInt.intValue = EditorGUILayout.Popup("Inverse", snpScript.snapModeInt, snapModeStr);
				
				if(GUILayout.Button("Snap To Closest Point")){
					snpScript.SnapToPointTransform();
				}
				EditorGUILayout.HelpBox("Use 'Inverse' to select the right orientation of the point when snapped to another.", MessageType.None);
			}else{
				if(GUILayout.Button("Unsnap To Previous")){
					snpScript.UnSnap();
				}
			}
			
			snp.ApplyModifiedProperties();
		}
	}
}
#endif
