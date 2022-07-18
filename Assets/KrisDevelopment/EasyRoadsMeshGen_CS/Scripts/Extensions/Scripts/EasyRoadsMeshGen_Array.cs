using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KrisDevelopment.ERMG
{
    [
        AddComponentMenu("Easy Roads Mesh Gen/Extensions/Array"),
        ExecuteAlways
    ]
    public class EasyRoadsMeshGen_Array : MeshGenExtensionComponent
    {
        //types:
        public enum FitType
        {
            FixedAmount,
            FitLength,
            FitPath
        }

        [System.Serializable]
        public class ArrayObject
        {
            public enum InstanceType
            {
                ArrayElement,
                CombinedMesh,
            }

            public GameObject prefab;
            public FitType fitType = FitType.FixedAmount;

            public int amount = 0;

            public float
                length,
                verticalOffset,
                horizontalOffset,
                pathOffset,
                rotation;

            public bool invert;

            [SerializeField] [FormerlySerializedAs("instances")] public List<GameObject> legacyInstances = new List<GameObject>();
            [NonSerialized] public List<GameObject> instances = new List<GameObject>();
            [NonSerialized] public List<GameObject> combinedMeshObjects = new List<GameObject>();


            public void Clear(DisposeMethod dispose)
            {
                for (int i = 0; i < instances.Count; i++)
                    if (instances[i] != null)
                        dispose(instances[i]);

                instances.Clear();

                for (int i = 0; i < combinedMeshObjects.Count; i++)
                    if (combinedMeshObjects[i] != null)
                        dispose(combinedMeshObjects[i]);
                
                combinedMeshObjects.Clear();
            }

            /// <summary>
            /// Collects existing unlinked instances from the object children, deletes unattended/stale ones.
            /// </summary>
            internal void FindExistingInstances(EasyRoadsMeshGen_Array array, DisposeMethod dispose)
            {
                var _instancesHash = new HashSet<GameObject>();
                var _combinedMeshesHash = new HashSet<GameObject>();

                foreach (var _instance in array.GetComponentsInChildren<ERArrayObjectInstance>())
                {
                    if (_instance.GetBinding() == array)
                    {
                        // the element belongs to this array
                        var _gameObject = _instance.gameObject;
                        if (_instance.GetInstanceType() == InstanceType.ArrayElement)
                        {
                            _instancesHash.Add(_gameObject);
                        }
                        else if (_instance.GetInstanceType() == InstanceType.CombinedMesh)
                        {
                            _combinedMeshesHash.Add(_gameObject);
                        }
                    }
                    else if (_instance.GetBinding() == null)
                    {
                        // House keeping: the element should be deleted, its owning array is long gone.
                        dispose(_instance.gameObject);
                    }
                }

                instances = _instancesHash.ToList();
                combinedMeshObjects = _combinedMeshesHash.ToList();
            }

            internal void ExportMeshAssets(AssetCreationMethod assetCreation)
            {
                instances.ForEach(o => assetCreation?.Invoke(o));
                combinedMeshObjects.ForEach(o => assetCreation?.Invoke(o));
            }
        }

        //--

        public const int MAX_VERTEX_COUNT_PER_MESH = 65500;

        public float length;
        public List<ArrayObject> arrayObjects = new List<ArrayObject>();

        [Tooltip("When the game starts, all meshes in the *Array* instances will combine into one.")]
        public bool combineMeshes;
        
        [Tooltip("If true, the parent-relative position, scale and rotation are modified such that " +
            "the object keeps the same world space position, rotation and scale as before.")]
        public bool ignoreParentSize = true;

        [Tooltip("Suspend all updates even if the component is subscribed to the Path Tracer")]
        public bool suspend;



        /// <summary>
        /// This method handles transferring and updating stuff that have changed between versions.
        /// </summary>
        private void VersionMigrate(ERMeshGen meshGen)
        {
            // Remove serialized array instances from prior to v2022.x.x
            foreach (var a in arrayObjects)
            {
                bool _needsMigrate = a.legacyInstances.Count > 0;

                if (_needsMigrate)
                {
                    foreach (var i in a.legacyInstances)
                    {
                        SETUtil.SceneUtil.SmartDestroy(i);
                    }
                    a.legacyInstances.Clear();

                    // Delete leftover instances
                    foreach (Transform _child in transform)
                    {
                        if (_child.name == a.prefab.name)
                        {
                            SETUtil.SceneUtil.SmartDestroy(_child.gameObject);
                        }
                    }
                }
            }

            Util.Dirtify(this);
        }

        private void CombineMeshes(ArrayObject arrayObject, DisposeMethod dispose, List<Matrix4x4> positions, Mesh mesh, Material[] materials, bool generateCollider)
        {
            if (arrayObject == null)
            {
                return;
            }

            if (!mesh.isReadable)
            {
                throw new Exception($"Unreadable mesh {mesh}");
            }


            // delete old meshes
            arrayObject.combinedMeshObjects.ForEach((a) =>
            {
                dispose(a);
            });
            arrayObject.combinedMeshObjects.Clear();

            var _meshVertexCount = mesh.vertices.Length;
            var _maxPositionsPerMesh = _meshVertexCount * positions.Count;
            int _combinedMeshesExpected = (_maxPositionsPerMesh) / MAX_VERTEX_COUNT_PER_MESH;
            
            var _combinedMeshes = new List<List<CombineInstance>>(_combinedMeshesExpected) {
                    new List<CombineInstance>(_combinedMeshesExpected > 1 ? _maxPositionsPerMesh : positions.Count)
                };

            var _combinedIndex = 0;
            int _currentVertexCount = 0;

            foreach (var _position in positions)
            {
                if (_currentVertexCount + _meshVertexCount > MAX_VERTEX_COUNT_PER_MESH)
                {
                    _combinedMeshes.Add(new List<CombineInstance>());
                    _combinedIndex++;
                    _currentVertexCount = 0;
                }

                _combinedMeshes[_combinedIndex].Add(new CombineInstance()
                {
                    mesh = mesh,
                    transform = _position,
                });

                _currentVertexCount += _meshVertexCount;
            }

            foreach (var _combinedMesh in _combinedMeshes)
            {
                var _combinedObject = new GameObject("_CombinedMesh");
                _combinedObject.transform.SetParent(transform, true);
                _combinedObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

                var _filter = _combinedObject.AddIfNotPresent<MeshFilter>();
                var _renderer = _combinedObject.AddIfNotPresent<MeshRenderer>();
                var _instanceComponent = _combinedObject.AddIfNotPresent<ERArrayObjectInstance>();
                _instanceComponent.BindTo(this, ArrayObject.InstanceType.CombinedMesh);

#if UNITY_EDITOR
                // disable GI so unity doesn't complain about it.
                GameObjectUtility.SetStaticEditorFlags(_combinedObject, 0);
#endif
                var _mesh = new Mesh();
                _mesh.CombineMeshes(_combinedMesh.ToArray());
                _filter.sharedMesh = _mesh;
                _renderer.sharedMaterials = materials;

                if (generateCollider)
                {
                    var _collider = _combinedObject.AddIfNotPresent<MeshCollider>();
                    _collider.sharedMesh = _mesh;
                }

                arrayObject.combinedMeshObjects.Add(_combinedObject);
            }
        }

        /// <summary>
        /// Clears the hide flags of instances
        /// </summary>
        private void MakeArrayObjectsReal()
        {
            foreach (var _arrayObject in arrayObjects)
            {
                MakeReal(_arrayObject);
            }
        }

        /// <summary>
        /// Clears the hide flags of instances
        /// </summary>
        private static void MakeReal(ArrayObject _arrayObject)
        {
            foreach (var _instance in _arrayObject.instances)
            {
                if (_instance)
                {
                    _instance.hideFlags = HideFlags.None;
                }
            }

            foreach (var _instance in _arrayObject.combinedMeshObjects)
            {
                if (_instance)
                {
                    _instance.hideFlags = HideFlags.None;
                }
            }
        }

        private void OnDestroy()
        {
            if (!suspend)
            {
                Clear(SETUtil.SceneUtil.SmartDestroy);
            }
        }

        private void Generate(int objectIndex, ERMeshGen meshGen, DisposeMethod dispose)
        {
            
            var _arrayObject = arrayObjects[objectIndex];
            _arrayObject.amount = Mathf.Max(_arrayObject.amount, 0);

            if (_arrayObject.instances == null)
            {
                _arrayObject.instances = new List<GameObject>();
            }

            if (combineMeshes)
            {

                _arrayObject.Clear(dispose);

                if (_arrayObject.prefab)
                {
                    // generate few combined meshes:
                    Quaternion _addedRotation = GetAddedRotation(objectIndex);
                    var _positions = new List<Matrix4x4>(_arrayObject.amount);
                    var _meshF = _arrayObject.prefab.GetComponentInChildren<MeshFilter>();
                    var _mesh = _meshF.sharedMesh;
                    var _localMeshMatrix = _meshF.transform.localToWorldMatrix;

                    for (int i = 0; i < _arrayObject.amount; i++)
                    {
                        // calc position & orientation
                        Vector3 _position;
                        Quaternion _rotationQuat;
                        OrientationOf(meshGen, objectIndex, _addedRotation, i, out _position, out _rotationQuat);
                        _positions.Add(Matrix4x4.TRS(_position, _rotationQuat, Vector3.one) * _localMeshMatrix);
                    }
                    CombineMeshes(_arrayObject, dispose, _positions, _mesh, _arrayObject.prefab.GetComponentInChildren<Renderer>().sharedMaterials, _arrayObject.prefab.GetComponentInChildren<Collider>() != null);
                }
            }
            else
            {
                // generate individual instances:

                bool _amountDontMatch = _arrayObject.amount != _arrayObject.instances.Count;
                if (_amountDontMatch)
                {
                    // for positioning
                    Quaternion _addedRotation = GetAddedRotation(objectIndex);

                    _arrayObject.Clear(dispose);
                    GameObject _template = null;

                    for (int i = 0; i < _arrayObject.amount; i++)
                    {
                        // calc position & orientation
                        Vector3 _position;
                        Quaternion _rotationQuat;
                        OrientationOf(meshGen, objectIndex, _addedRotation, i, out _position, out _rotationQuat);

                        GameObject _instance;
                        if (!_template)
                        {
                            _instance = SETUtil.SceneUtil.Instantiate(_arrayObject.prefab, _position, _rotationQuat);
                            var _instanceComponent = _instance.AddComponent<ERArrayObjectInstance>();
                            _instanceComponent.BindTo(this, ArrayObject.InstanceType.ArrayElement);

#if UNITY_EDITOR
                            foreach (var _rend in _instance.GetComponentsInChildren<Renderer>())
                            {
                                // disable GI so unity doesn't complain about it.
                                GameObjectUtility.SetStaticEditorFlags(_rend.gameObject, 0);
                            }
#endif

                            _template = _instance;
                        }
                        else
                        {
                            _instance = SETUtil.SceneUtil.Instantiate(_template, _position, _rotationQuat);
                        }

                        _instance.transform.SetParent(this.transform, ignoreParentSize);
                        _instance.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                        _arrayObject.instances.Add(_instance);
                    }
                }
                else
                {
                    PositionInstances(objectIndex, meshGen);
                }
            }
        }

        private void OrientationOf(ERMeshGen meshGen, int objectIndex, Quaternion addedRotation, int elementIndex, out Vector3 position, out Quaternion rotation)
        {
            float _distance = (float)elementIndex * arrayObjects[objectIndex].length + arrayObjects[objectIndex].pathOffset;
            var orientation = meshGen.path.Evaluate(objectIndex, _distance + (arrayObjects[objectIndex].invert ? arrayObjects[objectIndex].length : 0f), arrayObjects[objectIndex].length * (arrayObjects[objectIndex].invert ? -1f : 1f));
            rotation = orientation.ToQuaternion() * addedRotation;
            position = orientation.position;
        }

        /// <summary>
        /// For positioning whenever generation was skipped
        /// </summary>
        private void PositionInstances(int objectIndex, ERMeshGen meshGen)
        {
            Quaternion _addedRotation = GetAddedRotation(objectIndex);

            for (int i = 0; i < arrayObjects[objectIndex].instances.Count; i++)
            {
                if (arrayObjects[objectIndex].instances[i])
                {
                    Vector3 _position;
                    Quaternion _rotationQuat;
                    OrientationOf(meshGen, objectIndex, _addedRotation, i, out _position, out _rotationQuat);

                    var _iTransform = arrayObjects[objectIndex].instances[i].transform;
                    _iTransform.position = _position;
                    _iTransform.rotation = _rotationQuat;
                }
            }
        }

        private Quaternion GetAddedRotation(int objectIndex)
        {
            return Quaternion.Euler(Vector3.up * arrayObjects[objectIndex].rotation);
        }

        ///<summary> 
        ///Calculate how many objects can fit the given path length 
        ///</summary>
        private static int CalculateFit(FitType fitType, float objectLength, ERPathTracer path, float predefinedLength)
        {
            //set the AMOUNT variable based on fit length
            if (fitType == FitType.FitPath)
            {
                var _fitLength = path.distanceRecord;
                int _fitAmount = (int)Mathf.Floor(_fitLength / objectLength);
                return _fitAmount;
            }

            if (fitType == FitType.FitLength)
            {
                int _fitAmount = (int)Mathf.Floor(predefinedLength / objectLength);
                return _fitAmount;
            }

            return 0;
        }

        [Obsolete("Consider using " + nameof(ERMeshGen) + "." + nameof(ERMeshGen.RuntimeFinalize), false)]
        public void DestroyDontClear()
        {
            MakeArrayObjectsReal();
            SETUtil.SceneUtil.SmartDestroy(this);
        }

        [Obsolete]
        public void SubToPath(ERPathTracer path) { suspend = false; }

        [Obsolete]
        public void UnsubFromPath() { suspend = true; }

        public float GetAutoLength(int objectIndex)
        {
            float output = 0f;
            GameObject _prefab = arrayObjects[objectIndex].prefab;
            if (_prefab)
            {
                MeshFilter _meshFilter = _prefab.GetComponent<MeshFilter>();

            outp:
                if (_meshFilter)
                {
                    output = _meshFilter.sharedMesh.bounds.size.z * 2f;
                }
                else
                {
                    foreach (Transform child in _prefab.transform)
                    {
                        MeshFilter _meshFilterInChild = child.GetComponent<MeshFilter>();
                        if (_meshFilterInChild)
                        {
                            _meshFilter = _meshFilterInChild;
                            goto outp;
                        }
                    }
                }
            }

            return output;
        }

        public override void Init(ERMeshGen meshGen, DisposeMethod dispose)
        {
            base.Init(meshGen, dispose);
            VersionMigrate(meshGen);
        }

        public override void UpdateState(ERMeshGen meshGen, DisposeMethod dispose)
        {
            var _path = meshGen.path;

            if (!_path || suspend)
            {
                return;
            }

            if (!enabled)
            {
                Clear(dispose);
                return;
            }

            if (arrayObjects != null)
            {
                for (int i = 0; i < arrayObjects.Count; i++)
                {
                    var arrayObject = arrayObjects[i];
                    arrayObject.FindExistingInstances(this, dispose);

                    _path.TracePath(i, arrayObject.horizontalOffset, arrayObject.verticalOffset);

                    if (arrayObject.fitType != FitType.FixedAmount)
                        arrayObject.amount = CalculateFit(arrayObject.fitType, arrayObject.length, _path, length);

                    Generate(i, meshGen, dispose);
                }
            }
        }

        public override void Clear(DisposeMethod dispose)
        {
            foreach (var arrayObject in arrayObjects)
            {
                arrayObject.Clear(dispose);
            }
        }

        public override void OnFinalize(ERMeshGen meshGen, DisposeMethod dispose, AssetCreationMethod assetCreation = null)
{
            MakeArrayObjectsReal();

            // export assets
            foreach (var _arrayObject in arrayObjects)
            {
                _arrayObject.ExportMeshAssets(assetCreation);
            }

            Clear(dispose: (o) => { });
            Util.Dirtify(this);
        }
    }
}
