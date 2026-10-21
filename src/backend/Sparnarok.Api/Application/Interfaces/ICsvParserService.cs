using CsvHelper.Configuration;

namespace Sparnarok.Api.Application.Interfaces;

/// <summary>
/// Contrato del servicio genérico de procesamiento y streaming de archivos delimitados por comas (CSV).
/// </summary>
public interface ICsvParserService
{
    /// <summary>
    /// Parsea de forma asíncrona y en streaming el contenido de un stream CSV hacia una colección tipada de registros.
    /// </summary>
    /// <typeparam name="TRecord">Tipo de objeto a instanciar por fila.</typeparam>
    /// <typeparam name="TMap">Clase de mapeo declarativo de CsvHelper.</typeparam>
    /// <param name="stream">Stream que contiene el archivo CSV.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de registros fuertemente tipados.</returns>
    Task<IReadOnlyList<TRecord>> ParseCsvAsync<TRecord, TMap>(
        Stream stream,
        CancellationToken cancellationToken = default)
        where TMap : ClassMap<TRecord>;
}
