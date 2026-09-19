using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using PeliculasAPI;
using PeliculasAPI.Servicios;
using PeliculasAPI.Utilidades;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI(opc => { opc.EnablePersistAuthorization(); });

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseOutputCache();

app.Run();