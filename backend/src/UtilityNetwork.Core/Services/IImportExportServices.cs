using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IExportService
{
    Task<(byte[] Bytes, string ContentType, string FileName)> ExportGeoJsonAsync(
        ExportFilterRequest filter,
        ClaimsPrincipal user,
        CancellationToken ct = default);

    Task<(byte[] Bytes, string ContentType, string FileName)> ExportCsvAsync(
        ExportFilterRequest filter,
        ClaimsPrincipal user,
        CancellationToken ct = default);
}

public interface IImportPreviewStore
{
    void Store(Guid previewToken, IReadOnlyList<ParsedImportFeature> features, int sourceSrid);
    (IReadOnlyList<ParsedImportFeature> Features, int SourceSrid)? Retrieve(Guid previewToken);
    void Remove(Guid previewToken);
}

public interface IImportService
{
    Task<ImportPreviewResultDto> PreviewImportAsync(
        ImportPreviewRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default);

    Task<ImportCommitResultDto> CommitImportAsync(
        ImportCommitRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default);
}
