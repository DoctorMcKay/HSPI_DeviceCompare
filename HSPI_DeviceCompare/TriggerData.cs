using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using HomeSeerAPI;

namespace HSPI_DeviceCompare
{
	public class TriggerData
	{
		public TriggerType Type { get; set; }
		public int DevRefLeft { get; set; }
		public int DevRefRight { get; set; }
		public TriggerComp Comparison { get; set; }

		public TriggerData() {
			Type = TriggerType.DeviceValueSet;
			DevRefLeft = 0;
			DevRefRight = 0;
			Comparison = TriggerComp.LessThan;
		}

		public bool IsConfigured() {
			return DevRefLeft != 0 && DevRefRight != 0;
		}

		public bool EvaluateTrigger(Enums.HSEvent eventType, double valueLeft, double valueRight) {
			if (Type == TriggerType.DeviceValueChanged && eventType != Enums.HSEvent.VALUE_CHANGE) {
				// We don't need to check DeviceValueSet because it also covers changed cases
				return false;
			}

			return EvaluateCondition(valueLeft, valueRight);
		}

		public bool EvaluateCondition(double valueLeft, double valueRight) {
			switch (Comparison) {
				case TriggerComp.LessThan:
					return valueLeft < valueRight;
				
				case TriggerComp.LessThanOrEqual:
					return valueLeft < valueRight || doubleEqual(valueLeft, valueRight);
				
				case TriggerComp.Equal:
					return doubleEqual(valueLeft, valueRight);
				
				case TriggerComp.GreaterThanOrEqual:
					return valueLeft > valueRight || doubleEqual(valueLeft, valueRight);
				
				case TriggerComp.GreaterThan:
					return valueLeft > valueRight;
				
				case TriggerComp.NotEqual:
					return !doubleEqual(valueLeft, valueRight);
				
				default:
					return false;
			}
		}

		public byte[] Serialize() {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write((byte) 1); // version
			writer.Write((byte) Type);
			writer.Write(DevRefLeft);
			writer.Write(DevRefRight);
			writer.Write((byte) Comparison);

			byte[] output = stream.ToArray();
			writer.Dispose();
			stream.Dispose();
			return output;
		}

		public static TriggerData Unserialize(byte[] input) {
			TriggerData data = new TriggerData();
			if (input == null || input.Length != 11) {
				return data;
			}
			
			MemoryStream stream = new MemoryStream(input);
			BinaryReader reader = new BinaryReader(stream);

			byte version = reader.ReadByte();
			if (version != 1) {
				throw new Exception("Bad data version " + version);
			}
			
			data.Type = (TriggerType) reader.ReadByte();
			data.DevRefLeft = reader.ReadInt32();
			data.DevRefRight = reader.ReadInt32();
			data.Comparison = (TriggerComp) reader.ReadByte();
			
			reader.Dispose();
			stream.Dispose();
			return data;
		}

		public override string ToString() {
			return "Type = " + Type + "; " + DevRefLeft + " " + Comparison + " " + DevRefRight;
		}

		private bool doubleEqual(double left, double right) {
			return Math.Abs(left - right) < 0.0000001;
		}
	}

	public struct TriggerEntry
	{
		public TriggerData Data;
		public IPlugInAPI.strTrigActInfo TrigInfo;
	}

	public enum TriggerType
	{
		DeviceValueSet = 1,
		DeviceValueChanged = 2,
	}

	public enum TriggerComp
	{
		LessThan = 1,
		LessThanOrEqual = 2,
		Equal = 3,
		GreaterThanOrEqual = 4,
		GreaterThan = 5,
		NotEqual = 6,
	}
}
