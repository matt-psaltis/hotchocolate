using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore.Serialization;
using HotChocolate.AspNetCore.Utilities;
using HotChocolate.Stitching.Schemas.Accounts;
using HotChocolate.Stitching.Schemas.Inventory;
using HotChocolate.Stitching.Schemas.Products;
using HotChocolate.Stitching.Schemas.Reviews;
using HotChocolate.Transport.Sockets;
using HotChocolate.Transport.Sockets.Client;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Snapshooter.Xunit;
using Squadron;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

[assembly: EnableDotMemoryUnitSupport]

namespace HotChocolate.Stitching.Integration;

public class TestScenarios : ServerTestBase, IClassFixture<RedisResource>
{
    private const string _accounts = "accounts";
    private const string _inventory = "inventory";
    private const string _products = "products";
    private const string _reviews = "reviews";
    private readonly ConnectionMultiplexer _connection;
    private TestServer _server;
    private readonly ITestOutputHelper _testOutputHelper;

    protected Uri SubscriptionUri { get; } = new("ws://localhost:5000/graphql");

    public TestScenarios(ITestOutputHelper testOutputHelper, TestServerFactory serverFactory, RedisResource redisResource)
        : base(serverFactory)
    {
        _testOutputHelper = testOutputHelper;
        DotMemoryUnitTestOutput.SetOutputMethod(testOutputHelper.WriteLine);
        _connection = redisResource.GetConnection();
    }

    [DotMemoryUnit(SavingStrategy = SavingStrategy.OnCheckFail, Directory = @"C:\Temp\dotMemory")]
    [Fact]
    public void RefreshSchema()
    {
        var me = RefreshSchemaAsync().GetAwaiter().GetResult();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (dotMemoryApi.IsEnabled)
        {
            dotMemory.Check(_ =>
            {
                AssertScenario(me);
            });

            return;
        }

        AssertScenario(me);
    }

    private void AssertScenario(ClientQueryResult me)
    {
        var list = Schema.Schemas
            .Where(x => x.TryGetTarget(out Schema schema) && !schema.Name.IsEmpty)
            .Select(x =>
            {
                x.TryGetTarget(out Schema schema);
                return schema.Name.Value!;
            })
            .Where(x => !x.Equals("_Default"))
            .GroupBy(x => x)
            .ToDictionary(x => x.Key,
                x => ((string Name, int Count))(x.Key, x.Count()));

        Assert.Null(me.Errors);

        Assert.All(list.Values, item =>
        {
            _testOutputHelper.WriteLine($"{item.Name} {item.Count}");
            Assert.Equal(1, item.Count);
        });

        me.MatchSnapshot();
    }

