using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Byond.TopicSender.Tests
{
	[TestClass]
	public class IntegrationTests
	{
		static ITopicClient CreateTopicSender()
		{
			using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
			var logger = loggerFactory.CreateLogger<TopicClient>();
			return new TopicClient(new SocketParameters
			{
				SendTimeout = 10000,
				ReceiveTimeout = 10000
			}, logger);
		}


		[TestMethod]
		public async Task TestGoonstation()
		{
			var topicSender = CreateTopicSender();

			var response = await topicSender.SendTopic("goon2.goonhub.com", "?status", 26200);
			Assert.IsNotNull(response);
		}

		[TestMethod]
		public async Task TestEnvironmentString()
		{
			var topicSender = CreateTopicSender();

			var data = "expecting_this=response";
			var response = await topicSender.SendTopic("localhost", $"?{data}", 61612);
			Assert.IsNotNull(response);
			Assert.IsFalse(response.FloatData.HasValue);
			Assert.IsNotNull(response.StringData);
			Assert.AreEqual($"Received: {data}", response.StringData);
		}

		[TestMethod]
		public async Task TestEnvironmentFloat()
		{
			var topicSender = CreateTopicSender();

			var data = "return_float=1";
			var response = await topicSender.SendTopic("localhost", $"?{data}", 61612);
			Assert.IsNotNull(response);
			Assert.IsNull(response.StringData);
			Assert.IsTrue(response.FloatData.HasValue);
			Assert.AreEqual(3.14f, response.FloatData.Value);
		}
	}
}
