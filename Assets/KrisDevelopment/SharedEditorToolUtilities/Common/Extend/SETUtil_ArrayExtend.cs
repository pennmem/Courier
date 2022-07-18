////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System.Collections.Generic;
using UnityEngine;

namespace SETUtil.Common.Extend
{
	public static class ArrayExtend
	{
		public static int IndexOf<T> (this T[] array, System.Func<T, bool> predicate)
		{
			for(int i = 0; i < array.Length; i++){
				if(predicate.Invoke(array[i])){
					return i;
				}
			}

			return -1;
		}
		
		/// <summary>
		/// Split input collection into chunks of a given size
		/// </summary>
		public static List<T[]> Split<T>(this T[] targets, int chunkSize)
		{
			var _output = new List<T[]>();

			var _n = Mathf.FloorToInt((float) targets.Length / chunkSize);

			for (int i = 0; i < _n; i++) {
				// full chunk
				var _chunk = new T[chunkSize];
				for (int j = 0; j < _chunk.Length; j++) {
					_chunk[j] = targets[i * chunkSize + j];
				}

				_output.Add(_chunk);
			}

			{
				// remaining chunk
				var _remain = targets.Length % chunkSize;
				if (_remain != 0) {
					var _chunk = new T[_remain];

					for (int j = 0; j < _chunk.Length; j++) {
						_chunk[j] = targets[_n * chunkSize + j];
					}

					_output.Add(_chunk);
				}
			}
			return _output;
		}
	}
}