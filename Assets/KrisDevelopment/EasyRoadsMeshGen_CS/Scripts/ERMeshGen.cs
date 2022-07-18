using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Concurrent;
using Object = UnityEngine.Object;
using SETUtil.Extend;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KrisDevelopment.ERMG
{
	[
		AddComponentMenu("Easy Roads Mesh Gen/Mesh Gen"),
		ExecuteAlways,
		RequireComponent(typeof(MeshRenderer)),
		RequireComponent(typeof(MeshFilter)),
	]
	public sealed class ERMeshGen : MonoBehaviour
	{
		private class TerrainStrokeInfo
		{
			public Vector3 terrainSize;
			public float heightTexelDistanceX;
			public float heightTexelDistanceY;
			public float radiusFlat;
			public float radiusSmooth;
			public float totalRaidus;
			public int heightmapWidth;
			public int heightmapHeight;
			public Vector3 terrainPosition;
			public int brushSamplesX;
			public int brushSamplesY;
			public OrientationData[] cachedPathPoints;
		}

        private static ConcurrentQueue<Object> queueDelete = new ConcurrentQueue<Object>();
		private static ConcurrentQueue<ERMeshGen> queueOnDuplicate = new ConcurrentQueue<ERMeshGen>();

		public static DisposeMethod Dispose = (Object o) => {
			if (!o) { return; }
			queueDelete.Enqueue(o);
		};

		public static DisposeMethod DisposeImmediate = (Object o) => {
			SETUtil.SceneUtil.SmartDestroy(o);
		};


		public const int MAX_VERTEX_COUNT_PER_MESH = 65500;

		public Action onUpdateMesh;

		public const string NAV_POINT_NAMES = "Nav Point";
		public const string RIGHT_BORDER_NAME = "rightBorderMeshObj";
		public const string LEFT_BORDER_NAME = "leftBorderMeshObj";
		
		public List<NavPointReference> navPoints = new List<NavPointReference>();

		public float deltaWidth = 1.2f;
		internal float halfDeltaWidth { get { return deltaWidth / 2; } }

		/// <summary>
		/// The min value for this is 1,
		///  max value is determined dynamically with respect to the max mesh vetex count supported by Unity.
		/// </summary>
		public int subdivision = 1;
		public float uvScale = 1;
		public float groundOffset = 0.1f;
		public PointControl pointControl = PointControl.Manual;

		public int
			updatePointsMode = 0,
			includeCollider = 1,
			enableMeshBorders = 0;

		private OrientationData[] pathPoints = new OrientationData[0]; //nav points positions after subdivision
		private Vector3[] vertices = new Vector3[0];
		private Vector2[] uvs = new Vector2[0];
		private int[] newTriangles = new int[0];
		private int[] quadIndicesMatrix = { 2, 1, 0, 2, 3, 1 };
		private int uvSetCount = 0;

		private const int terrainBrushFrequency = 1;
		public float terrainBrushSmoothingRange = 1f;

		public UnwrapOption uvSet = UnwrapOption.WidthToLength;
		public RuntimeBehaviorOption runtimeBehavior = RuntimeBehaviorOption.Manual;

		public AnimationCurve borderCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 0.6f), new Keyframe(1, 0.6f)); //the points in 2d plane
		[Tooltip("This is the delta width and border behaviour of version 2021. " +
			"In that version delta width results in twice the size and also affects the horizontal scale of the border mesh.")]
		public bool legacyWidthMode = false; 
		public float borderScale = 1f;
		public Vector2 borderOffset = Vector2.zero;
		public float borderUvScale = 1f;
		public BorderUnwarpOption borderUvSet = BorderUnwarpOption.StraightUnwrap;

		public GameObject
			leftBorder,
			rightBorder;

		private Vector2[] borderSectionPoints; //the points in the 2d section graph
		private Vector3[] leftBorderVertices;
		private Vector2[] leftUV = new Vector2[0];
		private int[] leftTriangles = new int[0];
		private Vector3[] rightBorderVertices;
		private Vector2[] rightUV = new Vector2[0];
		private int[] rightTriangles = new int[0];

		[SerializeField] private int instanceId = 0;

		// part of a logic that prevents starting a level without having updated at least once when Automatic mode is selected
		[NonSerialized] private bool hasUpdatedOnce = false;

		private ERPathTracer m_path;
        internal ERPathTracer path
        {
			get
			{
				if (m_path == null)
				{
					m_path = this.GetOrAdd<ERPathTracer>();
					m_path.meshGen = this;
				}
				return m_path;
			}
        }

        private MeshFilter _meshFilter;
		private MeshFilter meshFilter
		{
			get
			{
				if (_meshFilter == null)
				{
					_meshFilter = GetComponent<MeshFilter>();
				}

				return _meshFilter;
			}
		}

		private MeshRenderer _meshRenderer;
		private MeshRenderer meshRenderer
		{
			get
			{
				if (_meshRenderer == null)
				{
					_meshRenderer = GetComponent<MeshRenderer>();
				}

				return _meshRenderer;
			}
		}

		
		private MeshCollider _meshCollider;
     
        private MeshCollider meshCollider
		{
			get
			{
				if (_meshCollider == null)
				{
					_meshCollider = GetComponent<MeshCollider>();
				}

				return _meshCollider;
			}
		}


#if UNITY_EDITOR
        void OnDrawGizmos()
		{
			if (updatePointsMode == (int)UpdateMode.Automatic)
			{
				if (!SelectionContainsSource())
				{
					return;
				}
			}

			var _localToWorldMtx = transform.localToWorldMatrix;
			for (int v = 0; v < vertices.Length; v++)
			{
				Gizmos.DrawWireSphere(_localToWorldMtx.MultiplyPoint3x4(vertices[v]), 0.05f);
			}
		}

#endif


        private void VersionMigrate()
        {
			var _colliders = GetComponents<MeshCollider>();
			if(_colliders.Length > 1){
				for (int i = 1; i < _colliders.Length; i++)
				{
					Dispose(_colliders[i]);
				}
				UpdateCollider(meshFilter.sharedMesh);
			}
		}

		private void OnEnable()
		{
			ForExtensions(a => a.Init(this, Dispose));
			RequestUpdate(Dispose);
		}

		private void Start()
		{
			VersionMigrate();
			Flush();
		}
		 
		private void Update()
		{
			RequestUpdate(DisposeImmediate);
			Flush();
		}

		private void OnDestroy()
		{
			if(queueDelete.Contains(gameObject) || queueDelete.Contains(this))
            {
				throw new Exception("Disposal queue contains reference to flushing object upon OnDestroy.");
            }

			Flush(true);
        }

		internal void RequestUpdate(DisposeMethod dispose = null)
		{
			if(dispose == null)
            {
				dispose = Dispose;
			}

            if (!hasUpdatedOnce)
            {
				UpdateMesh(dispose);
				return;
            }

			if (Application.isPlaying)
			{
				if (runtimeBehavior == RuntimeBehaviorOption.Manual)
				{
					updatePointsMode = (int)UpdateMode.Manual;
				}
				else if (runtimeBehavior == RuntimeBehaviorOption.Realtime)
				{
					updatePointsMode = (int)UpdateMode.Realtime;
				}
			}

			if (updatePointsMode == (int)UpdateMode.Manual)
				return;

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (updatePointsMode == (int)UpdateMode.Automatic)
				{
					if (hasUpdatedOnce && !SelectionContainsSource())
						return;
				}
			}
