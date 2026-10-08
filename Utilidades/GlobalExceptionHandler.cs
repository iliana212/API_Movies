using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PeliculasAPI.Utilidades
{
	public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
	{
		public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
		{
			var (statusCode, titulo, detalle) = exception switch
			{
				DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflicto de concurrencia",
					"El registro fue modificado por otro usuario. Recárgalo e intenta de nuevo."),

				// 2601 / 2627: violación de índice o restricción única en SQL Server
				DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
					(StatusCodes.Status409Conflict, "Conflicto de datos", "Ya existe un registro con esos datos."),

				// 547: violación de llave foránea
				DbUpdateException { InnerException: SqlException { Number: 547 } } =>
					(StatusCodes.Status400BadRequest, "Referencia inválida",
					"Alguno de los identificadores enviados no existe o el registro está en uso."),

				DbUpdateException => (StatusCodes.Status400BadRequest, "Error al guardar los datos",
					"No se pudo guardar la información. Verifica los datos enviados."),

				BadHttpRequestException => (StatusCodes.Status400BadRequest, "Solicitud inválida",
					"La solicitud no tiene el formato esperado."),

				_ => (StatusCodes.Status500InternalServerError, "Error interno del servidor",
					env.IsDevelopment() ? exception.Message : "Ocurrió un error inesperado.")
			};

			if (statusCode == StatusCodes.Status500InternalServerError)
			{
				logger.LogError(exception, "Error no controlado procesando {Metodo} {Ruta}",
					httpContext.Request.Method, httpContext.Request.Path);
			}
			else
			{
				logger.LogWarning(exception, "Error controlado ({Status}) procesando {Metodo} {Ruta}",
					statusCode, httpContext.Request.Method, httpContext.Request.Path);
			}

			var problemDetails = new ProblemDetails
			{
				Status = statusCode,
				Title = titulo,
				Detail = detalle,
				Instance = httpContext.Request.Path
			};
			problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

			httpContext.Response.StatusCode = statusCode;
			await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

			return true;
		}
	}
}