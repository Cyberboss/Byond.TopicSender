using System;

namespace Byond.TopicSender
{
	/// <summary>
	/// <see cref="System.Net.Sockets.Socket"/> parameters used by the <see cref="TopicClient"/>.
	/// </summary>
	public readonly record struct SocketParameters
	{
		/// <summary>
		/// The timeout for the send operation.
		/// </summary>
		public TimeSpan SendTimeout { get; init; }

		/// <summary>
		/// The timeout for the receive operation.
		/// </summary>
		public TimeSpan ReceiveTimeout { get; init; }

		/// <summary>
		/// The timeout for the receive operation.
		/// </summary>
		public TimeSpan ConnectTimeout { get; init; }

		/// <summary>
		/// The timeout for the disconnect operation.
		/// </summary>
		public TimeSpan DisconnectTimeout { get; init; }
	}
}
