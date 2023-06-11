using System;
using System.Collections.Generic;
using System.Text;

namespace Byond.TopicSender
{
	/// <summary>
	/// Represents a topic response from BYOND.
	/// </summary>
	public sealed class TopicResponse : TopicResponseHeader
	{
		/// <summary>
		/// The <see cref="TopicResponseType"/> of the header.
		/// </summary>
		public TopicResponseType ResponseType { get; }

		/// <summary>
		/// The returned <see cref="string"/> if decodable.
		/// </summary>
		public string? StringData { get; }

		/// <summary>
		/// The returned <see cref="float"/> if decodable.
		/// </summary>
		public float? FloatData { get; }

		/// <summary>
		/// The raw <see cref="byte"/>s returned from the topic call.
		/// </summary>
		public IReadOnlyCollection<byte> RawData => rawData;

		/// <summary>
		/// Backing field for <see cref="RawData"/>.
		/// </summary>
		readonly byte[] rawData;

		/// <summary>
		/// Initializes a new instance of the <see cref="TopicResponse"/> class.
		/// </summary>
		/// <param name="rawData">The value of <see cref="RawData"/>.</param>
		public TopicResponse(byte[] rawData)
			: base(rawData ?? throw new ArgumentNullException(nameof(rawData)))
		{
			this.rawData = rawData;

			if (PacketLength >= 1)
			{
				const byte StringResponse = 0x06;
				const byte FloatResponse = 0x2a;

				var responseType = rawData[4];
				if (responseType == StringResponse)
					ResponseType = TopicResponseType.StringResponse;
				if (responseType == FloatResponse && PacketLength >= 5)
					ResponseType = TopicResponseType.FloatResponse;
			}

			switch (ResponseType)
			{
				case TopicResponseType.StringResponse:
					var stringStartIndex = HeaderLength + 1;
					var stringEndIndex = PacketLength - 1;
					StringData = Encoding
						.UTF8
						.GetString(rawData[stringStartIndex..stringEndIndex])
						.TrimEnd((char)0);
					break;
				case TopicResponseType.FloatResponse:
					var floatBytes = new byte[4];

					var lilEndy = BitConverter.IsLittleEndian;
					floatBytes[lilEndy ? 0 : 3] = rawData[5];
					floatBytes[lilEndy ? 1 : 2] = rawData[6];
					floatBytes[lilEndy ? 2 : 1] = rawData[7];
					floatBytes[lilEndy ? 3 : 0] = rawData[8];

					FloatData = BitConverter.ToSingle(floatBytes);

					break;
				case TopicResponseType.UnknownResponse:
					break;
				default:
					throw new InvalidOperationException($"Invalid value for ResponseType: {ResponseType}");
			}
		}
	}
}
