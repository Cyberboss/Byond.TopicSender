﻿namespace Byond.TopicSender
{
	/// <summary>
	/// <see cref="System.Net.Sockets.Socket"/> parameters used by the <see cref="TopicClient"/>/
	/// </summary>
	public sealed class SocketParameters
	{
		/// <summary>
		/// The timeout for the send operation. Maps directly to <see cref="System.Net.Sockets.Socket.SendTimeout"/>
		/// </summary>
		public int SendTimeout { get; set; }

		/// <summary>
		/// The timeout for the receive operation. Maps directly to <see cref="System.Net.Sockets.Socket.ReceiveTimeout"/>
		/// </summary>
		public int ReceiveTimeout { get; set; }
	}
}