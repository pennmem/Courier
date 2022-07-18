using System;
using System.Collections;
using System.Collections.Generic;
using SETUtil;
using SETUtil.Common.Extend;
using SETUtil.Extend;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KrisDevelopment.ERMG
{
	public delegate void DisposeMethod(UnityEngine.Object obj);
	public delegate void AssetCreationMethod(UnityEngine.GameObject obj);


	public static class Util
	{
#if UNITY_EDITOR
		
			[UnityEditor.MenuItem("GameObject/3D Object/ER Mesh Gen")]
			public static ERMeshGen CreateERMeshGen ()
			{
				var newObject = new GameObject("ERMeshGen");
				var meshGen = newObject.AddComponent<ERMeshGen>();
				meshGen.CreateNavPoint();
				meshGen.CreateNavPoint();
				meshGen.GetComponent<MeshRenderer>().sharedMaterial = GetDefaultMaterial();
				meshGen.pointControl = PointControl.Automatic;
				return meshGen;
			}

#endif
		
		public static Material GetDefaultMaterial ()
		{
			var _sample = GameObject.CreatePrimitive(PrimitiveType.Quad);
			_sample.hideFlags = HideFlags.DontSave;
			var _material = _sample.GetComponent<Renderer>().sharedMaterial;
			SETUtil.SceneUtil.SmartDestroy(_sample);
			return _material;
		}

		public static OrientationData[] TransformToOrientationArray (Transform[] trArr)
		{
			OrientationData[] _p = new OrientationData[trArr.Length];
			for(int i = 0; i < _p.Length; i++){
				_p[i] = new OrientationData();
				if(trArr[i])
					_p[i].Set(trArr[i].position, trArr[i].forward, trArr[i].up);
			}
			return _p;
		}
		
		public static List<Transform> FindMatchingChildren (Transform parent, string baseName, System.Type tp = null)
		{
			List<Transform> _pts = new List<Transform>(0);
			foreach (Transform t in parent){
				if(t.name.Contains(baseName) || (tp != null && t.GetComponent(tp) != null)){ 
					_pts.Add(t);
				}
			}
			return _pts;
		}

		public static void ExportToOBJ (GameObject root)
		{
			var _dataPath = Application.dataPath;

#if UNITY_EDITOR
			UnityEditor.EditorUtility.DisplayProgressBar("Exporting OBJ", "...", 0);
			_dataPath = UnityEditor.EditorUtility.OpenFolderPanel("Export OBJ", _dataPath, "MeshGenExport");
#else
			_dataPath = SETUtil.FileUtil.CreatePathString("MeshGen", true);
#endif

			var objectsToExport = new List<GameObject>();
			objectsToExport.AddRange(SETUtil.SceneUtil.CollectAllChildren(root.transform, true).ToGameObjectArray());

			int _counter = 0;
			foreach (var objectToExport in objectsToExport) {
				if(objectToExport.GetComponent<MeshFilter>()) {

					var _exportPath = string.Format("{0}/MeshGenExport_{1}/{2}_{3}.obj", _dataPath, root.name, _counter++, objectToExport.name);
					_exportPath = _exportPath.Replace("(", "_").Replace(")", "_");

					SETUtil.MeshExporter.OBJExporter.ExportObject(_exportPath, objectToExport);
				}
			}

#if UNITY_EDITOR
			UnityEditor.EditorUtility.ClearProgressBar();
			UnityEditor.AssetDatabase.Refresh();
#endif
		}

		/// <summary>
		/// Mark for serialization within the editor
		/// </summary>
        public static void Dirtify(UnityEngine.Object obj)
        {
#if UNITY_EDITOR

			if (!obj || obj.hideFlags.ContainsAnyFlag(HideFlags.DontSave))
			{
				return;
			}

			UnityEditor.EditorUtility.SetDirty(obj);
#endif
		}
    }

	public enum UnwrapOption
	{
		PerSegment = 0,
		TopProject = 1,
		WidthToLength = 2,
		StretchSingleTexture = 3,
	}

	public enum BorderUnwarpOption
	{
		StraightUnwrap = 0,
		TopProject = 1,
	}

	public enum PointControl
	{
		Manual,
		Automatic,
	}

	public enum RuntimeBehaviorOption
	{
		FollowUpdateMode,
		Manual,
		Realtime,
	}

	public enum UpdateMode
	{
		Automatic,
		Manual,
		VerticesOnly,
		Realtime,
	}

	public class Border 
	{
		public GameObject gameObject;
		public MeshFilter meshFilter;
		public MeshCollider collider;
	}

	public class OrientationData
	{
		public Vector3
			position = Vector3.zero,
			tangentVector = Vector3.forward,
			normalVector = Vector3.up,
			scale = Vector3.one;

		public Vector3 up => normalVector;
		public Vector3 forward => tangentVector;
		public Vector3 right => Vector3.Cross(up, forward); 
		// remember: Unity's implementation of Vector3.Cross outputs inversed dot product.

		internal float width = 1;

        public OrientationData () {
			position = Vector3.zero;
			tangentVector = Vector3.forward;
			normalVector = Vector3.up;
		}
		
		public OrientationData (Vector3 pos, Vector3 tan, Vector3 nrm){
			Set(pos, tan, nrm);
		}

        public OrientationData(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            this.position = position;
			this.normalVector = rotation * Vector3.up;
			this.tangentVector = rotation * Vector3.forward;
			this.scale = localScale;
        }

        public void Set (Vector3 pos, Vector3 tan, Vector3 bnrm){
			position = pos;
			tangentVector = tan;
			normalVector = bnrm;
		}
		
		public Quaternion ToQuaternion () {
			return Quaternion.LookRotation(tangentVector, normalVector);
		}
	}
	
	public class PointData : OrientationData
	{
		public float distance = 0f;
		
		public PointData () : base() {
			distance = 0f;
		}
	}

	[System.Serializable]
	public class NavPointReference
	{
		public Transform transform;
		
		public GameObject gameObject {
			get{ return (transform != null) ? transform.gameObject : null; }
		}

		public Vector3 forward {
			get{ return transform.forward; }
		}

		public Vector3 up {
			get{ return transform.up; }
		}

		public Vector3 right {
			get{ return transform.right; }
		}

		public Vector3 position {
			get{ return transform.position; }
			set{ transform.position = value; }
		}

		public Vector3 localScale {
			get{ return transform.localScale; }
			set{ transform.localScale = value; }
		}

		public Quaternion rotation {
			get{ return transform.rotation; }
			set{ transform.rotation = value; }
		}

		public Vector3 eulerAngles {
			get{ return transform.eulerAngles; }
			set{ transform.eulerAngles = value; }
		}

		public string name {
			get{ return transform.name; }
			set{ transform.name = value; }
		}


		public ERPointSnap pointSnapComponent;
		public ERNavPoint navPointComponent;

		public NavPointReference(){}

		public NavPointReference(Transform transform)
		{
			this.transform = transform;
			pointSnapComponent = transform.GetComponent<ERPointSnap>();
			navPointComponent = transform.GetComponent<ERNavPoint>();
		}

		public void Update ()
		{
			if(pointSnapComponent != null) {
				pointSnapComponent.UpdatePos();
			}
		}

		public void SetParent (Transform transform)
		{
			this.transform.SetParent(transform);
		}
	}
	
	//extension methods
	public static class ERMGExtend
	{
		/// <summary>
		/// Adds component of type if it isn't present already
		/// </summary>
		public static T AddIfNotPresent <T> (this GameObject gameObject, bool dirtify = true) where T : Component
		{
			var c = gameObject.GetOrAdd<T>();
            if (dirtify)
			{
				Util.Dirtify(gameObject);
				Util.Dirtify(c);
			}

			return c;
		}

		public static PointData[] ToPointDataArray(this OrientationData[] o){
			PointData[] _p = new PointData[o.Length];
			for(int i = 0; i < o.Length; i++){
				_p[i] = new PointData();
				if(o[i] != null){
					_p[i].Set(o[i].position, o[i].tangentVector, o[i].normalVector);
				}
			}
			
			return _p;
		}
 
		/// <summary>
		/// Custom nav points equality check
		/// </summary>
		public static bool EqualsTo (this NavPointReference lhs, NavPointReference rhs)
		{
			//null check
			if(rhs == null) { 
				return lhs == null || lhs.transform == null;	
			}

			//compare
			return lhs == rhs && lhs.transform == rhs.transform;
		}
	}
}
