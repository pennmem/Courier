using UnityEngine;

namespace KrisDevelopment.ERMG
{
    /// <summary>
    /// Every extension to the ER Mesh Gen system implements this interface in order to recieve updates.
    /// </summary>
    [RequireComponent(typeof(ERMeshGen))]
    public abstract class MeshGenExtensionComponent : MonoBehaviour
    {
        private ERMeshGen meshGen;

        protected virtual void OnValidate ()
        {
            if(meshGen == null)
            {
                meshGen = GetComponent<ERMeshGen>();
            }
        }

        internal void RequestUpdate()
        {
            meshGen.RequestUpdate();
        }

        public virtual void Init(ERMeshGen meshGen, DisposeMethod dispose)
        {
            this.meshGen = meshGen;
        }

        public abstract void UpdateState(ERMeshGen meshGen, DisposeMethod dispose);

        public abstract void OnFinalize(ERMeshGen meshGen, DisposeMethod dispose, AssetCreationMethod assetCreation = null);

        public abstract void Clear(DisposeMethod dispose);

    }
}