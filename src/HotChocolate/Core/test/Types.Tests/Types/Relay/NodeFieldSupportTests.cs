using System;
using System.Threading.Tasks;
using HotChocolate.Execution;
using Snapshooter.Xunit;
using Xunit;

namespace HotChocolate.Types.Relay
{
    public class NodeFieldSupportTests
    {
        [Obsolete]
        [Fact]
        public async Task NodeId_Is_Correctly_Formatted()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .EnableRelaySupport()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .AsNode()
                    .IdField(t => t.Id)
                    .NodeResolver((_, id) => Task.FromResult(new Bar { Id = id })))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync("{ bar { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Obsolete]
        [Fact]
        public async Task Node_Type_Is_Correctly_In_Context()
        {
            // arrange
            string type = null;

            ISchema schema = SchemaBuilder.New()
                .EnableRelaySupport()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .AsNode()
                    .IdField(t => t.Id)
                    .NodeResolver((_, id) => Task.FromResult(new Bar { Id = id })))
                .Use(next => async ctx =>
                {
                    await next(ctx);

                    if (ctx.LocalContextData.TryGetValue(
                        WellKnownContextData.InternalType,
                        out var value))
                    {
                        type = (NameString)value;
                    }
                })
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            Assert.Equal("Bar", type);
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Separated_Resolver()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .ImplementsNode()
                    .IdField(t => t.Id)
                    .ResolveNodeWith<BarResolver>(t => t.GetBarAsync(default)))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Nodes_Get_Single()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .ImplementsNode()
                    .IdField(t => t.Id)
                    .ResolveNodeWith<BarResolver>(t => t.GetBarAsync(default)))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ nodes(ids: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Nodes_Get_Many()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .ImplementsNode()
                    .IdField(t => t.Id)
                    .ResolveNodeWith<BarResolver>(t => t.GetBarAsync(default)))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ nodes(ids: [\"QmFyCmQxMjM=\", \"QmFyCmQxMjM=\"]) { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Parent_Id()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType(
                    x => x.Name("Query")
                        .Field("childs")
                        .Resolve(new Child { Id = "123" }))
                .AddObjectType<Child>(d => d
                    .ImplementsNode()
                    .IdField(t => t.Id)
                    .ResolveNode((_, id) => Task.FromResult(new Child { Id = id })))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync("{ childs { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Separated_Resolver_ImplicitId()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo>()
                .AddObjectType<Bar>(d => d
                    .ImplementsNode()
                    .ResolveNodeWith<BarResolver>(t => t.GetBarAsync(default)))
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Implicit()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo1>()
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Implicit_Resolver()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Bar5>()
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            var json = result.ToJson();
            json.MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Implicit_Named_Resolver()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo2>()
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Implicit_External_Resolver()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo3>()
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        [Fact]
        public async Task Node_Resolve_Implicit_Custom_IdField()
        {
            // arrange
            ISchema schema = SchemaBuilder.New()
                .AddGlobalObjectIdentification()
                .AddQueryType<Foo4>()
                .Create();

            IRequestExecutor executor = schema.MakeExecutable();

            // act
            IExecutionResult result = await executor.ExecuteAsync(
                "{ node(id: \"QmFyCmQxMjM=\") { id } }");

            // assert
            result.ToJson().MatchSnapshot();
        }

        public class Foo
        {
            public Bar Bar { get; set; } = new() { Id = "123" };
        }

        public class Bar
        {
            public string Id { get; set; }
        }

        public class BarResolver
        {
            public Task<Bar> GetBarAsync(string id) => Task.FromResult(new Bar { Id = id });
        }

        public class Foo1
        {
            public Bar1 Bar { get; set; } = new() { Id = "123" };
        }

        [ObjectType("Bar")]
        [Node]
        public class Bar1
        {
            public string Id { get; set; }

            public static Bar1 GetBar1(string id) => new() { Id = id };
        }

        public class Foo2
        {
            public Bar2 Bar { get; set; } = new() { Id = "123" };
        }

        [ObjectType("Bar")]
        [Node(NodeResolver = nameof(GetFoo))]
        public class Bar2
        {
            public string Id { get; set; }

            public static Bar2 GetFoo(string id) => new() { Id = id };
        }

        public class Foo3
        {
            public Bar3 Bar { get; set; } = new() { Id = "123" };
        }

        [ObjectType("Bar")]
        [Node(NodeResolverType = typeof(Bar3Resolver))]
        public class Bar3
        {
            public string Id { get; set; }
        }

        public static class Bar3Resolver
        {
            public static Bar3 GetBar3(string id) => new() { Id = id };
        }

        public class Foo4
        {
            public Bar4 Bar { get; set; } = new() { Id1 = "123" };
        }

        [ObjectType("Bar")]
        [Node(
            IdField = nameof(Id1),
            NodeResolver = nameof(GetFoo))]
        public class Bar4
        {
            public string Id1 { get; set; }

            public static Bar2 GetFoo(string id) => new() { Id = id };
        }

        [ObjectType("Bar")]
        [Node]
        public class Bar5
        {
            public string Id { get; set; }

            public static Bar5 Get(string id) => new() { Id = id };
        }

        public class Parent
        {
            public string Id { get; set; }
        }

        public class Child : Parent
        {
        }
    }
}