#endif

			if (updatePointsMode == (int)UpdateMode.VerticesOnly)
			{
				subdivision = Mathf.Max(subdivision, 1);
				SetVerts();
			}

			if (updatePointsMode == (int)UpdateMode.Realtime 
				|| (updatePointsMode == (int)UpdateMode.Automatic && !Application.isPlaying))
			{
				UpdateMesh(dispose);
			}

		}

        private void ForExtensions(Action<MeshGenExtensionComponent> action)
        {
			//TODO: cache extensions for better performance
			foreach (var _extension in GetComponents<MeshGenExtensionComponent>())
			{
				action(_extension);
			}
		}

#if UNITY_EDITOR
        private bool SelectionContainsSource()
		{
			bool _contains = false;
			if (!Selection.Contains(gameObject))
			{
				for (int s = 0; s < navPoints.Count; s++)
				{
					if (!navPoints[s].EqualsTo(null))
					{
						if (Selection.Contains(navPoints[s].gameObject))
							_contains = true;
						else
						{
							var _erPointSnapComponent = navPoints[s].pointSnapComponent;
							if (_erPointSnapComponent && _erPointSnapComponent.snapped && _erPointSnapComponent.snappedToPoint != null)
							{
								if (Selection.Contains(_erPointSnapComponent.snappedToPoint.root.gameObject) ||
									Selection.Contains(_erPointSnapComponent.snappedToPoint.gameObject))
								{
									_contains = true;
								}
							}
						}
					}
				}
			}
			else
				_contains = true;
			return _contains;
		}
