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
	public sealed class ByondTopicSender : IByondTopicSender
	{
		/// <inheritdoc />
		public int SendTimeout { get; set; }
		/// <inheritdoc />
		public int ReceiveTimeout { get; set; }

		/// <inheritdoc />
		public async Task<string> SendTopic(string destinationServer, ushort port, string queryString, CancellationToken cancellationToken = default)
		{
			if (destinationServer == null)
				throw new ArgumentNullException(nameof(destinationServer));
			var hostEntries = await Dns.GetHostAddressesAsync(destinationServer).ConfigureAwait(false);
			//pick the first IPV4 entry
			return await SendTopic(hostEntries.First(x => x.AddressFamily == AddressFamily.InterNetwork), port, queryString, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task<string> SendTopic(IPAddress address, ushort port, string queryString, CancellationToken cancellationToken = default)
		{
			if (address == null)
				throw new ArgumentNullException(nameof(address));
			return SendTopic(new IPEndPoint(address, port), queryString, cancellationToken);
		}

		/// <inheritdoc />
		public async Task<string> SendTopic(IPEndPoint endPoint, string queryString, CancellationToken cancellationToken = default)
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

			var packet = Encoding.UTF8.GetBytes(fullString);
			packet[1] = 0x83;
			var FinalLength = packet.Length - 4;
			if (FinalLength > UInt16.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(queryString), queryString, "Topic too long!");

			var lengthBytes = BitConverter.GetBytes((ushort)FinalLength);

			var lilEndy = BitConverter.IsLittleEndian;

			packet[2] = lengthBytes[lilEndy ? 1 : 0];
			packet[3] = lengthBytes[lilEndy ? 0 : 1];

			using (var topicSender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
			{
				SendTimeout = SendTimeout,
				ReceiveTimeout = ReceiveTimeout
			})
			{
				//connect
				var connectTaskCompletionSource = new TaskCompletionSource<object>();
				topicSender.BeginConnect(endPoint, new AsyncCallback((asyncResult) =>
				{
					try
					{
						topicSender.EndConnect(asyncResult);
						connectTaskCompletionSource.TrySetResult(null);
					}
					catch (Exception e)
					{
						connectTaskCompletionSource.TrySetException(e);
					}
				}), null);
				using (cancellationToken.Register(() => connectTaskCompletionSource.TrySetCanceled()))
					await connectTaskCompletionSource.Task.ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();

				//send
				for (var offset = 0; offset < packet.Length;)
				{
					var sendTaskCompletionSource = new TaskCompletionSource<int>();
					topicSender.BeginSend(packet, offset, packet.Length - offset, SocketFlags.None, new AsyncCallback((asyncResult) =>
					{
						try
						{
							sendTaskCompletionSource.TrySetResult(topicSender.EndSend(asyncResult));
						}
						catch (Exception e)
						{
							sendTaskCompletionSource.TrySetException(e);
						}
					}), null);
					using (cancellationToken.Register(() => sendTaskCompletionSource.TrySetCanceled()))
						offset += await sendTaskCompletionSource.Task.ConfigureAwait(false);
					cancellationToken.ThrowIfCancellationRequested();
				}

				//receive
				var recieveTaskCompletionSource = new TaskCompletionSource<int>();
				var returnedData = new byte[UInt16.MaxValue];
				topicSender.BeginReceive(returnedData, 0, returnedData.Length, SocketFlags.None, new AsyncCallback((asyncResult) =>
				{
					try
					{
						recieveTaskCompletionSource.TrySetResult(topicSender.EndReceive(asyncResult));
					}
					catch (Exception e)
					{
						recieveTaskCompletionSource.TrySetException(e);
					}
				}), null);
				using (cancellationToken.Register(() => recieveTaskCompletionSource.TrySetCanceled()))
					await recieveTaskCompletionSource.Task.ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();

				//we need to properly disconnect the socket, otherwise Byond can be an asshole about future sends
				var disconnectTaskCompletionSource = new TaskCompletionSource<object>();
				topicSender.BeginDisconnect(false, new AsyncCallback((asyncResult) =>
				{
					try
					{
						topicSender.EndDisconnect(asyncResult);
						disconnectTaskCompletionSource.TrySetResult(null);
					}
					catch (Exception e)
					{
						disconnectTaskCompletionSource.TrySetException(e);
					}
				}), null);
				using (cancellationToken.Register(() => disconnectTaskCompletionSource.TrySetCanceled()))
					await disconnectTaskCompletionSource.Task.ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();

				//parse
				var raw_string = Encoding.ASCII.GetString(returnedData).TrimEnd(new char[] { (char)0 }).Trim();
				if (raw_string.Length > 6)
					return raw_string.Substring(5, raw_string.Length - 5);
				return null;
			}
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
