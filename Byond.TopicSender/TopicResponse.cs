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

			switch (ResponseType)
			{
				case TopicResponseType.StringResponse:
					if (!PacketLength.HasValue)
						throw new InvalidOperationException("Expected header content length to have a value!");

					var stringLength = PacketLength.Value - 1;
					StringData = Encoding
						.UTF8
						.GetString(rawData[HeaderLength..stringLength])
						.TrimEnd((char)0);
					break;
				case TopicResponseType.FloatResponse:
					if (PacketLength < 4)
						return;

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
