using Microsoft.AspNetCore.Http;
using HotChocolate.AspNetCore.Instrumentation;
using HotChocolate.AspNetCore.Serialization;
using HotChocolate.AspNetCore.Subscriptions;
using RequestDelegate = Microsoft.AspNetCore.Http.RequestDelegate;

namespace HotChocolate.AspNetCore;

public class WebSocketSubscriptionMiddleware : MiddlewareBase
{
    private readonly IServerDiagnosticEvents _diagnosticEvents;

    public WebSocketSubscriptionMiddleware(
        RequestDelegate next,
        IRequestExecutorResolver executorResolver,
        IHttpResultSerializer resultSerializer,
        IServerDiagnosticEvents diagnosticEvents,
        NameString schemaName)
        : base(next, executorResolver, resultSerializer, schemaName)
    {
        _diagnosticEvents = diagnosticEvents ??
            throw new ArgumentNullException(nameof(diagnosticEvents));
    }

    public Task InvokeAsync(HttpContext context)
    {
        return context.WebSockets.IsWebSocketRequest
            ? HandleWebSocketSessionAsync(context)
            : NextAsync(context);
    }

    private async Task HandleWebSocketSessionAsync(HttpContext context)
    {
        if (!IsDefaultSchema)
        {
            context.Items[WellKnownContextData.SchemaName] = SchemaName.Value;
        }

        void OnExecutorProxyOnExecutorEvicted(object o, EventArgs eventArgs)
        {
            context.Abort();
        }

        using (_diagnosticEvents.WebSocketSession(context))
        {
            RequestExecutorProxy proxy = ExecutorProxy;
            AutoUpdateRequestExecutorProxy? executor = default;
            try
            {
                proxy.ExecutorEvicted += OnExecutorProxyOnExecutorEvicted!;

                executor = await AutoUpdateRequestExecutorProxy
                    .CreateAsync(proxy, context.RequestAborted);

                ISocketSessionInterceptor interceptor = executor.GetRequiredService<ISocketSessionInterceptor>();
                context.Items[WellKnownContextData.RequestExecutor] = executor;
                await WebSocketSession.AcceptAsync(context, executor, interceptor);
            }
            catch (Exception ex)
            {
                _diagnosticEvents.WebSocketSessionError(context, ex);
            }
            finally
            {
                proxy.ExecutorEvicted -= OnExecutorProxyOnExecutorEvicted!;
                executor?.Dispose();
            }
        }
    }
}
