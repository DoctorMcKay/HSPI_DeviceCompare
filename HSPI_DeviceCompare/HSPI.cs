using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using HomeSeerAPI;
using Scheduler;
using Scheduler.Classes;

namespace HSPI_DeviceCompare
{
	// ReSharper disable once InconsistentNaming
	public class HSPI : HspiBase
	{
		public const string PLUGIN_NAME = "DeviceCompare";
		
		private List<DeviceData> devices;
		private List<TriggerEntry> triggerCache = null;
		private long lastTriggerCacheTime = 0;
		
		public HSPI() {
			Name = PLUGIN_NAME;
			PluginIsFree = true;
			TriggerCount = 1;
		}

		public override string InitIO(string port) {
			Program.WriteLog(LogType.Verbose, "InitIO");

			callbacks.RegisterEventCB(Enums.HSEvent.CONFIG_CHANGE, Name, InstanceFriendlyName());
			callbacks.RegisterEventCB(Enums.HSEvent.VALUE_SET, Name, InstanceFriendlyName());
			callbacks.RegisterEventCB(Enums.HSEvent.VALUE_CHANGE, Name, InstanceFriendlyName());
			cacheDeviceList();
			
			return "";
		}

		public override void HSEvent(Enums.HSEvent eventType, object[] parameters) {
			switch (eventType) {
				case Enums.HSEvent.CONFIG_CHANGE:
					int type = (int) parameters[1];
					int dac = (int) parameters[4];
					Program.WriteLog(LogType.Verbose, "HSEvent triggered " + eventType + " type " + type + " DAC " + dac);
					if (type == 0 && (dac == 1 || dac == 2)) {
						// Device was added or deleted
						cacheDeviceList();
					}
					
					break;
				
				case Enums.HSEvent.VALUE_SET:
				case Enums.HSEvent.VALUE_CHANGE:
					int devRef = (int) parameters[4];
					Program.WriteLog(LogType.Verbose, "HSEvent triggered " + eventType + " for dev ref " + devRef);
					
					// Do any of our triggers reference this device?
					foreach (TriggerEntry trig in getTriggerList()) {
						if (trig.Data.DevRefLeft == devRef || trig.Data.DevRefRight == devRef) {
							// Yep.
							double valueLeft = hs.DeviceValueEx(trig.Data.DevRefLeft);
							double valueRight = hs.DeviceValueEx(trig.Data.DevRefRight);
							if (trig.Data.EvaluateTrigger(eventType, valueLeft, valueRight)) {
								Program.WriteLog(LogType.Info,
									"Triggering event " + trig.TrigInfo.evRef + " because " + trig.Data.Type + " and " +
									valueLeft + " " + trig.Data.Comparison + " " + valueRight);
								callbacks.TriggerFire(Name, trig.TrigInfo);
							}
						}
					}

					break;
				
				default:
					Program.WriteLog(LogType.Verbose, "HSEvent triggered " + eventType);
					break;
			}
		}

		public override bool get_HasConditions(int triggerNumber) {
			return true; // all our triggers can be conditions
		}

		public override string get_TriggerName(int triggerNumber) {
			if (triggerNumber != 1) {
				return "Unknown trigger " + triggerNumber;
			}

			return "A Device's Value Compares With Another...";
		}

