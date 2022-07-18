using UnityEngine;

namespace KrisDevelopment.ERMG
{
	[
		AddComponentMenu("Easy Roads Mesh Gen/Extensions/Path Tracer"),
		ExecuteAlways,
		RequireComponent(typeof(ERMeshGen))
	]
	public class ERPathTracer : MonoBehaviour
	{
		public ERMeshGen meshGen = null;

		public float distanceRecord { get; private set; }

		private PointData[][] pathGroup;

		//accessors:
		public bool validLinking
		{
			get
			{
				//add reference conditions here
				return (meshGen != null);
			}
		}
		//--

		private void CalculateDistances(ref PointData[] points)
		{
			if (points == null)
				return;

			if (points.Length > 0)
				points[0].distance = 0f;

			float _distance = 0f;

			if (points.Length > 1)
			{
				for (int i = 1; i < points.Length; i++)
				{
					_distance += Vector3.Distance(points[i].position, points[i - 1].position);
					points[i].distance = _distance;
				}
			}

			distanceRecord = _distance;
		}

		private OrientationData OrientationDataAtDistance(int pathIndex, float distance)
		{
			int _pointIndex = 0;

			for (int i = 0; i < pathGroup[pathIndex].Length - 1; i++)
				if (pathGroup[pathIndex][i].distance < distance)
				{
					_pointIndex = i;
				}
				else break;

			var _p1 = pathGroup[pathIndex][_pointIndex];
			var _p2 = pathGroup[pathIndex][_pointIndex + 1];

			float
				_distanceInterval = (_p2.distance - _p1.distance),
				_lerp = (distance - _p1.distance) / _distanceInterval;

			OrientationData _o = new OrientationData(
				Vector3.Lerp(_p1.position, _p2.position, _lerp),
				_p1.tangentVector,
				Vector3.Lerp(_p1.normalVector, _p2.normalVector, _lerp));
			return _o;
		}

		public void TracePath(int pathIndex, float horizontalOffset, float verticalOffset)
		{
			if (!validLinking)
			{
				Debug.LogError("[ERPathTracer.TracePath ERROR] No path source!");
				return;
			}

			//perform initialization if needed
			if (pathGroup == null)
				pathGroup = new PointData[pathIndex][];
				
			if (pathGroup.Length <= pathIndex)
			{
				SETUtil.Deprecated.ArrUtil.Resize<PointData[]>(ref pathGroup, pathIndex - pathGroup.Length + 1);
			}

			pathGroup[pathIndex] = meshGen.GetOrientedPathPoints(horizontalOffset, verticalOffset).ToPointDataArray();
			CalculateDistances(ref pathGroup[pathIndex]);
		}

		public OrientationData Evaluate(int pathIndex, float distance, float elementLength)
		{
			OrientationData _o = new OrientationData(transform.position + Vector3.forward * distance, Vector3.forward, Vector3.up);

			if (pathGroup == null || pathGroup.Length < pathIndex + 1 || pathGroup[pathIndex] == null)
			{
				Debug.LogError("[ERPathTracer.Evaluate ERROR] no path at index " + pathIndex + ". Make sure you trace the path first!");
				return _o;
			}

			//if there is just a single point
			if (pathGroup[pathIndex].Length == 1)
			{
				_o = new OrientationData(transform.position + pathGroup[pathIndex][0].tangentVector * distance, pathGroup[pathIndex][0].tangentVector, pathGroup[pathIndex][0].normalVector);
			}
			else if (pathGroup[pathIndex].Length > 1)
			{ //if there is more than one point
				if (elementLength != 0)
				{
					OrientationData
						_pointA = OrientationDataAtDistance(pathIndex, distance),
						_pointB = OrientationDataAtDistance(pathIndex, distance + elementLength);
					_o.Set(_pointA.position, (_pointB.position - _pointA.position).normalized, _pointA.normalVector);
                }
                else
				{
					var _pointA = OrientationDataAtDistance(pathIndex, distance);
					_o.Set(_pointA.position, _pointA.tangentVector, _pointA.normalVector);
				}
			}

			return _o;
		}
	}
}
