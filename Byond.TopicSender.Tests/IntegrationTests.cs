using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Byond.TopicSender.Tests
{
	[TestClass]
	public class IntegrationTests
	{
		static ILoggerFactory loggerFactory;

		[ClassInitialize]
		public static void Initialize(TestContext _)
		{
			loggerFactory = LoggerFactory.Create(builder =>
			{
				builder.AddConsole();
				builder.AddDebug();
				builder.SetMinimumLevel(LogLevel.Trace);
			});
		}

		[ClassCleanup]
		public static void Cleanup() => loggerFactory.Dispose();

		static ITopicClient CreateTopicSender()
		{
			return new TopicClient(new SocketParameters
			{
				SendTimeout = TimeSpan.FromSeconds(10),
				ReceiveTimeout = TimeSpan.FromSeconds(10),
				ConnectTimeout = TimeSpan.FromSeconds(10),
				DisconnectTimeout = TimeSpan.FromSeconds(10)
			}, loggerFactory.CreateLogger("Test Topic Client"));
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

			var data = "hello";
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
