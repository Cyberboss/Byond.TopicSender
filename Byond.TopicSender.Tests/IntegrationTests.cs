using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Byond.TopicSender.Tests
{
	[TestClass]
	public class IntegrationTests
	{
		[TestMethod]
		public async Task TestGoonstation()
		{
			using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
			var logger = loggerFactory.CreateLogger<TopicClient>();
			var topicSender = new TopicClient(new SocketParameters
			{
				SendTimeout = 10000,
				ReceiveTimeout = 10000
			}, logger);

			var response = await topicSender.SendTopic("goon2.goonhub.com", "?status", 26200);
		}
	}
}
