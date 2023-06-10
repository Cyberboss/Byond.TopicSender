using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Byond.TopicSender
{
	/// <summary>
	/// Packet sender to create /world/Topic calls.
	/// </summary>
	public interface ITopicClient
	{
		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a <paramref name="destinationServer"/>.
		/// </summary>
		/// <param name="destinationServer">The IP address or domain name of the server.</param>
		/// <param name="queryString">The query string to send.</param>
		/// <param name="port">The port of the <paramref name="destinationServer"/>.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="TopicResponse"/> from the server.</returns>
		Task<TopicResponse> SendTopic(string destinationServer, string queryString, ushort port, CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a server at a given <paramref name="address"/>.
		/// </summary>
		/// <param name="address">The <see cref="IPAddress"/> of the server.</param>
		/// <param name="queryString">The query string to send.</param>
		/// <param name="port">The port of the <paramref name="address"/>.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="TopicResponse"/> from the server.</returns>
		Task<TopicResponse> SendTopic(IPAddress address, string queryString, ushort port, CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a <paramref name="queryString"/> to the /world/Topic of a server at a given <paramref name="endPoint"/>.
		/// </summary>
		/// <param name="endPoint">The <see cref="IPEndPoint"/> of the server.</param>
		/// <param name="queryString">The query string to send.</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the returned <see cref="TopicResponse"/> from the server.</returns>
		Task<TopicResponse> SendTopic(IPEndPoint endPoint, string queryString, CancellationToken cancellationToken = default);

		/// <summary>
		/// Properly escapes characters for a Byond Topic() packet. Performs URL encoding. See http://www.byond.com/docs/ref/info.html#/proc/list2params.
		/// </summary>
		/// <param name="input">The <see cref="string"/> to sanitize.</param>
		/// <returns>The sanitized <see cref="string"/>.</returns>
		string SanitizeString(string input);
	}
}