#endif

		public void UpdateMesh(DisposeMethod dispose = null)
		{
			hasUpdatedOnce = true;
			Validate();
			GenerateMesh();

			ForExtensions(a => a.UpdateState(this, dispose ?? Dispose));

			if (onUpdateMesh != null)
				onUpdateMesh();
		}

		private void OnValidate()
		{
			// catch duplication events
			var toValidate = GameObject.FindObjectsOfType<ERMeshGen>().Where(a => a.instanceId == instanceId).ToList();
			toValidate.ForEach(a => a.Validate());
		}

		private void Validate()
		{
			subdivision = Mathf.Max(subdivision, 1);

			// make sure the road doesnt exceed MAX_VERTEX_COUNT, try reducing the subdivision if so.
			if (subdivision > 1)
			{
				checkVertexCount:
				var _pathPointsCount = (navPoints.Count - 1) * subdivision + 1;
				var _verticesCount = _pathPointsCount * 2;

				if(_verticesCount > MAX_VERTEX_COUNT_PER_MESH / (enableMeshBorders == 1 ? borderCurve.length : 1))
                {
					subdivision--;
					if (subdivision > 1)
					{
						goto checkVertexCount;
					}
                }
			}

#if UNITY_EDITOR
			for (int key = 0; key < borderCurve.length; key++)
			{
				AnimationUtility.SetKeyLeftTangentMode(borderCurve, key, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyRightTangentMode(borderCurve, key, AnimationUtility.TangentMode.Linear);
			}
#endif
			if (UpdateInstanceId())
			{
				queueOnDuplicate.Enqueue(this);
			}
		}

		///<summary>
		/// Creates a new nav point and adds it at the specified index,
		/// if no index has been specified the new point will be added as last.
		///</summary>
		public GameObject CreateNavPoint(int? insertAtIndex = null)
		{
			if (navPoints.Count > 0 && insertAtIndex != null)
			{
				var i = (int)insertAtIndex;
				var _erPointSnapComponent = navPoints[i].pointSnapComponent;
				if (_erPointSnapComponent != null && _erPointSnapComponent.snapped)
				{
					Debug.LogWarning("Easy Roads: End point " + navPoints[i].gameObject.name + " is snapped. Unsnap it to add new points!");
					return null;
				}
			}

			GameObject navPointObject = new GameObject();
			navPointObject.name = $"{NAV_POINT_NAMES} {navPoints.Count}";
			navPointObject.transform.SetParent(transform);
			
			var _np = navPointObject.AddIfNotPresent<ERNavPoint>(false);
			navPointObject.AddIfNotPresent<ERPointSnap>(false);

#if UNITY_EDITOR
			if (navPoints.Count >= 1)
				Selection.activeGameObject = navPointObject;
#endif

			if (navPoints.Count > 0)
			{
				int _poistionIndex = insertAtIndex ?? navPoints.Count - 1;
				navPointObject.transform.position = navPoints[_poistionIndex].transform.forward * deltaWidth + navPoints[_poistionIndex].position;
				navPointObject.transform.rotation = navPoints[_poistionIndex].rotation;
			}
			else
			{
				navPointObject.transform.position = transform.position;
			}

			if (insertAtIndex == null)
			{
				navPoints.Add(new NavPointReference(navPointObject.transform));
			}
			else
			{
				navPoints.Insert((int)insertAtIndex + 1, new NavPointReference(navPointObject.transform));
			}

			UpdateNavPoints();
			UpdateMesh();

			Util.Dirtify(navPointObject);
			return navPointObject;
		}

		///<summary>
		/// Deletes the specified NavPoint,
		/// if no nav point index has been provided the last added one will be deleted.
		///</summary>
		public void DeleteNavPoint(int? removeAtIndex = null)
		{
			int _index = removeAtIndex ?? navPoints.Count - 1;
			Dispose(navPoints[_index].gameObject);
			navPoints.RemoveAt(_index);
			UpdateNavPoints();
			UpdateMesh();
		}

		private void GenerateMesh()
		{
			subdivision = Mathf.Max(subdivision, 1);

			gameObject.AddIfNotPresent<MeshFilter>();
			gameObject.AddIfNotPresent<MeshRenderer>();

			UpdateNavPoints();
			SetVerts();
			SetTriangles();
			SetUVs();

			if (enableMeshBorders == 1)
				SetBorderUVs();

			Mesh mesh;

			if (meshFilter.sharedMesh == null)
			{
				mesh = new Mesh();
				meshFilter.sharedMesh = mesh;
			}
			else
			{
				mesh = meshFilter.sharedMesh;
			}

			mesh.Clear();
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.triangles = newTriangles;
			mesh.RecalculateNormals();

			ToggleBorders(enableMeshBorders == 1);
			UpdateBorders();
			UpdateCollider(mesh);
		}

		private void OnDuplication()
        {
			//for (int a = 0; a < navPoints.Count; a++)
			//{
			//	if (!navPoints[a].EqualsTo(null))
			//	{
			//		if (navPoints[a].pointSnapComponent != null)
			//		{
			//			navPoints[a].pointSnapComponent.ClearSnap();
			//		}
			//	}
			//}

			//if a new mesh is not assigned to the mesh filters they will point to the old one

			void DivorceBorder(Func<bool> findMethod, GameObject borderObj)
			{
				if (findMethod())
				{
					borderObj.GetComponent<MeshFilter>().sharedMesh = new Mesh();
					if (includeCollider == 1)
					{
						borderObj.GetComponent<MeshCollider>().sharedMesh = new Mesh();
					}
				}
			}

			DivorceBorder(FindLeftBorder, leftBorder);
			DivorceBorder(FindRightBorder, rightBorder);

			gameObject.AddIfNotPresent<MeshFilter>();
			meshFilter.sharedMesh = new Mesh();

            UpdateMesh();
        }

        /// <summary>
        /// Return true if instance id was different from record
        /// </summary>
        private bool UpdateInstanceId()
		{
			var _newInstanceId = GetInstanceID();
			if (this.instanceId == _newInstanceId)
			{
				return false;
			}

			this.instanceId = _newInstanceId;
			Util.Dirtify(this);
			return true;
		}

		private void UpdateNavPoints()
		{
			if (navPoints.Count == 0)
			{
				FindNavPoints();
			}

			navPoints.RemoveAll(a => a == null || a.gameObject == null);
			ValidateNavPointScale();
			UpdateNavPointIndices();

			if (pathPoints.Length <= 1)
			{
				return;
			}

			const float SCALE_DIVISOR = 2.5f;
			const float MIN_SCALE = 0.5f;

			for (int _current = 0; _current < navPoints.Count; _current++)
			{
				// assign the point rotation
				int _previous = Mathf.Max(0, _current - 1); //previous point in array
				int _previousPrim = Mathf.Max(0, _previous - 1);
				int _next = Mathf.Min(navPoints.Count - 1, _current + 1); //next point in array

				var _previousPrimPoint = navPoints[_previousPrim];
				var _previousPoint = navPoints[_previous];
				var _currentPoint = navPoints[_current];
				var _nextPoint = navPoints[_next];

				if (pointControl == PointControl.Automatic)
				{
					// calculate orientation
					if (_currentPoint.pointSnapComponent == null || !_currentPoint.pointSnapComponent.snapped)
					{

						float _zScalePrevious =
						Mathf.Max(
							Mathf.Min(
								Vector3.Distance(_previousPoint.position, _previousPrimPoint.position),
								Vector3.Distance(_previousPoint.position, _currentPoint.position)
							) / SCALE_DIVISOR,
							MIN_SCALE
						);

						Vector3 _forwardPrev = (_currentPoint.position - _previousPoint.position).normalized;
						Vector3 _forwardNext = (_nextPoint.position - _currentPoint.position).normalized;

						if (_previous == _current)
						{
							_currentPoint.rotation = Quaternion.LookRotation(_forwardNext, transform.up);
						}
						else if (_next == _current)
						{
							_currentPoint.rotation = Quaternion.LookRotation(_forwardPrev, transform.up);
						}
						else
						{
							Vector3 _previousBezeirPoint = _previousPoint.position + _previousPoint.forward * _zScalePrevious;
							Vector3 _previousBias = (_currentPoint.position - _previousBezeirPoint).normalized;
							Vector3 _nextBias = (_nextPoint.position - _currentPoint.position).normalized;

							float _distanceFromPrevious = Vector3.Distance(_currentPoint.position, _previousPoint.position);
							float _lerp = _distanceFromPrevious / (_distanceFromPrevious + Vector3.Distance(_currentPoint.position, _nextPoint.position));
							_currentPoint.rotation = Quaternion.LookRotation(Vector3.Slerp(_previousBias, _nextBias, _lerp), transform.up);
						}
					}
					else
					{
						_currentPoint.pointSnapComponent.UpdatePos();
					}

					// scale z for better curvature
					float _zScale =
						Mathf.Max(
							Mathf.Min(
								Vector3.Distance(_currentPoint.position, _previousPoint.position),
								Vector3.Distance(_currentPoint.position, _nextPoint.position)
							) / SCALE_DIVISOR,
							MIN_SCALE
						);

					if (_next != _current && _previous == _current)
					{
						// correct z scale for the first element
						_zScale = Mathf.Max(Vector3.Distance(_currentPoint.position, _nextPoint.position) / 2f, 1f);
					}
					else if (_next == _current && _previous != _current)
					{
						// correct z scale for the last element
						_zScale = Mathf.Max(Vector3.Distance(_currentPoint.position, _previousPoint.position) / 2f, 1f);
					}

					// apply z scale
					_currentPoint.localScale = new Vector3(_currentPoint.localScale.x, _currentPoint.localScale.y, _zScale);
				}
			}
		}

		///<summary> Updates the nav point names and their component index records. This is a somewhat expensive operation. </summary>
		private void UpdateNavPointIndices()
		{
			//rename the points to match their index
			for (int i = 0; i < navPoints.Count; i++)
			{
				navPoints[i].name = NAV_POINT_NAMES + " " + i;
				var _navPointComp = navPoints[i].navPointComponent;

				if (_navPointComp != null)
					_navPointComp.SetIndex(i, this);
			}
		}

		private void ValidateNavPointScale()
		{
			foreach (var navPoint in navPoints)
			{
				if (!navPoint.EqualsTo(null))
				{
					navPoint.localScale = new Vector3(Mathf.Max(navPoint.localScale.x, 0.001f), Mathf.Max(navPoint.localScale.y, 0.001f), Mathf.Max(navPoint.localScale.z, 0.001f));
				}
			}
		}

		private void SetVerts()
		{
			// -------------------------------------------------
			// calculate path positions from nav points


			if (navPoints.Count > 0)
			{
				pathPoints = new OrientationData[(navPoints.Count - 1) * subdivision + 1];
			}
			else
			{
				pathPoints = new OrientationData[0];
			}

			for (int n = 0; n < navPoints.Count; n++)
			{
				int _next = Mathf.Min(navPoints.Count - 1, n + 1); //next point in array

				var _nextPointScale = navPoints[_next].localScale;
				var _nextPointComponent = navPoints[_next].navPointComponent;
				var _nextPointWidth = (_nextPointComponent?.lockSize ?? false) ? _nextPointComponent.lockedHalfWidth : (halfDeltaWidth * _nextPointScale.x);

				var _curPointScale = navPoints[n].localScale;
				var _curPointComponent = navPoints[n].navPointComponent;
				var _curPointWidth = (_curPointComponent?.lockSize ?? false) ? _curPointComponent.lockedHalfWidth : (halfDeltaWidth * _curPointScale.x);

				pathPoints[n * subdivision] = new OrientationData(navPoints[n].position, navPoints[n].rotation, navPoints[n].localScale) {
					width = _curPointWidth,
				}; //set primary points

				if (n < navPoints.Count - 1)
				{
					// subdivision positions
					for (int b = 1; b < subdivision; b++)
					{
						//positions of sub points
						float _interval = (float)b / subdivision;
						Vector3 ap; Vector3 bp; Vector3 cp; Vector3 dp; Vector3 ep; Vector3 fp;
						ap = Vector3.Lerp(navPoints[n].position, navPoints[n].forward * _curPointScale.z + navPoints[n].position, _interval);
						cp = Vector3.Lerp(-navPoints[_next].forward * _nextPointScale.z + navPoints[_next].position, navPoints[_next].position, _interval);
						bp = Vector3.Lerp(navPoints[n].forward * _curPointScale.z + navPoints[n].position, -navPoints[_next].forward * _nextPointScale.z + navPoints[_next].position, _interval);
						dp = Vector3.Lerp(ap, bp, _interval);
						ep = Vector3.Lerp(bp, cp, _interval);
						fp = Vector3.Lerp(dp, ep, _interval);

						// rotation of sub points should be basedo on the curavature
						Vector3 _tangent = (ep - dp).normalized;
						Vector3 _normal = Vector3.Lerp(navPoints[n].up, navPoints[_next].up, _interval);
						//Vector3 _binormal = Vector3.Cross(_tangent, _normal).normalized;
						Quaternion _rot = Quaternion.LookRotation(_tangent, _normal);

						// scale of sub points

						var _scale = Vector3.Lerp(_curPointScale, _nextPointScale, _interval);
						var _width = Mathf.Lerp(_curPointWidth, _nextPointWidth, _interval);

						pathPoints[n * subdivision + b] = new OrientationData(fp, _rot, _scale) { 
							width = _width,
						};
					}
				}
			}

			//----------------------------------------------------
			// calculate vertices

			var _verticesCount = (pathPoints.Length) * 2;

			vertices = new Vector3[_verticesCount];
			borderSectionPoints = new Vector2[borderCurve.length];

			for (int v = 0; v < borderCurve.length; v++)
			{
				//assign the borderNavPoints : Vector2 values based on the borderCurve
				borderSectionPoints[v] = new Vector2(borderCurve.keys[v].time, borderCurve.keys[v].value) * borderScale + borderOffset;
			}

			if (borderSectionPoints.Length > 0)
			{
				var _n = borderCurve.length * pathPoints.Length + borderCurve.length;
				leftBorderVertices = new Vector3[_n];
				rightBorderVertices = new Vector3[_n];
			}

			var _worldToLocalMatrix = transform.worldToLocalMatrix;

			for (int n = 0; n < pathPoints.Length; n++)
			{
				if (legacyWidthMode)
				{
					// v2021
					var _pathPoint = pathPoints[n];

					var _relativePointPosition = _worldToLocalMatrix.MultiplyPoint3x4(_pathPoint.position);
					var _relativeBinormal = _worldToLocalMatrix.MultiplyVector(_pathPoint.right);
					var _roadEdge = _relativeBinormal * _pathPoint.width; // offset to the edge of the road based on road width

					vertices[n * 2] = _relativePointPosition - _roadEdge;
					vertices[n * 2 + 1] = _relativePointPosition + _roadEdge;

					// compute border
					for (int b = 0; b < borderCurve.length; b++)
					{
						if (borderCurve.length + b < rightBorderVertices.Length)
						{
							var _up = _pathPoint.up * borderSectionPoints[b].y;
							var _right = _roadEdge + _relativeBinormal * halfDeltaWidth * borderSectionPoints[b].x;

							rightBorderVertices[n * borderCurve.length + b] = _relativePointPosition + _right + _up;
							leftBorderVertices[n * borderCurve.length + b] = _relativePointPosition - _right + _up;
						}
					}
				}
				else
				{
					// v2022
					var _pathPoint = pathPoints[n];

					var _relativePointPosition = _worldToLocalMatrix.MultiplyPoint3x4(_pathPoint.position);
					var _relativeBinormal = _worldToLocalMatrix.MultiplyVector(_pathPoint.right);
					var _roadEdge = _relativeBinormal * _pathPoint.width; // offset to the edge of the road based on road width

					vertices[n * 2] = _relativePointPosition - _roadEdge;
					vertices[n * 2 + 1] = _relativePointPosition + _roadEdge;

					// compute border
					for (int b = 0; b < borderCurve.length; b++)
					{
						if (borderCurve.length + b < rightBorderVertices.Length)
						{
							var _up = _pathPoint.up * borderSectionPoints[b].y;
							var _right = _roadEdge + _relativeBinormal * borderSectionPoints[b].x;

							rightBorderVertices[n * borderCurve.length + b] = _relativePointPosition + _right + _up;
							leftBorderVertices[n * borderCurve.length + b] = _relativePointPosition - _right + _up;
						}
					}
				}
			}
		}

		private void SetTriangles()
		{
			int _quadCount = (navPoints.Count - 1) * subdivision + 1;
			if (navPoints.Count > 1) //if there is room for triangles to be drawn
				newTriangles = new int[_quadCount * 6];
			else
				newTriangles = new int[0];

			for (int quad = 1; quad < _quadCount; quad++)
			{
				for (int s2 = 0; s2 < 6; s2++)
				{
					//assign numbers
					newTriangles[(quad - 1) * 6 + s2] = quadIndicesMatrix[s2] + ((quad * 2) - 2);
				}
			}

			//BORDER
			_quadCount = ((navPoints.Count - 1) * subdivision) * borderCurve.length;//*borderCurve.length;
			if (navPoints.Count > 1)
			{ //if there is room for triangles to be drawn
				if (enableMeshBorders == 1)
				{
					rightTriangles = new int[_quadCount * 6];
					leftTriangles = new int[_quadCount * 6];
				}
			}
			else
			{
				rightTriangles = new int[0];
				leftTriangles = new int[0];
			}

			// quad matrix - used to construct triangles for each quad face
			int[] _borderQuadMatrixRight = {
				borderCurve.length + 1, 1, 0,
				0, borderCurve.length, borderCurve.length + 1
			};

			int[] _borderQuadMatrixLeft = {
				borderCurve.length + 1, 0, 1,
				0, borderCurve.length + 1, borderCurve.length
			};

			int _triCounter = 0;

			for (int ll = 0; ll < (navPoints.Count - 1) * subdivision; ll++)
			{ //Length index (horizontal index) X
				for (int lb = 0; lb < borderCurve.length - 1; lb++)
				{//vertical index Y quad
					for (int bct = 0; bct < 6; bct++)
					{ //under each quad - for matrix (assign tri points to curve points)
						if (_triCounter < rightTriangles.Length)
						{
							var _l = lb + borderCurve.length * ll;
							rightTriangles[_triCounter] = _borderQuadMatrixRight[bct] + _l;
							leftTriangles[_triCounter] = _borderQuadMatrixLeft[bct] + _l;
							_triCounter++;
						}
					}
				}
			}
		}

		private void SetUVs()
		{
			int _uvs_y_array = vertices.Length / 2;
			uvs = new Vector2[vertices.Length];

			//get point-to-point distance and mesh Length
			float previousDistance = 0;
			float[] _ptpDistance = new float[pathPoints.Length];

			for (int ptp = 0; ptp < _ptpDistance.Length - 1; ptp++)
			{
				_ptpDistance[ptp] = Vector3.Distance(pathPoints[ptp].position, pathPoints[ptp + 1].position);
			}


			switch (uvSet)
			{
				case UnwrapOption.PerSegment: //per segment
					uvSetCount = 0;
					for (int uvy = 0; uvy < _uvs_y_array; uvy++)
					{
						for (int uvx = 0; uvx < 2; uvx++)
						{
							uvs[uvSetCount] = new Vector2(uvx * uvScale, uvy * uvScale);

							uvSetCount++;
						}
					}
					break;
				case UnwrapOption.TopProject: //top projection
					{
						var _localToWorld = transform.localToWorldMatrix;
						for (int uvp = 0; uvp < uvs.Length; uvp++)
						{
							var _v = _localToWorld.MultiplyPoint3x4(vertices[uvp]);
							uvs[uvp] = new Vector2(_v.x, _v.z) * uvScale;
						}
					}
					break;
				case UnwrapOption.WidthToLength: //width-to-Length (match width)
					uvSetCount = 0;
					previousDistance = 0;

					for (int uvny = 0; uvny < _ptpDistance.Length && uvny < uvs.Length; uvny++)
					{
						for (int uvnx = 0; uvnx < 2; uvnx++)
						{
							uvs[uvSetCount] = new Vector2(uvnx * uvScale, previousDistance * uvScale / deltaWidth);
							uvSetCount++;
						}
						previousDistance += _ptpDistance[uvny];
					}
					//last segment uvs
					if (uvs.Length >= 4 && pathPoints.Length > 2)
					{
						float _lastPointDistance = Vector3.Distance(pathPoints.Last().position, pathPoints[pathPoints.Length - 2].position);
						var _v = new Vector2(0, 1) * _lastPointDistance * uvScale / deltaWidth;

						uvs[uvs.Length - 1] = uvs[uvs.Length - 3] + _v;
						uvs[uvs.Length - 2] = uvs[uvs.Length - 4] + _v;

					}

					break;
				case UnwrapOption.StretchSingleTexture: //stretch single texture
					for (int uvny = 0; uvny < pathPoints.Length; uvny++)
					{
						for (int uvnx = 0; uvnx < 2; uvnx++)
						{
							int uvIndex = uvnx + uvny * 2;
							uvs[uvIndex] = new Vector2(uvnx * uvScale, 1f / pathPoints.Length * uvny * uvScale);
						}
					}
					break;
			}
		}

		void SetBorderUVs()
		{
			rightUV = new Vector2[rightBorderVertices.Length];
			leftUV = new Vector2[rightBorderVertices.Length];

			switch (borderUvSet)
			{
				case BorderUnwarpOption.StraightUnwrap: //straight unwrap
					{
						//get point-to-point distance and mesh Length
						float[] _ptpDistance = new float[pathPoints.Length];
						for (int ptp = 0; ptp < _ptpDistance.Length - 1; ptp++)
						{
							_ptpDistance[ptp] = Vector3.Distance(pathPoints[ptp].position, pathPoints[ptp + 1].position);
						}

						var _uvSetCount = 0;
						float _longitudalDistance = 0;

						for (int uvny = 0; uvny < _ptpDistance.Length && uvny < rightUV.Length; uvny++)
						{
							if (borderCurve.length == 0)
							{
								break;
							}

							float _transverseDist = 0f;
							Vector2 _previousKeyPoint = new Vector2(borderCurve[0].time, borderCurve[0].value);

							for (int uvnx = 0; uvnx < borderCurve.length; uvnx++)
							{
								var _currentKey = borderCurve[uvnx];
								var _keyPoint = new Vector2(_currentKey.time, _currentKey.value);
								var _distanceFromPreviousKey = Vector2.Distance(_previousKeyPoint, _keyPoint);
								_transverseDist += _distanceFromPreviousKey; //the distance between the vertices based on key time and value

								rightUV[_uvSetCount] = new Vector2(_transverseDist * borderUvScale, _longitudalDistance * borderUvScale);
								leftUV[_uvSetCount] = new Vector2(_transverseDist * borderUvScale, _longitudalDistance * borderUvScale);
								
								_uvSetCount++;
								_previousKeyPoint = _keyPoint;
							}
							_longitudalDistance += _ptpDistance[uvny];
						}
						//fix last segment uvs
						if (rightUV.Length >= borderCurve.length && leftUV.Length >= borderCurve.length && pathPoints.Length > 0)
						{
							float lastPointDistance = Vector3.Distance(pathPoints[pathPoints.Length - 1].position, navPoints[navPoints.Count - 1].position);
							for (int uvnx1 = 0; uvnx1 < borderCurve.length && uvnx1 < rightUV.Length; uvnx1++)
							{
								rightUV[rightUV.Length - uvnx1 - 1] = rightUV[rightUV.Length - uvnx1 - borderCurve.length - 1] + new Vector2(0, 1) * lastPointDistance * uvScale;
								leftUV[rightUV.Length - uvnx1 - 1] = leftUV[rightUV.Length - uvnx1 - borderCurve.length - 1] + new Vector2(0, 1) * lastPointDistance * uvScale;
							}
						}
					}
					break;
				case BorderUnwarpOption.TopProject:
					{
						var _localToWorld = transform.localToWorldMatrix;
						for (int uvpb = 0; uvpb < rightUV.Length && uvpb < leftUV.Length; uvpb++)
						{
							{
								var _r = _localToWorld.MultiplyPoint3x4(rightBorderVertices[uvpb]);
								rightUV[uvpb] = new Vector2(_r.x, _r.z) * borderUvScale;
							}
							{
								var _l = _localToWorld.MultiplyPoint3x4(leftBorderVertices[uvpb]);
								leftUV[uvpb] = new Vector2(_l.x, _l.z) * borderUvScale;
							}
						}
					}
					break;
			}
		}

		///<summary>
		/// Moves all nav points to the surface of whatever object lies underneath,
		/// while keeping the specified offset.
		///</summary>
		public void GroundPoints(float offset)
		{
			RaycastHit _hit;

			//temporary variable used to store the position of the points to be used to cast a ray from there
			Vector3[] _pointPos = new Vector3[navPoints.Count];

			for (int p = 0; p < _pointPos.Length; p++)
			{
				_pointPos[p] = navPoints[p].position;
			}

			for (int vg = 0; vg < navPoints.Count; vg++)
			{
				if (Physics.Raycast(_pointPos[vg], Vector3.down, out _hit))
				{
					navPoints[vg].position = _hit.point + _hit.normal * offset;
					Quaternion normalQuaternion = Quaternion.FromToRotation(Vector3.up, _hit.normal);
					if (navPoints[vg].pointSnapComponent)
					{ //Snap Points Check
						if (!navPoints[vg].pointSnapComponent.snapped)
						{
							navPoints[vg].eulerAngles = new Vector3(normalQuaternion.eulerAngles.x, navPoints[vg].eulerAngles.y, normalQuaternion.eulerAngles.z);
						}
					}
					else
					{
						navPoints[vg].eulerAngles = new Vector3(normalQuaternion.eulerAngles.x, navPoints[vg].eulerAngles.y, normalQuaternion.eulerAngles.z);
					}
				}
			}

			UpdateMesh();
		}

		public void ProcessUnderlyingTerrains()
		{
			HashSet<Terrain> _terrains = new HashSet<Terrain>();
			foreach (var _navPoint in GetNavPoints())
			{
				RaycastHit _hit;
				if (Physics.Raycast(_navPoint.position, Vector3.down, out _hit))
				{
					var _hitTerrain = _hit.transform.GetComponent<Terrain>();

					if (_hitTerrain != null)
					{
						_terrains.Add(_hitTerrain);
					}
				}
			}

#if UNITY_EDITOR
			if (_terrains.Count == 0)
			{
				EditorUtility.DisplayDialog("Morph Terrain Failed", "Could not find any underlying terrain!", "Close");
			}
#endif

			int _progress = 0;
			foreach (var _terrain in _terrains)
			{
				MorphTerrain(_terrain, string.Format("Processing Terrain {0}/{1}", ++_progress, _terrains.Count));
			}
		}

		public void MorphTerrain(Terrain terrain, string progressMessage = "")
		{
#if UNITY_EDITOR
			EditorUtility.DisplayProgressBar(progressMessage, "Collecting path points", 0f);
#endif
			var _pathPoints = GetOrientedPathPoints();

			if (_pathPoints.Length == 0)
				goto end;

			float _brushFlatRadius = deltaWidth / 2;
			float _brushSmoothRadius = terrainBrushSmoothingRange;

			var _terrainData = terrain.terrainData;
			var _terrainPosition = terrain.transform.position;
			int _heightmapHeight = (int)_terrainData.heightmapResolution;
			int _heightmapWidth = (int)_terrainData.heightmapResolution;
			float[,] _terrainHeights = _terrainData.GetHeights(0, 0, _heightmapHeight, _heightmapWidth);
			List<int[,]> _terrainDetails = new List<int[,]>();
			var _terrainSize = _terrainData.size;

			// collect details
			for (int i = 0; i < terrain.terrainData.detailPrototypes.Length; i++)
			{
				_terrainDetails.Add(_terrainData.GetDetailLayer(0, 0, _terrainData.detailWidth, _terrainData.detailHeight, i));
			}

			var _totalRadius = _brushFlatRadius + _brushSmoothRadius;
			var _heightTexelDistanceX = _terrainSize.x / _heightmapWidth;
			var _heightTexelDistanceY = _terrainSize.z / _heightmapHeight;

			var _terrainStrokeInfo = new TerrainStrokeInfo()
			{
				terrainSize = _terrainSize,
				heightTexelDistanceX = _heightTexelDistanceX,
				heightTexelDistanceY = _heightTexelDistanceY,
				radiusFlat = _brushFlatRadius,
				radiusSmooth = _brushSmoothRadius,
				totalRaidus = _totalRadius,
				heightmapWidth = _heightmapWidth,
				heightmapHeight = _heightmapHeight,
				terrainPosition = terrain.GetPosition(),
				brushSamplesX = (int)(_totalRadius / _heightTexelDistanceX),
				brushSamplesY = (int)(_totalRadius / _heightTexelDistanceY),
				cachedPathPoints = GetOrientedPathPoints(),
			};

			int _progress = 0;
			var _lastPathPoint = _pathPoints[0];

			for (int i = 0; i < _pathPoints.Length - 1; i++)
			{

#if UNITY_EDITOR
				EditorUtility.DisplayProgressBar(progressMessage, "Processing...", (float)_progress++ / _pathPoints.Length);
#endif
				if (_lastPathPoint != _pathPoints[i] && Vector3.Distance(_lastPathPoint.position, _pathPoints[i].position) < _brushFlatRadius)
				{
					if (Vector3.Distance(_pathPoints[i + 1].position, _pathPoints[i].position) < _brushFlatRadius * 2)
					{
						continue;
					}
				}

				_lastPathPoint = _pathPoints[i];

				// for each quad
				var _pointA = _pathPoints[i];
				var _pointB = _pathPoints[i + 1];

				int _positionsCount = Mathf.CeilToInt((_pointB.position - _pointA.position).magnitude / _brushFlatRadius) * terrainBrushFrequency;
				var _strokePositions = new List<Vector3>(_positionsCount);

				for (int p = 0; p < _positionsCount; p++)
				{
					_strokePositions.Add(Vector3.Lerp(_pointA.position, _pointB.position, (float)p / _positionsCount));
				}

				// apply stroke positions
				foreach (var _strokePosition in _strokePositions)
				{
					int _strokeHeightmapCenterX = (int)(_strokePosition.x / _terrainStrokeInfo.heightTexelDistanceX);
					int _strokeHeightmapCenterY = (int)(_strokePosition.z / _terrainStrokeInfo.heightTexelDistanceY);

					ApplyBrushOnTerrainHeight(
						ref _terrainHeights,
						_strokeHeightmapCenterX,
						_strokeHeightmapCenterY,
						_terrainStrokeInfo,
						_pointA,
						_pointB,
						_strokePosition
					);
				}
			}

			_terrainData.SetHeights(0, 0, _terrainHeights);

			for (int i = 0; i < _terrainDetails.Count; i++)
			{
				_terrainData.SetDetailLayer(0, 0, i, _terrainDetails[i]);
			}

#if UNITY_EDITOR
			EditorUtility.SetDirty(_terrainData);
#endif

		end:;
#if UNITY_EDITOR
			EditorUtility.ClearProgressBar();
#endif
		}

		private void ApplyBrushOnTerrainHeight(ref float[,] heights, int centerX, int centerY, TerrainStrokeInfo strokeInfo, OrientationData pointA, OrientationData pointB, Vector3 strokePosition)
		{
			// loop through the brush's area
			for (int x = -strokeInfo.brushSamplesX; x < strokeInfo.brushSamplesX * 2; x++)
			{
				for (int y = -strokeInfo.brushSamplesY; y < strokeInfo.brushSamplesY * 2; y++)
				{
					int _xSample = centerX + x;
					int _ySample = centerY + y;

					if (_xSample < 0 || _xSample >= strokeInfo.heightmapWidth || _ySample < 0 || _ySample >= strokeInfo.heightmapHeight)
					{
						continue;
					}

					var _currentRealTerrainHeight = heights[_ySample, _xSample] * strokeInfo.terrainSize.y;
					var _point3f = new Vector3(x * strokeInfo.heightTexelDistanceX, _currentRealTerrainHeight, y * strokeInfo.heightTexelDistanceY) + strokePosition;
					var _projectedPoint = SnapPointToPath(_point3f, strokeInfo.cachedPathPoints);
					var _horizontalPlaneDistance = Vector2.Distance(new Vector2(_point3f.x, _point3f.z), new Vector2(_projectedPoint.x, _projectedPoint.z));
					var _interval = Mathf.Max(0, _horizontalPlaneDistance - strokeInfo.radiusFlat) / (strokeInfo.radiusSmooth != 0 ? strokeInfo.radiusSmooth : 1);
					var _finalHeight = Mathf.Lerp(_projectedPoint.y - groundOffset, _currentRealTerrainHeight, _interval);

					heights[_ySample, _xSample] = (_finalHeight) / strokeInfo.terrainSize.y;
				}
			}
		}

		public Vector3 SnapPointToPath(Vector3 point, OrientationData[] cachedPathPoints = null)
		{
			var _pathPoints = cachedPathPoints ?? GetOrientedPathPoints();

			// no points
			if (_pathPoints.Length == 0)
			{
				return point;
			}

			// single point
			if (_pathPoints.Length == 1)
			{
				return _pathPoints[0].position;
			}

			int _closestPointIndex = 0;
			float _closestDistance = float.MaxValue;

			for (int i = 0; i < _pathPoints.Length; i++)
			{
				var _distance = Vector3.Distance(point, _pathPoints[i].position);
				if (_distance < _closestDistance)
				{
					_closestPointIndex = i;
					_closestDistance = _distance;
				}
			}

			Vector3 _projectionVector = Vector3.zero;
			Vector3 _closestPointPosition = _pathPoints[_closestPointIndex].position;

			if (_closestPointIndex == 0)
			{
				// is first
				_projectionVector = (_pathPoints[_closestPointIndex + 1].position - _closestPointPosition).normalized;
				return Vector3.Project(point - _closestPointPosition, _projectionVector) + _closestPointPosition;
			}
			else if (_closestPointIndex == _pathPoints.Length - 1)
			{
				// is last
				_projectionVector = (_pathPoints[_closestPointIndex - 1].position - _closestPointPosition).normalized;
				return Vector3.Project(point - _closestPointPosition, _projectionVector) + _closestPointPosition;
			}


			// is somewhere in the middle
			var _projectionVectorNext = (_pathPoints[_closestPointIndex + 1].position - _closestPointPosition);
			var _projectionVectorPrev = (_pathPoints[_closestPointIndex - 1].position - _closestPointPosition);

			var _projectedNext = Vector3.Project(point - _closestPointPosition, _projectionVectorNext) + _closestPointPosition;
			var _projectedPrev = Vector3.Project(point - _closestPointPosition, _projectionVectorPrev) + _closestPointPosition;

			if (_projectionVectorPrev.magnitude < Vector3.Distance(_projectedPrev, _pathPoints[_closestPointIndex - 1].position)
				&& Vector3.Distance(_projectedNext, _pathPoints[_closestPointIndex + 1].position) < _projectionVectorNext.magnitude)
			{
				return _projectedNext;
			}
			else
			{
				return _projectedPrev;
			}
		}

		public void ResetMesh()
		{
			ForExtensions(a => a.Clear(Dispose));

			for (int nav = 0; nav < navPoints.Count; nav++)
			{
				if (!navPoints[nav].EqualsTo(null))
				{
					Dispose(navPoints[nav].gameObject);
				}
			}

			navPoints.Clear();
			vertices = new Vector3[0];
			uvs = new Vector2[0];
			newTriangles = new int[0];

			CreateNavPoint();
			GenerateMesh();
		}


		/// <summary>
		/// Remove the MeshGen and its components and leave only the generated mesh and objects in their current state.
		/// Intended for play mode and runtime.
		/// This method won't export meshes to assets.
		/// </summary>
		public void RuntimeFinalize(DisposeMethod dispose, AssetCreationMethod assetCreation = null)
		{
			ForExtensions(a => a.OnFinalize(this, dispose, assetCreation));

			foreach (var navPoint in navPoints)
			{
				dispose(navPoint.gameObject);
			}
			navPoints.Clear();

			foreach (var _component in
				(new HashSet<Component>())
				.Concat(GetComponents<MeshGenExtensionComponent>())
				.Concat(GetComponents<ERPathTracer>())
				.Concat(GetComponents<ERMeshGen>()))
			{
				dispose(_component);
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// This will remove all components except MeshCollider, MeshFilter and MeshRenderer and will export all related mesh assets
		/// </summary>
		public void EditorFinalize(DisposeMethod dispose)
		{
			var _path = Application.dataPath;

			_path = UnityEditor.EditorUtility.OpenFolderPanel("Export Assets", _path, "MeshGenExport");
			if (_path.StartsWith(Application.dataPath))
			{
				_path = "Assets" + _path.Substring(Application.dataPath.Length);
			}

			var _finalName = name.Replace(' ', '_');
			var _genericPiecePath = _path + "/" + _finalName + "_Gen.asset";
			var _meshGenMeshPath = _path + "/" + _finalName + "_Mesh.asset";
			var _leftBorderMeshPath = _path + "/" + _finalName + "_LBorderMesh.asset";
			var _rightBorderMeshPath = _path + "/" + _finalName + "_RBorderMesh.asset";

			EditorFinalizeERObject(gameObject, _meshGenMeshPath);

			if (leftBorder != null)
			{
				EditorFinalizeERObject(leftBorder, _leftBorderMeshPath);
			}

			if (rightBorder != null)
			{
				EditorFinalizeERObject(rightBorder, _rightBorderMeshPath);
			}

			RuntimeFinalize(dispose, (GameObject o) => { EditorFinalizeERObject(o, _genericPiecePath); });

			Util.Dirtify(this);
		}

		private void EditorFinalizeERObject(GameObject erMeshGenObject, string path)
		{
			{
				var _filter = erMeshGenObject.GetComponent<MeshFilter>();
				if (_filter)
				{
					_filter.sharedMesh = ExportMeshAsset(path, _filter.sharedMesh);
					Util.Dirtify(_filter);
				}
			}
			{
				var _collider = erMeshGenObject.GetComponent<MeshCollider>();
				if (_collider)
				{
					_collider.sharedMesh = ExportMeshAsset(path.Replace(".asset", "_COL.asset"),
						_collider.sharedMesh);
				}
				Util.Dirtify(_collider);
			}
        }

        private static Mesh ExportMeshAsset(string path, Mesh source)
        {
			Debug.Assert(source != null, "Source mesh should not be null!");
            
			path = AssetDatabase.GenerateUniqueAssetPath(path);
            var _mesh = source;

            // Check if the mesh is already an asset. If so, create a new copy.
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_mesh)))
            {
                _mesh = Mesh.Instantiate(source);
            }

            AssetDatabase.CreateAsset(_mesh, path);
			return AssetDatabase.LoadAssetAtPath<Mesh>(path);
		}
