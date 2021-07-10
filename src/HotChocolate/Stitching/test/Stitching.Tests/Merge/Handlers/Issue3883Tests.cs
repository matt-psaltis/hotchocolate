using System.Collections.Generic;
using System.Linq;
using HotChocolate.Language;
using HotChocolate.Language.Utilities;
using Snapshooter.Xunit;
using Xunit;

namespace HotChocolate.Stitching.Merge.Handlers
{
    public class Issue3883Tests
    {
        [Fact]
        public void Merge_SchemasWithConflictingComplexTypes_TypesMerge()
        {
            // arrange
            DocumentNode schema_a =
                Utf8GraphQLParser.Parse(@"
type CompanyConnection {
  pageInfo: PageInfo!
  edges: [CompanyEdge!]
  nodes: [Company]
}

type CompanyEdge {
  cursor: String!
  node: Company
}

type Company {
  id: String!
  name1: String
}");
            DocumentNode schema_b =
                Utf8GraphQLParser.Parse(@"
type CompanyConnection {
  pageInfo: PageInfo!
  edges: [CompanyEdge!]
  nodes: [Company]
}

type CompanyEdge {
  cursor: String!
  node: Company
}

type Company {
  id: String!
  name3: String
}");
            DocumentNode schema_c =
                Utf8GraphQLParser.Parse(@"
type CompanyConnection {
  pageInfo: PageInfo!
  edges: [CompanyEdge!]
  nodes: [Company]
}

type CompanyEdge {
  cursor: String!
  node: Company
}

type Company {
  id: String!
  name3: String
}");

            var types = BuildTypes("Schema_A", schema_a)
                .Concat(BuildTypes("Schema_B", schema_b))
                .Concat(BuildTypes("Schema_C", schema_c))
                .ToList();

            var context = new SchemaMergeContext();

            // act
            var typeMerger = new ObjectTypeMergeHandler((c, t) => { });
            typeMerger.Merge(context, types);

            // assert
            context
                .CreateSchema()
                .Print()
                .MatchSnapshot();
        }

        private static IEnumerable<ITypeInfo> BuildTypes(string schemaName, DocumentNode schema)
        {
            var schemaInfo = new SchemaInfo(schemaName, schema);
            return schema.Definitions.OfType<ITypeDefinitionNode>()
                .Select(x => TypeInfo.Create(x, schemaInfo));
        }
    }
}
