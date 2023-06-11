using System;

namespace Byond.TopicSender
{
	/// <summary>
	/// Represents the header data of a <see cref="TopicResponse"/>.
	/// </summary>
	public class TopicResponseHeader
	{
		/// <summary>
		/// The <see cref="Array.Length"/> of a topic response header.
		/// </summary>
		public const int HeaderLength = 4;

		/// <summary>
		/// The length of the content in the header.
		/// </summary>
		public ushort PacketLength { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TopicResponseHeader"/> class.
		/// </summary>
		/// <param name="data">The header <see cref="byte"/>s.</param>
		public TopicResponseHeader(ReadOnlySpan<byte> data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			if (data.Length < HeaderLength || data[1] != 0x83)
				return;

			var receiveLengthBytes = new byte[sizeof(ushort)];
			var lilEndy = BitConverter.IsLittleEndian;

			receiveLengthBytes[lilEndy ? 1 : 0] = data[2];
			receiveLengthBytes[lilEndy ? 0 : 1] = data[3];

			var intContentLength = Math.Max(BitConverter.ToUInt16(receiveLengthBytes) + HeaderLength, 0);
			if (intContentLength <= UInt16.MaxValue)
				PacketLength = (ushort)intContentLength;
		}
	}
}
