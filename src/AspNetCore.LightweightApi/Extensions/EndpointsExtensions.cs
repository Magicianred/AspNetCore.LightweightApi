﻿using AspNetCore.LightweightApi.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AspNetCore.LightweightApi.Extensions
{
    public static class EndpointsExtensions
    {
        private static readonly Type _endpointWithOutputType = typeof(IEndpointHandler<>);

        public static void UseLightweightApi(this IEndpointRouteBuilder endpoints)
        {
            var endpointCollection = endpoints.ServiceProvider.GetRequiredService<EndpointCollection>();
            foreach (var endpoint in endpointCollection.Items)
            {
                var httpMethod = endpoint.Method.ToString().ToUpperInvariant();
                var requestHandler = endpoint.HandlerType switch
                {
                    EndpointHandlerType.Basic => GetRequestHandler(endpoint.Type),
                    EndpointHandlerType.WithOutput => GetRequestHandlerOfEndpointWithOutput(endpoint.Type),
                    EndpointHandlerType.WithInputAndOutput => throw new NotImplementedException(),
                    _ => throw new NotImplementedException()
                };

                endpoints.MapMethods(endpoint.Pattern, new[] { httpMethod }, requestHandler);
            }
        }

        private static RequestDelegate GetRequestHandler(Type type)
        {
            return async context =>
            {
                var handler = (IEndpointHandler)context.RequestServices.GetRequiredService(type);
                await handler.Handle(context);
            };
        }

        private static RequestDelegate GetRequestHandlerOfEndpointWithOutput(Type type)
        {
            var handleTask = GenerateTaskConverter(type); // Generate once
            return async context =>
            {
                var handler = context.RequestServices.GetRequiredService(type);
                var result = await handleTask(handler, context);
                await context.Response.WriteAsJsonAsync(result);
            };
        }

        private static Func<object, HttpContext, Task<object?>> GenerateTaskConverter(Type type)
        {
            var handleMethod = type.GetMethod(nameof(IEndpointHandler.Handle))!;
            var outputType = handleMethod.ReturnType.GetGenericArguments();

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var contextParam = Expression.Parameter(typeof(HttpContext), "context");
            var instanceExpr = Expression.TypeAs(handlerParam, type);

            var call = Expression.Call(instanceExpr, handleMethod, contextParam);
            call = Expression.Call(typeof(EndpointsExtensions), nameof(ConvertTask), outputType, call);

            var lambda = Expression.Lambda<Func<object, HttpContext, Task<object?>>>(call, handlerParam, contextParam);
            return lambda.Compile();
        }

        private static async Task<object?> ConvertTask<T>(Task<T> task) => await task;
    }
}
