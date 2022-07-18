using KrisDevelopment.ERMG;
using UnityEngine;

public class FinalizeExample : MonoBehaviour
{
    [SerializeField] private ERMeshGen meshGen;

    [ContextMenu("Finalize")]
    void Run()
    {
        meshGen.GetComponent<EasyRoadsMeshGen_Array>().suspend = false;
        meshGen.UpdateMesh();
        meshGen.RuntimeFinalize(Destroy);
    }
}