		public override string TriggerBuildUI(string unique, IPlugInAPI.strTrigActInfo trigInfo) {
			StringBuilder sb = new StringBuilder();
			TriggerData trig = TriggerData.Unserialize(trigInfo.DataIn);
			clsJQuery.jqDropList dropList;
			
			Program.WriteLog(LogType.Console, "Building UI for " + trig);

			if (!Condition) {
				dropList = new clsJQuery.jqDropList("TrigType" + unique, "events", true);
				dropList.AddItem("This device's value was set:", ((byte) TriggerType.DeviceValueSet).ToString(),
					trig.Type == TriggerType.DeviceValueSet);
				dropList.AddItem("This device's value changed:", ((byte) TriggerType.DeviceValueChanged).ToString(),
					trig.Type == TriggerType.DeviceValueChanged);
				sb.Append(dropList.Build());
			}

			dropList = new clsJQuery.jqDropList("DeviceLeft" + unique, "events", true);
			dropList.AddItem("(Select A Device)", "0", trig.DevRefLeft == 0);
			foreach (DeviceData device in devices) {
				dropList.AddItem(device.Name, device.DevRef.ToString(), trig.DevRefLeft == device.DevRef);
			}

			sb.Append(dropList.Build());
			if (!Condition) {
				sb.Append("<br />And it is ");
			} else {
				sb.Append(" is ");
			}

			dropList = new clsJQuery.jqDropList("CompOperator" + unique, "events", true);
			dropList.AddItem("less than", ((int) TriggerComp.LessThan).ToString(), trig.Comparison == TriggerComp.LessThan);
			dropList.AddItem("less than or equal to", ((int) TriggerComp.LessThanOrEqual).ToString(),
				trig.Comparison == TriggerComp.LessThanOrEqual);
			dropList.AddItem("equal to", ((int) TriggerComp.Equal).ToString(), trig.Comparison == TriggerComp.Equal);
			dropList.AddItem("greater than", ((int) TriggerComp.GreaterThan).ToString(),
				trig.Comparison == TriggerComp.GreaterThan);
			dropList.AddItem("greater than or equal to", ((int) TriggerComp.GreaterThanOrEqual).ToString(),
				trig.Comparison == TriggerComp.GreaterThanOrEqual);
			dropList.AddItem("not equal to", ((int) TriggerComp.NotEqual).ToString(), trig.Comparison == TriggerComp.NotEqual);
			sb.Append(dropList.Build());

			if (Condition) {
				sb.Append("<br />");
			}
			
			sb.Append(" the value of: ");

			dropList = new clsJQuery.jqDropList("DeviceRight" + unique, "events", true);
			dropList.AddItem("(Select A Device)", "0", trig.DevRefRight == 0);
			foreach (DeviceData device in devices) {
				dropList.AddItem(device.Name, device.DevRef.ToString(), trig.DevRefRight == device.DevRef);
			}

			sb.Append(dropList.Build());
			return sb.ToString();
		}

		public override IPlugInAPI.strMultiReturn TriggerProcessPostUI(NameValueCollection postData, IPlugInAPI.strTrigActInfo trigInfo) {
			if (trigInfo.TANumber != 1) {
				throw new Exception("Unknown trigger number " + trigInfo.TANumber);
			}
			
			IPlugInAPI.strMultiReturn ret = new IPlugInAPI.strMultiReturn();
			ret.TrigActInfo = trigInfo;
			ret.DataOut = trigInfo.DataIn;
			
			foreach (string key in postData.AllKeys) {
				string[] parts = key.Split('_');
				if (parts.Length > 1) {
					postData.Add(parts[0], postData.Get(key));
					Program.WriteLog(LogType.Console, parts[0] + " set to " + postData.Get(key));
				}
			}
			
			TriggerData trig = TriggerData.Unserialize(trigInfo.DataIn);

			int tempInt;
			string tempStr;
			
			if ((tempStr = postData.Get("TrigType")) != null && int.TryParse(tempStr, out tempInt)) {
				trig.Type = (TriggerType) tempInt;
			}

			if ((tempStr = postData.Get("DeviceLeft")) != null && int.TryParse(tempStr, out tempInt)) {
				trig.DevRefLeft = tempInt;
			}

			if ((tempStr = postData.Get("DeviceRight")) != null && int.TryParse(tempStr, out tempInt)) {
				trig.DevRefRight = tempInt;
			}

			if ((tempStr = postData.Get("CompOperator")) != null && int.TryParse(tempStr, out tempInt)) {
				trig.Comparison = (TriggerComp) tempInt;
			}

			Program.WriteLog(LogType.Console, "Returning " + trig);
			ret.DataOut = trig.Serialize();
			return ret;
		}

