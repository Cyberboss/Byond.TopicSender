using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Byond.TopicSender
{
	/// <summary>
	/// Packet sender to create /world/Topic calls
	/// </summary>
	public interface IByondTopicSender
	{
		/// <summary>
		/// The timeout for the send operation. Maps directly to <see cref="System.Net.Sockets.Socket.SendTimeout"/>
		/// </summary>
		int SendTimeout { get; set; }
		/// <summary>
		/// The timeout for the receive operation. Maps directly to <see cref="System.Net.Sockets.Socket.ReceiveTimeout"/>
		/// </summary>
		int ReceiveTimeout { get; set; }

		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a <paramref name="destinationServer"/>
		/// </summary>
		/// <param name="destinationServer">The IP address or domain name of the server</param>
		/// <param name="port">The port of the <paramref name="destinationServer"/></param>
		/// <param name="queryString">The query string to send</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="string"/> from the server</returns>
		Task<string> SendTopic(string destinationServer, ushort port, string queryString, CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a server at a given <paramref name="address"/>
		/// </summary>
		/// <param name="address">The <see cref="IPAddress"/> of the server</param>
		/// <param name="port">The port of the <paramref name="address"/></param>
		/// <param name="queryString">The query string to send</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="string"/> from the server</returns>
		Task<string> SendTopic(IPAddress address, ushort port, string queryString, CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a server at a given <paramref name="endPoint"/>
		/// </summary>
		/// <param name="endPoint">The <see cref="IPEndPoint"/> of the server</param>
		/// <param name="queryString">The query string to send</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="string"/> from the server</returns>
		Task<string> SendTopic(IPEndPoint endPoint, string queryString, CancellationToken cancellationToken = default);

		/// <summary>
		/// Properly escapes characters for a Byond Topic() packet. See http://www.byond.com/docs/ref/info.html#/proc/list2params
		/// </summary>
		/// <param name="input">The <see cref="string"/> to sanitize</param>
		/// <returns>The sanitized <see cref="string"/></returns>
		string SanitizeString(string input);
	}
}