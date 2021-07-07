using System;
using System.Reflection;
using HotChocolate.Language;

#nullable enable

namespace HotChocolate.Resolvers.Expressions.Parameters
{
    internal sealed class OperationParameterExpressionBuilder
        : LambdaParameterExpressionBuilder<IResolverContext, OperationDefinitionNode>
    {
        public OperationParameterExpressionBuilder()
            : base(ctx => ctx.Operation)
        {
        }

        public override ArgumentKind Kind => ArgumentKind.OperationDefinitionSyntax;

        public override bool CanHandle(ParameterInfo parameter, Type source)
            => typeof(OperationDefinitionNode) == parameter.ParameterType;
    }
}
