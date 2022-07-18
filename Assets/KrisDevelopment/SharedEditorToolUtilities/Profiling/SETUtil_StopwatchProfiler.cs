////////////////////////////////////////
//    Shared Editor Tool Utilities    //
//    by Kris Development             //
////////////////////////////////////////

//License: MIT
//GitLab: https://gitlab.com/KrisDevelopment/SETUtil

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using U = UnityEngine;

namespace SETUtil.Profiling
{
	public class StopwatchProfiler : IDisposable
	{
		private struct Record
		{
			public TimeSpan timeSinceStart;
			public TimeSpan timeSinceLast;
			public string label;

			public override string ToString()
			{
				return string.Format("[{0}: at- {1}, span- {2}]", label, timeSinceStart, timeSinceLast);
			}
		}

		private string name;
		private Stopwatch stopWatch;
		private List<Record> times = new List<Record>();
		private TimeSpan timeOfLastTick;

		
		public StopwatchProfiler(string name = null)
		{
			this.name = name ?? "SETUtil";
			stopWatch = new Stopwatch();
			stopWatch.Start();
			timeOfLastTick = TimeSpan.Zero;
		}

		public void Time(string label)
		{
			var _elapsed = stopWatch.Elapsed;
			times.Add(new Record()
			{
				timeSinceStart = _elapsed,
				timeSinceLast = _elapsed - timeOfLastTick,
				label = label,
			});
			timeOfLastTick = _elapsed;
		}

		public TimeSpan CurrentTime ()
		{
			return stopWatch.Elapsed;
		}

		public void Dispose()
		{
			stopWatch.Stop();
			Time("END");

			// OUTPUT:
			var _elapsedTotal = stopWatch.Elapsed;

			var _log = new StringBuilder();
			_log.AppendLine(string.Format("{0} Stopwatch results", name));
			foreach (var _time in times)
			{
				_log.AppendLine(_time.ToString());
			}
			_log.AppendLine(string.Format("Total: {0}", _elapsedTotal));

			U.Debug.Log(_log);
		}
	}
}