using System;

namespace HSPI_DeviceCompare
{
	public class Time
	{
		public static long UnixTimeSeconds() {
			return (long) getTimeSpan().TotalSeconds;
		}

		public static long UnixTimeMilliseconds() {
			return (long) getTimeSpan().TotalMilliseconds;
		}

		private static TimeSpan getTimeSpan() {
			return DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
		}
	}
}
