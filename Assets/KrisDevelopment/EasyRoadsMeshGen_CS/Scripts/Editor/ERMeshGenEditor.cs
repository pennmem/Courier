#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SETUtil;
using SETUtil.Types;
using KrisDevelopment.DistributedInternalUtilities;

namespace KrisDevelopment.ERMG
{
	[CustomEditor(typeof(ERMeshGen))]
	public class ERMeshGenEditor : Editor
	{
		private string[] constantVertsUpdateStr = { "Automatic (Recommended)", "Manual Update", "Update Vertices Only", "Realtime" };
		private string[] includeColliderStr = { "Off", "On" };
		private string[] uvSetStr = { "Per Segment", "Top Projection", "Match Width", "Stretch Single Texture" };
		private string[] enableMeshBordersStr = { "Off", "On" };
		private string[] borderUvSetStr = { "Straight Unwrap", "Top Projection" };

		ERMeshGen meshGen;

		SerializedObject so_meshGen;
		SerializedProperty
			meshGen_deltaWidth,
			meshGen_subdivision,
			meshGen_uvScale,
			meshGen_borderUvScale,
			meshGen_groundOffset,
			meshGen_pointControl,
			meshGen_vertsUpdate,
			meshGen_runtimeBehavior,
			meshGen_includeCollider,
			meshGen_uvSet,
			meshGen_enableMeshBorders,
			meshGen_legacyWidthMode,
			meshGen_borderCurve,
			meshGen_borderScale,
			meshGen_borderOffset,
			meshGen_borderUvSet,
			meshGen_smoothingRange;


		public void OnEnable()
		{
			Init();
		}

		public void Init()
		{
			meshGen = (ERMeshGen)target;
			so_meshGen = new SerializedObject(meshGen);
			meshGen_deltaWidth = so_meshGen.FindProperty(nameof(ERMeshGen.deltaWidth));
			meshGen_subdivision = so_meshGen.FindProperty(nameof(ERMeshGen.subdivision));
			meshGen_uvScale = so_meshGen.FindProperty(nameof(ERMeshGen.uvScale));
			meshGen_borderUvScale = so_meshGen.FindProperty(nameof(ERMeshGen.borderUvScale));
			meshGen_groundOffset = so_meshGen.FindProperty(nameof(ERMeshGen.groundOffset));
			meshGen_pointControl = so_meshGen.FindProperty(nameof(ERMeshGen.pointControl));
			meshGen_vertsUpdate = so_meshGen.FindProperty(nameof(ERMeshGen.updatePointsMode));
			meshGen_runtimeBehavior = so_meshGen.FindProperty(nameof(ERMeshGen.runtimeBehavior));
			meshGen_includeCollider = so_meshGen.FindProperty(nameof(ERMeshGen.includeCollider));
			meshGen_uvSet = so_meshGen.FindProperty(nameof(ERMeshGen.uvSet));
			meshGen_enableMeshBorders = so_meshGen.FindProperty(nameof(ERMeshGen.enableMeshBorders));
			meshGen_legacyWidthMode = so_meshGen.FindProperty(nameof(ERMeshGen.legacyWidthMode));
			meshGen_borderCurve = so_meshGen.FindProperty(nameof(ERMeshGen.borderCurve));
			meshGen_borderScale = so_meshGen.FindProperty(nameof(ERMeshGen.borderScale));
			meshGen_borderOffset = so_meshGen.FindProperty(nameof(ERMeshGen.borderOffset));
			meshGen_borderUvSet = so_meshGen.FindProperty(nameof(ERMeshGen.borderUvSet));
			meshGen_smoothingRange = so_meshGen.FindProperty(nameof(ERMeshGen.terrainBrushSmoothingRange));
		}


