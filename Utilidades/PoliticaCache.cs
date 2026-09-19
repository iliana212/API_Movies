using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace PeliculasAPI.Utilidades
{
	internal sealed class PoliticaCache : IOutputCachePolicy
	{
		public static readonly PoliticaCache Instance = new();

		private PoliticaCache()
		{
		}

		/// <inheritdoc />
		ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
		{
			var attemptOutputCaching = AttemptOutputCaching(context);
			context.EnableOutputCaching = true;
			context.AllowCacheLookup = attemptOutputCaching;
			context.AllowCacheStorage = attemptOutputCaching;
			context.AllowLocking = true;

			// Vary by any query by default
			context.CacheVaryByRules.QueryKeys = "*";

			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}

		/// <inheritdoc />
		ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
		{
			var response = context.HttpContext.Response;

			// Verify existence of cookie headers
			if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
			{
				context.AllowCacheStorage = false;
				return ValueTask.CompletedTask;
			}

			if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
			{
				context.AllowCacheStorage = false;
				return ValueTask.CompletedTask;
			}

			// Check response code
			if (response.StatusCode != StatusCodes.Status200OK)
			{
				context.AllowCacheStorage = false;
				return ValueTask.CompletedTask;
			}

			return ValueTask.CompletedTask;
		}

		private static bool AttemptOutputCaching(OutputCacheContext context)
		{
			// Check if the current request fulfills the requirements to be cached

			var request = context.HttpContext.Request;

			// Verify the method
			if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
			{
				return false;
			}

			return true;
		}
	}
}