#endif

        private void ToggleBorders(bool state)
		{
			if (leftBorder != null && rightBorder != null && state)
				return;

			if (leftBorder == null && rightBorder == null && !state)
				return;

			if (state)
			{
				var _material = Util.GetDefaultMaterial();

				if (!FindLeftBorder())
				{
					var _newGameObject = new GameObject();
					leftBorder = _newGameObject;

					leftBorder.transform.parent = this.transform;
					leftBorder.name = LEFT_BORDER_NAME;
					leftBorder.transform.position = transform.position;
					leftBorder.transform.localRotation = Quaternion.identity;
					leftBorder.AddIfNotPresent<MeshFilter>();
					var _renderer = leftBorder.GetOrAdd<MeshRenderer>();
					_renderer.sharedMaterial = _material;
					Util.Dirtify(leftBorder);
					Util.Dirtify(_renderer);
				}
				if (!FindRightBorder())
				{
					var _newGameObject = new GameObject();
					rightBorder = _newGameObject;

					rightBorder.transform.parent = this.transform;
					rightBorder.name = RIGHT_BORDER_NAME;
					rightBorder.transform.position = transform.position;
					rightBorder.transform.localRotation = Quaternion.identity;
					rightBorder.AddIfNotPresent<MeshFilter>();
					var _renderer = rightBorder.GetOrAdd<MeshRenderer>();
					_renderer.sharedMaterial = _material;
					Util.Dirtify(rightBorder);
					Util.Dirtify(_renderer);
				}

				var _leftBorderMeshFilter = leftBorder.GetComponent<MeshFilter>();

				if (_leftBorderMeshFilter.sharedMesh != null)
				{
					Dispose(_leftBorderMeshFilter.sharedMesh);
				}
				_leftBorderMeshFilter.sharedMesh = new Mesh();
				Util.Dirtify(_leftBorderMeshFilter);


				var _rightBorderMeshFilter = rightBorder.GetComponent<MeshFilter>();

				if (_rightBorderMeshFilter.sharedMesh != null)
				{
					Dispose(_rightBorderMeshFilter.sharedMesh);
				}
				_rightBorderMeshFilter.sharedMesh = new Mesh();
				Util.Dirtify(_rightBorderMeshFilter);

			}
			else
			{
				if (FindRightBorder())
				{
					Dispose(rightBorder);
				}
				rightBorder = null;

				if (FindLeftBorder())
				{
					Dispose(leftBorder);
				}
				leftBorder = null;
			}
		}

		private void UpdateBorders()
		{
			if (enableMeshBorders == 0)
			{
				return;
			}

			if (leftBorder != null)
			{
				var _borderMeshFilter = leftBorder.GetComponent<MeshFilter>();
				var _mesh = _borderMeshFilter.sharedMesh;
				if (_mesh == null)
				{
					_mesh = _borderMeshFilter.sharedMesh = new Mesh();
					Util.Dirtify(_borderMeshFilter);
				}

				_mesh.Clear();
				_mesh.vertices = leftBorderVertices;
				_mesh.uv = leftUV;
				_mesh.triangles = leftTriangles;
				_mesh.RecalculateNormals();
			}


			if (rightBorder != null)
			{
				var _borderMeshFilter = rightBorder.GetComponent<MeshFilter>();
				var _mesh = _borderMeshFilter.sharedMesh;
				if (_mesh == null)
				{
					_mesh = _borderMeshFilter.sharedMesh = new Mesh();
				}

				_mesh.Clear();
				_mesh.vertices = rightBorderVertices;
				_mesh.uv = rightUV;
				_mesh.triangles = rightTriangles;
				_mesh.RecalculateNormals();
			}
		}

		private bool FindLeftBorder()
		{
			if (leftBorder != null)
				return true;

			Transform t = transform.Find(ERMeshGen.LEFT_BORDER_NAME);
			if (t != null)
			{
				leftBorder = (GameObject)t.gameObject;
				return true;
			}

			//return false if border has not been found
			return false;
		}

		private bool FindRightBorder()
		{
			if (rightBorder != null)
				return true;

			Transform t = transform.Find(ERMeshGen.RIGHT_BORDER_NAME);
			if (t != null)
			{
				rightBorder = (GameObject)t.gameObject;
				return true;
			}

			//return false if border has not been found
			return false;
		}

		/// <summary>
		/// Parents children nav points where each next nav point is a child of the previous.
		/// This method is prone to breaking the proper behavior of the road, so use it carefully.
		/// </summary>
		private void ReparentPoints(bool parent)
		{
			for (int p = 1; p < navPoints.Count; p++)
			{
				if (parent)
				{
					navPoints[p].SetParent(navPoints[p - 1].transform);
				}
				else
				{
					navPoints[p].SetParent(transform);
				}
			}
		}

		private void UpdateCollider(Mesh colMesh)
		{
			if (includeCollider == 1)
			{
				gameObject.AddIfNotPresent<MeshCollider>();
				meshCollider.sharedMesh = colMesh; //assign the updated mesh to the collider;
				
				Util.Dirtify(meshCollider);


				if (enableMeshBorders == 1)
				{
					var _rightBorderCol = rightBorder.AddIfNotPresent<MeshCollider>(true);
					var _leftBorderCol = leftBorder.AddIfNotPresent<MeshCollider>(true);

					//TODO: this probably shouldn't be here (borders should be handled separately in a more abstract way)
					_rightBorderCol.sharedMesh = rightBorder.GetComponent<MeshFilter>().sharedMesh; //assign the updated mesh to the collider;
					_leftBorderCol.sharedMesh = leftBorder.GetComponent<MeshFilter>().sharedMesh; //assign the updated mesh to the collider;

					Util.Dirtify(_leftBorderCol);
					Util.Dirtify(_rightBorderCol);
				}
			}
			else
			{
				if (meshCollider)
				{
					Dispose(meshCollider);
				}
				if (FindRightBorder())
				{
					var _rightBorderCol = rightBorder.GetComponent<MeshCollider>();
					if (_rightBorderCol)
						Dispose(_rightBorderCol);
				}
				if (FindLeftBorder())
				{
					var _leftBorderCol = leftBorder.GetComponent<MeshCollider>();
					if (_leftBorderCol)
						Dispose(_leftBorderCol);
				}
			}
		}

		/// <summary>
		/// Processes all queued items.
		/// Delete queued objects, update duplicates, etc.
		/// Some operations can't be handled during Awake or OnValidate, so a queue is used, which then is 'flushed' when appropriate.
		/// </summary>
		public static void Flush(bool isExplicitOnDestroyOrDisable = false)
		{
			while (queueDelete.Count > 0)
			{
				Object _o;
				if (queueDelete.TryDequeue(out _o) && _o != null)
				{
					SETUtil.SceneUtil.SmartDestroy(_o);
				}
			}

            if (!isExplicitOnDestroyOrDisable) {
			//Note: OnDuplication might spawn new instances, which if done during Destroy or Disable might generate errors.
				while (queueOnDuplicate.Count > 0)
				{
					ERMeshGen _o;
					if (queueOnDuplicate.TryDequeue(out _o) && _o != null)
					{
						_o.OnDuplication();
					}
				}
			}
		}

		/// <summary>
		/// In the case nav points array is out of date, try to find nav point children by name
		/// </summary>
		public void FindNavPoints()
		{
			bool foundAllNavPoints = false;
			int navPointCounter = 0;
			List<NavPointReference> _navPoints = new List<NavPointReference>();

			while (!foundAllNavPoints)
			{
				Transform point = transform.Find($"{NAV_POINT_NAMES} {navPointCounter}");
				if (point != null)
				{
					_navPoints.Add(new NavPointReference(point));
					var _erNavPointComponent = point.GetComponent<ERNavPoint>();
					if (_erNavPointComponent != null)
						_erNavPointComponent.assignedMeshGen = this;

					navPointCounter++;
				}
				else
					foundAllNavPoints = true;
			}

			navPoints = _navPoints;
		}

		/// <summary>
		/// Returns data about the nav point transforms
		/// </summary>
		public NavPointReference[] GetNavPoints()
		{
			return navPoints.ToArray();
		}

		/// <summary>
		/// Returns an array with the path information after subdivision (offset = 0,0)
		/// </summary>
		public OrientationData[] GetOrientedPathPoints()
		{
			return GetOrientedPathPoints(0f, 0f);
		}

		/// <summary>
		/// Returns an array with offset path positions
		/// </summary>
		public OrientationData[] GetOrientedPathPoints(float horizontalOffset, float verticalOffset)
		{
			if (pathPoints.Length == 0)
			{
				// make sure we have oriented path points
				SetVerts();
			}

			OrientationData[] _positions = new OrientationData[pathPoints.Length];

			for (int i = 0; i < _positions.Length; i++)
			{
				var _normal = pathPoints[i].up;
				var _tangent = pathPoints[i].forward;
				var _binormal = pathPoints[i].right * pathPoints[i].width;

				_positions[i] = new OrientationData(pathPoints[i].position + _normal * verticalOffset + _binormal * horizontalOffset, _tangent, _normal)
				{
					width = pathPoints[i].width,
					scale = pathPoints[i].scale,
				};
			}

			return _positions;
		}
    }
}
