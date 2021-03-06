using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Zeebe.Client;

namespace Client.IntegrationTests
{
    public class ZeebeIntegrationTestHelper
    {
        private const string LatestVersion = "0.23.0";

        private Container container;
        private IZeebeClient client;

        private readonly string version;

        private ZeebeIntegrationTestHelper(string version)
        {
            this.version = version;
        }

        public static ZeebeIntegrationTestHelper Latest()
        {
            return new ZeebeIntegrationTestHelper(LatestVersion);
        }

        public static ZeebeIntegrationTestHelper OfVersion(string version)
        {
            return new ZeebeIntegrationTestHelper(version);
        }

        public async Task<IZeebeClient> SetupIntegrationTest()
        {
            container = CreateZeebeContainer();
            await container.Start();

            client = CreateZeebeClient();
            await AwaitBrokerReadiness();
            return client;
        }

        public async Task TearDownIntegrationTest()
        {
            client.Dispose();
            client = null;
            await container.Stop();
            container = null;
        }

        private Container CreateZeebeContainer()
        {
            return new GenericContainerBuilder<Container>()
                .Begin()
                .WithImage("camunda/zeebe:" + version)
                .WithExposedPorts(26500)
                .Build();
        }

        private IZeebeClient CreateZeebeClient()
        {
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                // configure Logging with NLog
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "NLog.config");
                loggingBuilder.AddNLog(path);
            });

            var host = "0.0.0.0:" + container.GetMappedPort(26500);

            return ZeebeClient.Builder()
                .UseLoggerFactory(loggerFactory)
                .UseGatewayAddress(host)
                .UsePlainText()
                .Build();
        }

        private async Task AwaitBrokerReadiness()
        {
            var ready = false;
            do
            {
                try
                {
                    var topology = await client.TopologyRequest().Send();
                    ready = topology.Brokers[0].Partitions.Count == 1;
                }
                catch (Exception)
                {
                    // retry
                }
            }
            while (!ready);
        }
    }
}