    public async Task<ClientQueryResult> RefreshSchemaAsync()
    {
        // arrange

        NameString configurationName = "C" + Guid.NewGuid().ToString("N");
        IHttpClientFactory httpClientFactory = CreateDefaultRemoteSchemas(configurationName);

        _server = ServerFactory.Create(
            services => services
                .AddSingleton(httpClientFactory)
                .AddRouting()
                .AddHttpResultSerializer(HttpResultSerialization.JsonArray)
                .AddGraphQLServer("APIGateway")
                .AddQueryType(d => d.Name("Query"))
                .AddSubscriptionType(d => d.Name("Subscription"))
                .AddRemoteSchemasFromRedis(configurationName, _ => _connection)
                .AddTypeExtension<QueryExtension>()
                .AddTypeExtension<SubscriptionsExtensions>()
                .AddExportDirectiveType(),
            app => app
                .UseWebSockets()
                .UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapGraphQL("/graphql", "APIGateway");
                }));

        await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

        WebSocketClient webSocketClient = CreateWebSocketClient(_server);
        using WebSocket webSocket = await webSocketClient.ConnectAsync(SubscriptionUri, CancellationToken.None);
        SocketClient client = await SocketClient.ConnectAsync(webSocket, CancellationToken.None);

        var subscriptionRequest = new OperationRequest("subscription { onNext() }");

        var index = 0;
        var sb = new StringBuilder();
        using SocketResult socketResult = await client.ExecuteAsync(subscriptionRequest, CancellationToken.None);
        try
        {
            await foreach (OperationResult operationResult in socketResult.ReadResultsAsync()
                               .WithCancellation(CancellationToken.None))
            {
                var streamedResult = operationResult.Data.ToString();
                sb.AppendLine(streamedResult);
                operationResult.Dispose();
                index++;

                if (index == 1)
                {
                    CreateDefaultRemoteSchemas(configurationName);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                if (index >= 3)
                {
                    break;
                }
            }
        }
        catch (SocketClosedException)
        {
        }

        Assert.Equal(1, index);

#if NET6_0
        HttpClient httpClient = _server.CreateClient();
        var result = await httpClient.GetStringAsync("/graphql?sdl", CancellationToken.None);
#endif

        ClientQueryResult me = await _server.GetAsync(
            new ClientQueryRequest { Query = "{ me { id name } }" });

        return me;
    }

    public TestServer CreateAccountsService(NameString configurationName) =>
        ServerFactory.Create(
            services => services
                .AddRouting()
                .AddHttpResultSerializer(HttpResultSerialization.JsonArray)
                .AddGraphQLServer()
                .AddAccountsSchema()
                .InitializeOnStartup()
                .PublishSchemaDefinition(c => c
                    .SetName(_accounts)
                    .IgnoreRootTypes()
                    .AddTypeExtensionsFromString(
                        @"extend type Query {
                                me: User! @delegate(path: ""user(id: 1)"")
                            }

                            extend type Review {
                                author: User @delegate(path: ""user(id: $fields:authorId)"")
                            }")
                    .PublishToRedis(configurationName, _ => _connection)),
            app => app
                .UseWebSockets()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGraphQL("/")));

    public TestServer CreateInventoryService(NameString configurationName) =>
        ServerFactory.Create(
            services => services
                .AddRouting()
                .AddHttpResultSerializer(HttpResultSerialization.JsonArray)
                .AddGraphQLServer()
                .AddInventorySchema()
                .InitializeOnStartup()
                .PublishSchemaDefinition(c => c
                    .SetName(_inventory)
                    .IgnoreRootTypes()
                    .AddTypeExtensionsFromString(
                        @"extend type Product {
                                inStock: Boolean
                                    @delegate(path: ""inventoryInfo(upc: $fields:upc).isInStock"")

                                shippingEstimate: Int
                                    @delegate(path: ""shippingEstimate(price: $fields:price weight: $fields:weight)"")
                            }")
                    .PublishToRedis(configurationName, _ => _connection)),
            app => app
                .UseWebSockets()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGraphQL("/")));

    public TestServer CreateProductsService(NameString configurationName) =>
        ServerFactory.Create(
            services => services
                .AddRouting()
                .AddHttpResultSerializer(HttpResultSerialization.JsonArray)
                .AddGraphQLServer()
                .AddProductsSchema()
                .InitializeOnStartup()
                .PublishSchemaDefinition(c => c
                    .SetName(_products)
                    .IgnoreRootTypes()
                    .AddTypeExtensionsFromString(
                        @"extend type Query {
                                topProducts(first: Int = 5): [Product] @delegate
                            }

                            extend type Review {
                                product: Product @delegate(path: ""product(upc: $fields:upc)"")
                            }")
                    .PublishToRedis(configurationName, _ => _connection)),
            app => app
                .UseWebSockets()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGraphQL("/")));

    public TestServer CreateReviewsService(NameString configurationName) =>
        ServerFactory.Create(
            services => services
                .AddRouting()
                .AddHttpResultSerializer(HttpResultSerialization.JsonArray)
                .AddGraphQLServer()
                .AddReviewSchema()
                .InitializeOnStartup()
                .PublishSchemaDefinition(c => c
                    .SetName(_reviews)
                    .IgnoreRootTypes()
                    .AddTypeExtensionsFromString(
                        @"extend type User {
                                reviews: [Review]
                                    @delegate(path:""reviewsByAuthor(authorId: $fields:id)"")
                            }

                            extend type Product {
                                reviews: [Review]
                                    @delegate(path:""reviewsByProduct(upc: $fields:upc)"")
                            }")
                    .PublishToRedis(configurationName, _ => _connection)),
            app => app
                .UseWebSockets()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGraphQL("/")));

    public IHttpClientFactory CreateDefaultRemoteSchemas(NameString configurationName)
    {
        var connections = new Dictionary<string, HttpClient>
            {
                { _accounts, CreateAccountsService(configurationName).CreateClient() },
                //{ _inventory, CreateInventoryService(configurationName).CreateClient() },
                //{ _products, CreateProductsService(configurationName).CreateClient() },
                //{ _reviews, CreateReviewsService(configurationName).CreateClient() },
            };

        return StitchingTestContext.CreateHttpClientFactory(connections);
    }

    protected static WebSocketClient CreateWebSocketClient(TestServer testServer)
    {
        WebSocketClient client = testServer.CreateWebSocketClient();
        client.ConfigureRequest = r => r.Headers.Add(
            "Sec-WebSocket-Protocol",
            WellKnownProtocols.GraphQL_Transport_WS);
        return client;
    }
}
