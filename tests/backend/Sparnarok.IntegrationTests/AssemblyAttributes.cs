using Xunit;

// Deshabilita la ejecución paralela dentro del ensamblado de pruebas de integración
// para evitar condiciones de carrera en EnsureCreated y colisiones en la base de datos compartida de PostgreSQL.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
