using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using PeliculasAPI;
using PeliculasAPI.Servicios;
using PeliculasAPI.Utilidades;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using System.Text;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UseSerilog((contexto, servicios, configuracion) => configuracion
	.ReadFrom.Configuration(contexto.Configuration)
	.ReadFrom.Services(servicios)
	.Enrich.FromLogContext()
	.WriteTo.Console(outputTemplate:
		"[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
	.WriteTo.File("logs/api-.log", 
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
		rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
);

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDBContext>(options => options.UseSqlServer("name=DefaultConnection", 
    sqlServer => sqlServer.UseNetTopologySuite()));

builder.Services.AddSingleton<GeometryFactory>(
	NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326)
);

builder.Services.AddSingleton(provider =>
{
	var geometryFactory = provider.GetRequiredService<GeometryFactory>();

	var config = new MapperConfiguration(cfg =>
	{
		cfg.AddProfile(new AutoMapperProfiles(geometryFactory));
	}, NullLoggerFactory.Instance);

	return config.CreateMapper();
});

builder.Services.AddOutputCache(opciones =>
{
    opciones.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(60);
	opciones.AddPolicy(nameof(PoliticaCache), PoliticaCache.Instance);
});

var origenesPermitidos = builder.Configuration.GetValue<string>("origenesPermitidos")!.Split(',');

builder.Services.AddCors(opciones =>
{
    opciones.AddDefaultPolicy(opcCors => 
	{ 
		opcCors.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("cantidad-total-registros"); 
	});
});

builder.Services.AddTransient<IAlmacenadorArchivos, AlmacenadorArchivosLocal>();
//builder.Services.AddTransient<IAlmacenadorArchivos, AlmacenadorArchivosAzure>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddIdentityCore<IdentityUser>()
	.AddEntityFrameworkStores<ApplicationDBContext>()
	.AddDefaultTokenProviders();

builder.Services.AddScoped<UserManager<IdentityUser>>();
builder.Services.AddScoped<SignInManager<IdentityUser>>();

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication().AddJwtBearer(opciones =>
{
	opciones.MapInboundClaims = false;
	opciones.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true,
		ValidIssuer = jwt["Issuer"],
		ValidAudience = jwt["Audience"],
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["llavejwt"]!)),
		ClockSkew = TimeSpan.Zero
	};
});

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("esadmin", politica => politica.RequireClaim("esadmin"));

builder.Services.AddTransient<IServicioUsuarios, ServicioUsuarios>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opc =>
{
	opc.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
	{
		Version = "v1",
		Title = "Movies API",
		Description = "Web API para trabajar con datos de películas",
		Contact = new OpenApiContact
		{
			Email = "iliana.8093@gmail.com",
			Name = "Iliana Barron",
			Url = new Uri("https://github.com/iliana212")
		}
	});

	opc.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
	{
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		Description = "JWT Authorization header using the Bearer scheme."
	});
	
	opc.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("bearer", document)] = []
	}); 
});

builder.Services.AddLogging();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(opciones =>
{
	opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

	// Login: 5 intentos por minuto por IP
	opciones.AddPolicy("login", httpContext =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 5,
				Window = TimeSpan.FromMinutes(1),
				QueueLimit = 0
			}));

	// Registro: 3 cuentas cada 10 minutos por IP
	opciones.AddPolicy("registro", httpContext =>
		RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 3,
				Window = TimeSpan.FromMinutes(10),
				QueueLimit = 0
			}));

	opciones.OnRejected = async (contexto, cancelacion) =>
	{
		if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
		{
			contexto.HttpContext.Response.Headers.RetryAfter = ((int)espera.TotalSeconds).ToString();
		}

		contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		await contexto.HttpContext.Response.WriteAsJsonAsync(
			new ProblemDetails { Status = 429, Title = "Demasiados intentos", Instance=contexto.HttpContext.Request.Path }, cancelacion);
	};
});

builder.Services.AddHealthChecks()
	.AddDbContextCheck<ApplicationDBContext>("PeliculasAPI", tags: ["ready"]);

builder.Services.AddApiVersioning(opciones =>
{
	opciones.DefaultApiVersion = new ApiVersion(1, 0);
	opciones.AssumeDefaultVersionWhenUnspecified = true;
	opciones.ReportApiVersions = true;
	opciones.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(opciones =>
{
	opciones.GroupNameFormat = "'v'VVV";
	opciones.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSerilogRequestLogging(opciones =>
{
	opciones.GetLevel = (contexto, _, _) =>
		contexto.Request.Path.StartsWithSegments("/swagger")
			? LogEventLevel.Verbose
			: LogEventLevel.Information;

	opciones.EnrichDiagnosticContext = (diagnostico, httpContext) =>
	{
		diagnostico.Set("Usuario", httpContext.User.Identity?.IsAuthenticated == true
			? httpContext.User.FindFirst("email")?.Value
			: "anonimo");
	};
});

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(opc => { opc.EnablePersistAuthorization(); });

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = check => check.Tags.Contains("ready"),
	ResponseWriter = async (contexto, reporte) =>
	{
		contexto.Response.ContentType = "application/json";
		await contexto.Response.WriteAsJsonAsync(new
		{
			estado = reporte.Status.ToString(),
			checks = reporte.Entries.Select(e => new
			{
				nombre = e.Key,
				estado = e.Value.Status.ToString(),
				duracionMs = e.Value.Duration.TotalMilliseconds
			})
		});
	}
});

app.Run();