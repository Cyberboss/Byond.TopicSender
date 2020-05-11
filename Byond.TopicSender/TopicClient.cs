using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

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
		readonly ILogger<TopicClient> logger;

		private static async Task<TResult> AsyncSocketOperation<TResult>(
			Func<AsyncCallback, IAsyncResult> start,
			Func<IAsyncResult, TResult> end,
			TimeSpan timeout,
			CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<TResult>();
			start(new AsyncCallback(asyncResult =>
			{
				try
				{
					var result = end(asyncResult);
					tcs.TrySetResult(result);
				}
				catch (Exception ex)
				{
					tcs.TrySetException(ex);
				}
			}));

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(timeout);
			cancellationToken = cts.Token;
			TResult result;
			using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
				result = await tcs.Task.ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();
			return result;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TopicClient"/> <see langword="class"/>.
		/// </summary>
		/// <param name="socketParameters">The <see cref="SocketParameters"/> to use.</param>
		/// <param name="logger">The optional <see cref="ILogger"/> to use.</param>
		public TopicClient(SocketParameters socketParameters, ILogger<TopicClient>? logger = null)
		{
			this.socketParameters = socketParameters ?? throw new ArgumentNullException(nameof(socketParameters));
			this.logger = logger ?? new NullLogger<TopicClient>();
		}

		/// <inheritdoc />
		public async Task<TopicResponse> SendTopic(string destinationServer, string queryString, ushort port, CancellationToken cancellationToken = default)
		{
			if (destinationServer == null)
				throw new ArgumentNullException(nameof(destinationServer));
			var hostEntries = await Dns.GetHostAddressesAsync(destinationServer).ConfigureAwait(false);
			//pick the first IPV4 entry
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

			//prepare
			var stringPacket = new StringBuilder();
			stringPacket.Append('\x00', 8);
			if (queryString.Length == 0 || queryString[0] != '?')
				queryString = '?' + queryString;
			stringPacket.Append(queryString);
			stringPacket.Append('\x00');

			var fullString = stringPacket.ToString();

			var sendPacket = Encoding.UTF8.GetBytes(fullString);
			sendPacket[1] = 0x83;
			var FinalLength = sendPacket.Length - 4;
			if (FinalLength > UInt16.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(queryString), queryString, "Topic too long!");

			var sendLengthBytes = BitConverter.GetBytes((ushort)FinalLength);

			var lilEndy = BitConverter.IsLittleEndian;

			sendPacket[2] = sendLengthBytes[lilEndy ? 1 : 0];
			sendPacket[3] = sendLengthBytes[lilEndy ? 0 : 1];

			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			var connectTimeout = socketParameters.ConnectTimeout;
			var sendTimeout = socketParameters.SendTimeout;
			var receiveTimeout = socketParameters.ReceiveTimeout;
			var disconnectTimeout = socketParameters.DisconnectTimeout;

			logger.LogDebug("Export to {0}: {1}", endPoint, queryString);
			logger.LogTrace(
				"Timeouts: Connect: {0}, Send: {1}, Recv: {2}, Disc: {3}, Raw data: {4}",
				connectTimeout,
				sendTimeout,
				receiveTimeout,
				disconnectTimeout,
				Convert.ToBase64String(sendPacket));

			// connect
			await AsyncSocketOperation<object?>(
				callback => socket.BeginConnect(endPoint, callback, null),
				asyncResult =>
				{
					socket.EndConnect(asyncResult);
					return null;
				},
				connectTimeout,
				cancellationToken)
				.ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();

			// send
			for (int offset = 0, chunkCount = 1; offset < sendPacket.Length; ++chunkCount)
			{
				if (chunkCount > 1)
					logger.LogTrace("Send chunk {0}, offset {1}", chunkCount, offset);

				offset += await AsyncSocketOperation(
					callback => socket.BeginSend(sendPacket, offset, sendPacket.Length + offset, SocketFlags.None, callback, null),
					asyncResult => socket.EndSend(asyncResult),
					socketParameters.SendTimeout,
					cancellationToken).ConfigureAwait(false);
			}

			// receive
			var returnedData = new byte[5];
			var receiveOffset = 0;
			bool checkedHeader = false;
			for (int chunkCount = 1; receiveOffset < returnedData.Length; ++chunkCount)
			{
				if (chunkCount > 1)
					logger.LogTrace("Receive chunk {0}, offset {1}", chunkCount, receiveOffset);

				var read = await AsyncSocketOperation(
					callback => socket.BeginReceive(returnedData, receiveOffset, returnedData.Length - receiveOffset, SocketFlags.None, callback, null),
					asyncResult => socket.EndReceive(asyncResult),
					receiveTimeout,
					cancellationToken)
					.ConfigureAwait(false);

				receiveOffset += read;
				if (read == 0)
				{
					if (receiveOffset < returnedData.Length)
						logger.LogTrace("Zero bytes read at offset {0} before expected length of {1}.", receiveOffset, returnedData.Length);
					break;
				}

				if (!checkedHeader && receiveOffset >= TopicResponseHeader.HeaderLength)
				{
					// we now have the header
					var header = new TopicResponseHeader(returnedData);

					if (!header.ContentLength.HasValue)
						throw new InvalidOperationException("Expected header content length to have a value!");

					var expectedLength = (ushort)(TopicResponseHeader.HeaderLength + header.ContentLength.Value);
					Array.Resize(ref returnedData, expectedLength);
					checkedHeader = true;
				}
			}

			//we need to properly disconnect the socket, otherwise Byond can be an asshole about future sends
			await AsyncSocketOperation<object?>(
				callback => socket.BeginDisconnect(false, callback, null),
				asyncResult =>
				{
					socket.EndDisconnect(asyncResult);
					return null;
				},
				disconnectTimeout,
				cancellationToken)
				.ConfigureAwait(false);

			if(returnedData.Length > receiveOffset)
				returnedData = returnedData.Take(receiveOffset).ToArray();

			logger.LogTrace("Received: {0}", Convert.ToBase64String(returnedData));
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
