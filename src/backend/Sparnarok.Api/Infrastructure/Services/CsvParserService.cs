using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación genérica y resiliente para el parseo de archivos CSV basada en <see cref="CsvReader"/>.
/// </summary>
public class CsvParserService : ICsvParserService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TRecord>> ParseCsvAsync<TRecord, TMap>(
        Stream stream,
        CancellationToken cancellationToken = default)
        where TMap : ClassMap<TRecord>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim(),
        };

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<TMap>();

        var records = new List<TRecord>();
        await foreach (var record in csv.GetRecordsAsync<TRecord>(cancellationToken))
        {
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }
}
