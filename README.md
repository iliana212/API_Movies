🇪🇸 Español&nbsp;|&nbsp;🇬🇧 [English](README.en.md)

# 🎬 Movies API — Web API en ASP.NET Core

API REST construida con **ASP.NET Core (.NET 10)** y **Entity Framework Core** para administrar un catálogo de películas: géneros, actores, cines con ubicación geográfica, películas y calificaciones de usuarios. Es el backend de dos clientes que la consumen:

- [React_Movies](https://github.com/iliana212/React_Movies) — React 19 + TypeScript
- [Angular_Movies](https://github.com/iliana212/Angular_Movies) — Angular 21

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet) ![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core-CC2927?logo=microsoftsqlserver&logoColor=white) ![JWT](https://img.shields.io/badge/Auth-JWT-000000?logo=jsonwebtokens) ![Swagger](https://img.shields.io/badge/Docs-Swagger-85EA2D?logo=swagger&logoColor=black) ![Serilog](https://img.shields.io/badge/Logs-Serilog-4B5563)

> **Estado:** en desarrollo activo.

![Swagger UI](docs/swagger.png) ![Swagger UI](docs/swagger_2.png) ![Swagger UI](docs/swagger_3.png)

## En un vistazo

- 6 recursos (géneros, actores, cines, películas, calificaciones y usuarios) expuestos con controladores REST, **DTOs** y **AutoMapper**
- Autenticación **JWT** con ASP.NET Core Identity y autorización basada en claims (`esadmin`)
- **Datos geoespaciales** con NetTopologySuite para la ubicación de los cines
- Carga de imágenes (posters de películas y fotos de actores) con almacenamiento **local** o **Azure Blob Storage**, intercambiable mediante una interfaz
- Filtrado de películas, paginación, **caché de salida** con tags y documentación **Swagger / OpenAPI**
- **Preparada para producción:** rate limiting en autenticación, manejo global de errores con `ProblemDetails`, health checks y logging estructurado con Serilog

## Arquitectura

```mermaid
graph LR
    React["React_Movies<br/><sub>React 19 · Vite</sub>"] --> API
    Angular["Angular_Movies<br/><sub>Angular 21</sub>"] --> API
    API["Movies API<br/><sub>ASP.NET Core · JWT · Rate Limiting</sub>"] --> DB[("SQL Server<br/><sub>EF Core · NetTopologySuite</sub>")]
    API --> Storage[("Imágenes<br/><sub>wwwroot / Azure Blob</sub>")]
    API -. "logs" .-> Logs[("Logs<br/><sub>Serilog · consola + archivo</sub>")]
    Monitor{{"Monitoreo / orquestador"}} -. "/health/live · /health/ready" .-> API
```

## Endpoints principales

| Recurso | Rutas | Acceso |
| --- | --- | --- |
| Géneros | `api/generos` (CRUD) · `GET api/generos/todos` | Admin (`todos` es público) |
| Actores | `api/actores` (CRUD) · `GET api/actores/{nombre}` (búsqueda por nombre) | Admin |
| Cines | `api/cines` (CRUD) | Admin |
| Películas (lectura) | `GET api/peliculas/landing` · `GET api/peliculas/{id}` · `GET api/peliculas/filtrar` | Público |
| Películas (gestión) | `POST` / `PUT` / `DELETE api/peliculas` · `GET PostGet` · `GET PutGet/{id}` | Admin |
| Calificaciones | `POST api/rating` | Usuario autenticado |
| Usuarios | `POST api/usuarios/registrar` · `POST api/usuarios/login` (con límite de peticiones) | Público |
| Administración de usuarios | `GET ListadoUsuarios` · `POST HacerAdmin` · `POST RemoverAdmin` | Admin |
| Salud | `GET /health/live` · `GET /health/ready` | Público |

- **Landing:** películas en cines y próximos estrenos.
- **Detalle:** incluye el promedio de calificaciones y el voto del usuario autenticado.
- **Filtro:** por título, género, en cines y próximos estrenos, con paginación (máximo 50 registros por página).
- El total de registros se devuelve en el encabezado `cantidad-total-registros`.

## Funcionalidades de producción

Más allá de los endpoints, estas son las piezas que hacen la API más robusta y fácil de operar:

- **Rate limiting en autenticación** — `POST api/usuarios/login` admite 5 intentos por minuto por IP y `POST api/usuarios/registrar` 3 cada 10 minutos (ventana fija). Al excederlo responde `429` con el encabezado `Retry-After`. Reduce el riesgo de fuerza bruta y de creación masiva de cuentas.
- **Manejo global de excepciones** — un `IExceptionHandler` centralizado devuelve todos los errores en formato estándar `ProblemDetails`: violaciones de unicidad → `409`, referencias inválidas (llave foránea) → `400`, conflictos de concurrencia → `409` y errores inesperados → `500` con un mensaje genérico (el detalle real solo se muestra en Development). Cada respuesta incluye un `traceId` para correlacionarla con los logs.
- **Health checks** — `/health/live` (liveness: el proceso está vivo, sin consultar dependencias) y `/health/ready` (readiness: verifica la conexión a SQL Server mediante EF Core). Responden JSON con el estado de cada check, sin exponer mensajes de excepción. Es el patrón que esperan Docker y Kubernetes.
- **Logging estructurado con Serilog** — salida a consola y a un archivo rotativo diario (`logs/api-.log`, 14 días de retención). Cada petición registra un resumen con su `RequestId` y el usuario autenticado; ese `RequestId` coincide con el `traceId` de las respuestas de error, de modo que un error reportado por un cliente se puede rastrear hasta su línea de log. Los niveles se configuran en la sección `Serilog` de `appsettings.json`.

## Tecnologías

| Área | Tecnología |
| --- | --- |
| Framework | ASP.NET Core Web API, .NET 10 |
| Acceso a datos | Entity Framework Core 10, SQL Server, migraciones code-first |
| Datos espaciales | NetTopologySuite (SRID 4326) |
| Seguridad | ASP.NET Core Identity, JWT Bearer, Rate Limiting |
| Manejo de errores | `IExceptionHandler` + `ProblemDetails` |
| Observabilidad | Serilog (consola + archivo), ASP.NET Core Health Checks |
| Mapeo | AutoMapper |
| Almacenamiento | Sistema de archivos local / Azure Blob Storage |
| Documentación | Swashbuckle (Swagger UI) |

## Estructura del proyecto

```
├── Controllers/      # Endpoints de la API
├── DTOs/             # Objetos de transferencia de datos
├── Entidades/        # Entidades de EF Core
├── Migrations/       # Migraciones de base de datos
├── Servicios/        # Servicios (usuarios, almacenamiento de archivos)
├── Utilidades/       # Perfiles de AutoMapper, política de caché, manejador global de excepciones, extensiones
├── Validaciones/     # Atributos de validación personalizados
├── logs/             # Archivos de log generados por Serilog (no se versionan)
├── wwwroot/          # Archivos estáticos / imágenes subidas
├── ApplicationDBContext.cs
└── Program.cs        # Registro de servicios y pipeline HTTP
```

## Cómo ejecutarlo

### Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, Docker o una instancia completa)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Clonar el repositorio

```bash
git clone https://github.com/iliana212/API_Movies.git
cd API_Movies
```

### 2. Configurar los secretos

`appsettings.json` no contiene cadenas de conexión ni llaves. Configúralas con [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (o con un `appsettings.Development.json` que **no** se suba al repositorio):

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=PeliculasDB;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Issuer" "https://localhost"
dotnet user-secrets set "Jwt:Audience" "https://localhost"
dotnet user-secrets set "Jwt:llavejwt" "<una-cadena-aleatoria-de-al-menos-32-caracteres>"
dotnet user-secrets set "origenesPermitidos" "http://localhost:5173,http://localhost:4200"
```

| Clave | Para qué sirve |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server |
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y audiencia esperados de los tokens |
| `Jwt:llavejwt` | Llave simétrica para firmar los tokens JWT |
| `origenesPermitidos` | Orígenes permitidos por CORS, separados por coma (React en `5173`, Angular en `4200` por defecto) |
| `ConnectionStrings:AzureStorageConnection` | Solo si usas Azure Blob Storage |

### 3. Crear la base de datos

```bash
dotnet ef database update
```

### 4. Ejecutar la API

```bash
dotnet run --launch-profile https
```

Swagger UI queda disponible en `https://localhost:7263/swagger`. Los logs se escriben en consola y en la carpeta `logs/`.

### 5. Verificar que la API está sana

```bash
curl -k https://localhost:7263/health/live
curl -k https://localhost:7263/health/ready
```

`live` responde `200` mientras el proceso esté vivo; `ready` responde `200` si la base de datos es accesible y `503` si no lo es.

### 6. Crear el primer administrador

Los endpoints de gestión requieren el claim `esadmin`, y solo un administrador puede otorgarlo. Para el primer usuario:

1. Regístralo con `POST api/usuarios/registrar` desde Swagger.
2. Asígnale el claim directamente en la base de datos:

```sql
INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
SELECT Id, 'esadmin', 'true' FROM AspNetUsers WHERE Email = 'tu-correo@ejemplo.com';
```

A partir de ahí, ese administrador puede promover a otros usuarios con `POST api/usuarios/HacerAdmin`.

### Autenticación en Swagger

Inicia sesión con `POST api/usuarios/login`, copia el token, haz clic en **Authorize** y pégalo.

### Cambiar el almacenamiento a Azure Blob Storage

Por defecto las imágenes se guardan en `wwwroot`. Para usar Azure, cambia en `Program.cs` el registro de `IAlmacenadorArchivos` de `AlmacenadorArchivosLocal` a `AlmacenadorArchivosAzure` y agrega la cadena `ConnectionStrings:AzureStorageConnection`.

## Proyectos relacionados

| Proyecto | Descripción |
| --- | --- |
| [React_Movies](https://github.com/iliana212/React_Movies) | Cliente en React + TypeScript |
| [Angular_Movies](https://github.com/iliana212/Angular_Movies) | Cliente en Angular |

## Autora

**Iliana Barron** — [@iliana212](https://github.com/iliana212)