		public override string TriggerFormatUI(IPlugInAPI.strTrigActInfo trigInfo) {
			if (trigInfo.TANumber != 1) {
				return "Unknown trigger number " + trigInfo.TANumber;
			}
			
			TriggerData trig = TriggerData.Unserialize(trigInfo.DataIn);
			Program.WriteLog(LogType.Console, "Formatting UI for " + trig);
			StringBuilder sb = new StringBuilder();

			DeviceClass devLeft = (DeviceClass) hs.GetDeviceByRef(trig.DevRefLeft);
			DeviceClass devRight = (DeviceClass) hs.GetDeviceByRef(trig.DevRefRight);

			sb.Append("The value of ");
			sb.Append("<span class=\"event_Txt_Option\">");
			sb.Append(devLeft.get_Location2(hs) + " " + devLeft.get_Location(hs) + " " + devLeft.get_Name(hs));
			sb.Append("</span> ");

			if (!Condition) {
				sb.Append("<span class=\"event_Txt_Selection\">");
				
				switch (trig.Type) {
					case TriggerType.DeviceValueSet:
						sb.Append("was set");
						break;

					case TriggerType.DeviceValueChanged:
						sb.Append("changed");
						break;
				}

				sb.Append("</span> and ");
			}
			
			sb.Append("is <span class=\"event_Txt_Selection\">");
			switch (trig.Comparison) {
				case TriggerComp.LessThan:
					sb.Append("less than");
					break;
				
				case TriggerComp.LessThanOrEqual:
					sb.Append("less than or equal to");
					break;
				
				case TriggerComp.Equal:
					sb.Append("equal to");
					break;
				
				case TriggerComp.GreaterThan:
					sb.Append("greater than");
					break;
				
				case TriggerComp.GreaterThanOrEqual:
					sb.Append("greater than or equal to");
					break;
				
				case TriggerComp.NotEqual:
					sb.Append("not equal to");
					break;
			}

			sb.Append("</span> the value of <span class=\"event_Txt_Option\">");
			sb.Append(devRight.get_Location2(hs) + " " + devRight.get_Location(hs) + " " + devRight.get_Name(hs));
			sb.Append("</span>");

			return sb.ToString();
		}

		public override bool get_TriggerConfigured(IPlugInAPI.strTrigActInfo trigInfo) {
			return TriggerData.Unserialize(trigInfo.DataIn).IsConfigured();
		}

		public override bool TriggerReferencesDevice(IPlugInAPI.strTrigActInfo trigInfo, int devRef) {
			TriggerData trig = TriggerData.Unserialize(trigInfo.DataIn);
			return devRef == trig.DevRefLeft || devRef == trig.DevRefRight;
		}

		public override bool TriggerTrue(IPlugInAPI.strTrigActInfo trigInfo) {
			TriggerData trig = TriggerData.Unserialize(trigInfo.DataIn);
			double valueLeft = hs.DeviceValueEx(trig.DevRefLeft);
			double valueRight = hs.DeviceValueEx(trig.DevRefRight);
			if (trig.EvaluateCondition(valueLeft, valueRight)) {
				Program.WriteLog(LogType.Info,
					"Passing condition for event " + trigInfo.evRef + " because " + valueLeft + " " + trig.Comparison +
					" " + valueRight);
				return true;
			}
			
			return false;
		}

		private void cacheDeviceList() {
			Program.WriteLog(LogType.Debug, "Caching device list");
			List<DeviceData> devices = new List<DeviceData>();
			clsDeviceEnumeration enumerator = (clsDeviceEnumeration) hs.GetDeviceEnumerator();
			do {
				DeviceClass device = enumerator.GetNext();
				if (device != null) {
					devices.Add(new DeviceData {
						DevRef = device.get_Ref(hs),
						Name = device.get_Location2(hs) + " " + device.get_Location(hs) + " " + device.get_Name(hs),
					});
				}
			} while (!enumerator.Finished);

			Program.WriteLog(LogType.Debug, "Device list cached successfully");
			this.devices = devices.OrderBy(d => d.Name).ToList();
		}

		private List<TriggerEntry> getTriggerList() {
			if (triggerCache != null && Time.UnixTimeSeconds() - lastTriggerCacheTime < 30) {
				return triggerCache;
			}
			
			long startTime = Time.UnixTimeMilliseconds();
			List<TriggerEntry> list = new List<TriggerEntry>();

			foreach (IPlugInAPI.strTrigActInfo trig in callbacks.TriggerMatches(Name, 1, -1)) {
				TriggerData data = TriggerData.Unserialize(trig.DataIn);
				if (data.IsConfigured()) {
					list.Add(new TriggerEntry {
						Data = data,
						TrigInfo = trig,
					});
				}
			}
			
			Program.WriteLog(LogType.Debug, "Refreshed trigger list in " + (Time.UnixTimeMilliseconds() - startTime) + " ms");

			lastTriggerCacheTime = Time.UnixTimeSeconds();
			triggerCache = list;			
			return list;
		}
	}

	struct DeviceData
	{
		public int DevRef;
		public string Name;
	}
}