		public override void OnInspectorGUI()
		{
			using (new ColorPocket())
			{
				if (meshGen.GetComponent<MeshFilter>())
				{
					EditorUtil.BeginColorPocket(new Color(0.3f, 1, 0.1f));
					GUILayout.BeginHorizontal();
					{
						if (GUILayout.Button("Add Nav Point"))
						{
							meshGen.CreateNavPoint();
						}
						GUI.color = new Color(1, 1, 0.1f);
						if (meshGen.navPoints.Count > 1)
						{
							if (GUILayout.Button("Delete Nav Point"))
							{
								meshGen.DeleteNavPoint();
							}
						}
					}
					GUILayout.EndHorizontal();
					GUI.color = new Color(0.3f, 1, 0.1f);

					if (meshGen.updatePointsMode > 0 && meshGen.updatePointsMode != 3)
					{
						if (GUILayout.Button("Apply Changes"))
						{
							meshGen.UpdateMesh();
						}
					}

					GUI.color = new Color(0.8f, 0.8f, 1);

					GUILayout.Space(8);

					meshGen_deltaWidth.floatValue = EditorGUILayout.FloatField("Delta Width", meshGen.deltaWidth);

					EditorGUILayout.PropertyField(meshGen_legacyWidthMode);

					meshGen_subdivision.intValue = EditorGUILayout.IntField(new GUIContent("Subdivision *", "The min value for this is 1," +
						" max value is determined dynamically with respect to the max mesh vetex count supported by Unity." +
						" This if the number is too high to support the mesh vertex count, it will get clamped down to the maximum valid value."), meshGen.subdivision);
					
					meshGen_uvScale.floatValue = EditorGUILayout.FloatField("UV Scale", meshGen.uvScale);

					EditorUtil.BeginColorPocket(new Color(meshGen.updatePointsMode / 2f, 1, 0.3f));
					meshGen_vertsUpdate.intValue = EditorGUILayout.Popup("Update Mode", meshGen.updatePointsMode, constantVertsUpdateStr);
					EditorUtil.EndColorPocket();

					EditorGUILayout.PropertyField(meshGen_runtimeBehavior);

					meshGen_pointControl.enumValueIndex = (int)(ERMG.PointControl)EditorGUILayout.EnumPopup("Point Control", meshGen.pointControl);
					meshGen_includeCollider.intValue = EditorGUILayout.Popup("Include Collider", meshGen.includeCollider, includeColliderStr);
					meshGen_uvSet.enumValueIndex = EditorGUILayout.Popup("UVs", (int)meshGen.uvSet, uvSetStr);

					GUILayout.BeginVertical(EditorStyles.helpBox);
					meshGen_enableMeshBorders.intValue = EditorGUILayout.Popup("Borders Mesh", meshGen.enableMeshBorders, enableMeshBordersStr);

					if (meshGen.enableMeshBorders == 1)
					{
						EditorGUI.indentLevel++;
						meshGen_borderCurve.animationCurveValue = EditorGUILayout.CurveField("Border Shape", meshGen.borderCurve);
						EditorGUILayout.PropertyField(meshGen_borderScale);
						EditorGUILayout.PropertyField(meshGen_borderOffset);
						meshGen_borderUvSet.enumValueIndex = EditorGUILayout.Popup("Border UVs", (int)meshGen.borderUvSet, borderUvSetStr);
						meshGen_borderUvScale.floatValue = EditorGUILayout.FloatField("UV Scale", meshGen.borderUvScale);
						EditorGUI.indentLevel--;
					}

					GUILayout.EndVertical();

					GUILayout.BeginVertical("Surface Interaction", new GUIStyle("Window"));
					{
						meshGen_groundOffset.floatValue = EditorGUILayout.Slider("Offset Value", meshGen.groundOffset, 0, 1);

						GUILayout.Label("Ground Points:", EditorStyles.boldLabel);

						EditorGUILayout.HelpBox("Snap nav points to the surface underneath.", MessageType.None);

						if (GUILayout.Button("Ground Points"))
						{
							meshGen.GroundPoints(meshGen.groundOffset);
						}

						GUILayout.Label("Morph Terrain:", EditorStyles.boldLabel);

						EditorGUILayout.PropertyField(meshGen_smoothingRange, new GUIContent("Range"));

						if (GUILayout.Button("Process Terrain"))
						{
							if (EditorUtility.DisplayDialog("Process Terrain Warning!",
								"WARNING: This operation is destructive, meaning your terrain data will be permanently modified. Do you still wish to continue?", "Yes", "No"))
							{
								meshGen.ProcessUnderlyingTerrains();
							}
						}
					}
					GUILayout.EndVertical();

					GUILayout.Space(8);

					GUILayout.BeginHorizontal();
					{
						EditorUtil.BeginColorPocket(new Color(1, 0.2f, 0.2f));

						if (GUILayout.Button("Reset"))
						{
							if (EditorUtility.DisplayDialog("Reset?", "Are you sure you want to clear the current mesh and delete all Nav Points?", "Yes", "No"))
							{
								meshGen.ResetMesh();
								EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
							}
						}

						EditorUtil.EndColorPocket();

						GUI.color = Color.white;

						if (GUILayout.Button("Export To OBJ File"))
						{
							if (EditorUtility.DisplayDialog("Warning", "This is an experimental featre. Results may be inaccurate or partially incomplete.", "Continue", "Cancel"))
							{
								ERMG.Util.ExportToOBJ(meshGen.gameObject);
							}
						}

					}
					GUILayout.EndHorizontal();

					SETUtil.EditorUtil.BeginColorPocket(Color.yellow);
					if (GUILayout.Button("Finalize"))
					{
						if (EditorUtility.DisplayDialog("Warning", "This will remove all components except MeshCollider, MeshFilter and MeshRenderer and will export all related mesh assets. " +
							"Path Tracer component will no longer have a Mesh Gen component to draw data from. Changes cannot be reverted.", "Continue", "Cancel"))
						{
							meshGen.EditorFinalize(ERMeshGen.DisposeImmediate);
							EditorGUIUtility.ExitGUI();
							return;
						}
					}
					SETUtil.EditorUtil.EndColorPocket();

					SETUtil.EditorUtil.HorizontalRule();

					BugReporting.SmallBugReportButton();

					EditorUtil.EndColorPocket();
				}

			}

			so_meshGen.ApplyModifiedProperties();
			if (so_meshGen != null)
			{
				so_meshGen.Update();
			}
		}
	}
}
#endif
