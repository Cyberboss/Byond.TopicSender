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
			Assert.IsNotNull(response);
		}

		[TestMethod]
		public async Task TestEnvironmentString()
		{
			using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
			var logger = loggerFactory.CreateLogger<TopicClient>();
			var topicSender = new TopicClient(new SocketParameters
			{
				SendTimeout = 10000,
				ReceiveTimeout = 10000
			}, logger);

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
			using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
			var logger = loggerFactory.CreateLogger<TopicClient>();
			var topicSender = new TopicClient(new SocketParameters
			{
				SendTimeout = 10000,
				ReceiveTimeout = 10000
			}, logger);

			var data = "return_float=1";
			var response = await topicSender.SendTopic("localhost", $"?{data}", 61612);
			Assert.IsNotNull(response);
			Assert.IsNull(response.StringData);
			Assert.IsTrue(response.FloatData.HasValue);
			Assert.AreEqual(3.14f, response.FloatData.Value);
		}
	}
}
