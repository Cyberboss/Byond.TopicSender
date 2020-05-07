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
		public static readonly int HeaderLength = 5;

		/// <summary>
		/// The <see cref="TopicResponseType"/> of the header.
		/// </summary>
		public TopicResponseType ResponseType { get; }

		/// <summary>
		/// The length of the content in the header.
		/// </summary>
		public ushort? ContentLength { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TopicResponseHeader"/> <see langword="class"/>.
		/// </summary>
		/// <param name="data">The header <see cref="byte"/>s.</param>
		public TopicResponseHeader(ReadOnlySpan<byte> data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			if (data.Length < HeaderLength - sizeof(ushort))
				return;

			var receiveLengthBytes = new byte[sizeof(ushort)];
			var lilEndy = BitConverter.IsLittleEndian;

			receiveLengthBytes[lilEndy ? 1 : 0] = data[2];
			receiveLengthBytes[lilEndy ? 0 : 1] = data[3];

			var intContentLength = Math.Max(BitConverter.ToUInt16(receiveLengthBytes) + HeaderLength, 0);
			if (intContentLength > UInt16.MaxValue)
				ContentLength = 0;
			else
				ContentLength = (ushort)intContentLength;

			if (data.Length < HeaderLength)
				return;

			const byte StringResponse = 0x06;
			const byte FloatResponse = 0x2a;
			switch (data[4])
			{
				case StringResponse:
					ResponseType = TopicResponseType.StringResponse;
					break;
				case FloatResponse:
					ResponseType = TopicResponseType.FloatResponse;
					break;
			}
		}
	}
}
