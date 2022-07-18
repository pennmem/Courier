using UnityEngine;

namespace KrisDevelopment.ERMG
{
	public class SnapPointToPositionExample : MonoBehaviour
	{
		public Transform pointTransform;
		public ERMeshGen meshGen;

		void OnDrawGizmos()
		{
			if (pointTransform && meshGen)
			{
				Gizmos.DrawWireSphere(meshGen.SnapPointToPath(pointTransform.position), 1f);
			}
		}
	}
}
