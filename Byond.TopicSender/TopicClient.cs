using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Byond.TopicSender
{
	/// <inheritdoc />
	public sealed class TopicClient : ITopicClient
	{
		/// <summary>
		/// The <see cref="SocketParameters"/> for the <see cref="TopicClient"/>.
		/// </summary>
		readonly SocketParameters socketParameters;

		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="TopicClient"/>.
		/// </summary>
		readonly ILogger logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="TopicClient"/> class.
		/// </summary>
		/// <param name="socketParameters">The <see cref="SocketParameters"/> to use.</param>
		/// <param name="logger">The optional <see cref="ILogger"/> to use.</param>
		public TopicClient(SocketParameters socketParameters, ILogger? logger = null)
		{
			this.socketParameters = socketParameters ?? throw new ArgumentNullException(nameof(socketParameters));
			this.logger = logger ?? new NullLogger<TopicClient>();
		}

		/// <inheritdoc />
		public async Task<TopicResponse> SendTopic(string destinationServer, string queryString, ushort port, CancellationToken cancellationToken = default)
		{
			if (destinationServer == null)
				throw new ArgumentNullException(nameof(destinationServer));
			var hostEntries = await Dns.GetHostAddressesAsync(destinationServer, cancellationToken).ConfigureAwait(false);

			// pick the first IPV4 entry
			return await SendTopic(hostEntries.First(x => x.AddressFamily == AddressFamily.InterNetwork), queryString, port, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task<TopicResponse> SendTopic(IPAddress address, string queryString, ushort port, CancellationToken cancellationToken = default)
		{
			if (address == null)
				throw new ArgumentNullException(nameof(address));
			return SendTopic(new IPEndPoint(address, port), queryString, cancellationToken);
		}

		/// <inheritdoc />
		public async Task<TopicResponse> SendTopic(IPEndPoint endPoint, string queryString, CancellationToken cancellationToken = default)
		{
			if (endPoint == null)
				throw new ArgumentNullException(nameof(endPoint));
			if (queryString == null)
				throw new ArgumentNullException(nameof(queryString));

			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			{
				// prepare
				var needsQueryToken = queryString.Length == 0 || queryString[0] != '?';

				var queryStringByteLength = Encoding.UTF8.GetByteCount(queryString);
				if (needsQueryToken)
					++queryStringByteLength;

				const int ZerosPaddingCount = 5;

				const int BytesBeforeString = 1 // padding
					+ 1 // signature
					+ 2 // length header
					+ ZerosPaddingCount;

				var totalLength = BytesBeforeString
					+ queryStringByteLength
					+ 1; // null terminator

				var lengthHeader = totalLength - 4;
				if (lengthHeader > UInt16.MaxValue)
					throw new ArgumentOutOfRangeException(nameof(queryString), queryString, "Topic too long!");

				await using var dataStream = new MemoryStream(totalLength);
				await using (var writer = new BinaryWriter(dataStream, Encoding.UTF8, true))
				{
					writer.Write((byte)0);
					writer.Write((byte)0x83);

					// #RIP-lilEndy
					writer.Write(BinaryPrimitives.ReverseEndianness((ushort)lengthHeader));

					for (var i = 0; i < ZerosPaddingCount; ++i)
						writer.Write((byte)0);

					if (queryString.Length == 0 || queryString[0] != '?')
						writer.Write('?');

					writer.Seek(queryStringByteLength, SeekOrigin.Current);
					writer.Write((byte)0);
				}

				var sendBuffer = dataStream.GetBuffer();
				Encoding.UTF8.GetBytes(queryString, 0, queryString.Length, sendBuffer, BytesBeforeString);

				// connect
				using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				connectCts.CancelAfter(socketParameters.ConnectTimeout);
				await socket.ConnectAsync(endPoint, connectCts.Token);

				// send
				using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				sendCts.CancelAfter(socketParameters.SendTimeout);
				for (int offset = 0, chunkCount = 1; offset < dataStream.Length; ++chunkCount)
				{
					logger.LogTrace("Send chunk {chunk}, offset {offset}", chunkCount, offset);

					offset += await socket.SendAsync(
						new ReadOnlyMemory<byte>(sendBuffer, offset, (int)(dataStream.Length - offset)),
						SocketFlags.None,
						sendCts.Token);
				}
			}

			// receive
			var returnedData = new byte[TopicResponseHeader.HeaderLength];
			var receiveOffset = 0;
			try
			{
				TopicResponseHeader? header = null;

				using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				receiveCts.CancelAfter(socketParameters.ReceiveTimeout);
				for (int chunkCount = 1; receiveOffset < returnedData.Length - 1; ++chunkCount)
				{
					logger.LogTrace("Receive chunk {chunk}, offset {receiveOffset}", chunkCount, receiveOffset);

					int read;
					try
					{
						read = await socket.ReceiveAsync(
							new Memory<byte>(returnedData, receiveOffset, returnedData.Length - receiveOffset),
							SocketFlags.None,
							receiveCts.Token);
					}
					catch (SocketException ex)
					{
						// BYOND closes the socket after replying *sometimes*
						if ((SocketError)ex.ErrorCode == SocketError.ConnectionReset
							&& receiveOffset == returnedData.Length)
						{
							logger.LogDebug(ex, "BYOND reset connection after receive");
							break;
						}

						throw;
					}

					receiveOffset += read;
					if (read == 0)
					{
						if (receiveOffset < returnedData.Length)
							logger.LogDebug("Zero bytes read at offset {receiveOffset} before expected length of {expectedLength}.", receiveOffset, returnedData.Length);
						break;
					}

					if (header == null && receiveOffset >= TopicResponseHeader.HeaderLength)
					{
						// we now have the header
						header = new TopicResponseHeader(returnedData);

						if (!header.PacketLength.HasValue)
							throw new InvalidOperationException("Expected header content length to have a value!");

						var expectedLength = header.PacketLength.Value;
						logger.LogTrace("Header indicates packet length of {expectedLength}", expectedLength);
						Array.Resize(ref returnedData, expectedLength);
					}
				}

				if (socket.Connected)
					try
					{
						// we need to properly disconnect the socket, otherwise Byond can be an asshole about future sends
						logger.LogTrace("Disconnect");
						using var disconnectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
						disconnectCts.CancelAfter(socketParameters.DisconnectTimeout);
						await socket.DisconnectAsync(false, disconnectCts.Token).ConfigureAwait(false);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						logger.LogDebug(ex, "Disconnect exception!");
					}
			}
			finally
			{
				if (returnedData.Length > receiveOffset)
					Array.Resize(ref returnedData, receiveOffset);
			}

			return new TopicResponse(returnedData);
		}

		/// <inheritdoc />
		public string SanitizeString(string input)
		{
			if (input == null)
				throw new ArgumentNullException(nameof(input));

			return HttpUtility.UrlEncode(input);
		}
	}
}
