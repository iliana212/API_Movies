global using System.Net;
global using System.Net.Http.Json;
global using System.Text.Json;
global using Xunit;

// Las pruebas de integración comparten un único contenedor de SQL Server y
// variables de entorno del proceso, por eso se ejecutan de forma secuencial.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
