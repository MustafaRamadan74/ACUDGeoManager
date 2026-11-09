using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class ReportService : IReportService
{
    private readonly string _connectionString;
    private readonly IAnalysisService _analysisService;

    static ReportService()
    {
        // Explicit License compliance check for QuestPDF
        // QuestPDF Community License is free for open-source and organizations with revenue under $1M USD.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportService(IOptions<DatabaseOptions> dbOptions, IAnalysisService analysisService)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
        _analysisService = analysisService;
    }

    #region Workspace Summary Report

    public async Task<GeneratedReportResult> GenerateWorkspaceSummaryReportAsync(WorkspaceSummaryReportRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string netSql = "SELECT name, code, domain, description FROM networks WHERE id = @networkId;";
        var net = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(netSql, new { networkId = request.NetworkId }, cancellationToken: ct));
        var networkName = net != null ? (string)net.name : "ACUD Network";
        var networkCode = net != null ? (string)net.code : "NET";

        var stats = await _analysisService.GetNetworkStatisticsAsync(request.NetworkId, ct);

        if (request.Format == ReportFormat.Csv)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Layer Code,Layer Name,Geometry,Feature Count,Total Length (m),Total Area (sq m)");
            foreach (var item in stats.ByType)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(item.FeatureTypeCode),
                    EscapeCsv(item.DisplayName),
                    EscapeCsv(item.GeometryKind),
                    item.Count.ToString(CultureInfo.InvariantCulture),
                    item.TotalLengthMeters.ToString("F2", CultureInfo.InvariantCulture),
                    item.TotalAreaSqM.ToString("F2", CultureInfo.InvariantCulture)
                ));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new GeneratedReportResult(bytes, "text/csv; charset=utf-8", $"{networkCode}_Summary_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // PDF Generation
        byte[]? mapImageBytes = ParseBase64Image(request.MapSnapshotBase64);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, "Workspace Summary Report | تقرير ملخص شبكة المرافق", $"{networkName} ({networkCode})"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    // Key KPI Cards
                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "إجمالي المعالم | Total Assets", stats.TotalFeatures.ToString("#,##0", CultureInfo.InvariantCulture), Colors.Blue.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "أطوال الشبكة | Total Length", $"{stats.TotalPipeLengthMeters:N1} m", Colors.Teal.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "المساحات الكلية | Total Area", $"{stats.TotalAreaSqM:N1} m²", Colors.Amber.Darken2));
                    });

                    // Optional Map Snapshot
                    if (mapImageBytes != null && mapImageBytes.Length > 0)
                    {
                        col.Item().Column(imgCol =>
                        {
                            imgCol.Item().Text("لقطة الخريطة الميدانية | Map View Snapshot").Bold().FontSize(11).FontColor(Colors.Grey.Darken2);
                            imgCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2).MaxHeight(200).Image(mapImageBytes).FitArea();
                        });
                    }

                    // Table: Feature Types Breakdown
                    col.Item().Text("توزيع الطبقات والمعالم الجغرافية | Feature Layer Breakdown").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الكود (Code)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("اسم الطبقة (Layer Name)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("النوع (Kind)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("العدد (Count)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الطول / المساحة").Bold();
                        });

                        foreach (var ft in stats.ByType)
                        {
                            var metric = ft.TotalLengthMeters > 0 ? $"{ft.TotalLengthMeters:N1} m" : ft.TotalAreaSqM > 0 ? $"{ft.TotalAreaSqM:N1} m²" : "-";
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(ft.FeatureTypeCode).FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(ft.DisplayName);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(ft.GeometryKind).FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(ft.Count.ToString(CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(metric);
                        }
                    });

                    // Summary of Pipes by Diameter
                    if (stats.PipesByDiameter.Count > 0)
                    {
                        col.Item().Text("توزيع خطوط الأنابيب حسب القطر | Pipes by Diameter").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("القطر (Diameter)").Bold();
                                header.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("العدد (Segments)").Bold();
                                header.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("الطول الإجمالي (Length)").Bold();
                            });

                            foreach (var p in stats.PipesByDiameter)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(p.DiameterMm > 0 ? $"Ø {p.DiameterMm} mm" : "Not Specified");
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(p.Count.ToString(CultureInfo.InvariantCulture));
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text($"{p.TotalLengthMeters:N1} m");
                            }
                        });
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = doc.GeneratePdf();
        return new GeneratedReportResult(pdfBytes, "application/pdf", $"{networkCode}_Summary_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    #endregion

    #region Validation Report

    public async Task<GeneratedReportResult> GenerateValidationReportAsync(ValidationReportRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT v.id, v.rule_code AS RuleCode, v.severity AS Severity, v.asset_name AS AssetName,
                   v.message AS Message, v.status AS Status, v.network_id AS NetworkId, v.created_at AS CreatedAt
            FROM validation_issues v
            WHERE (@networkId IS NULL OR v.network_id = @networkId)
              AND (@severity IS NULL OR v.severity = @severity)
            ORDER BY 
              CASE v.severity WHEN 'ERROR' THEN 1 WHEN 'WARNING' THEN 2 ELSE 3 END,
              v.created_at DESC;";

        var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
        {
            networkId = request.NetworkId,
            severity = request.Severity
        }, cancellationToken: ct))).ToList();

        if (request.Format == ReportFormat.Csv)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Rule Code,Severity,Asset Name,Message,Status,Created At");
            foreach (var r in rows)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv((string)r.rulecode),
                    EscapeCsv((string)r.severity),
                    EscapeCsv((string)r.assetname),
                    EscapeCsv((string)r.message),
                    EscapeCsv((string)r.status),
                    ((DateTime)r.createdat).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                ));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new GeneratedReportResult(bytes, "text/csv; charset=utf-8", $"Validation_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // PDF Report
        byte[]? mapImageBytes = ParseBase64Image(request.MapSnapshotBase64);
        int errorsCount = rows.Count(r => (string)r.severity == "ERROR");
        int warningsCount = rows.Count(r => (string)r.severity == "WARNING");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, "Spatial Topology Validation Report | تقرير التدقيق الطوبولوجي والمكاني", $"Total Issues: {rows.Count}"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "أخطاء حرجة | Errors", errorsCount.ToString(CultureInfo.InvariantCulture), Colors.Red.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "تحذيرات طوبولوجية | Warnings", warningsCount.ToString(CultureInfo.InvariantCulture), Colors.Amber.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "إجمالي المشكلات | Total Issues", rows.Count.ToString(CultureInfo.InvariantCulture), Colors.Blue.Darken2));
                    });

                    if (mapImageBytes != null && mapImageBytes.Length > 0)
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).MaxHeight(180).Image(mapImageBytes).FitArea();
                    }

                    col.Item().Text("سجل المشكلات الطوبولوجية المكتشفة | Detected Validation Issues").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(2.5f);
                            columns.RelativeColumn(4.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("القاعدة (Rule)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الدرجة (Severity)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("اسم المعلم (Asset)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("وصف المشكلة (Message)").Bold();
                        });

                        foreach (var r in rows)
                        {
                            var isErr = (string)r.severity == "ERROR";
                            var color = isErr ? Colors.Red.Darken2 : Colors.Amber.Darken2;

                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.rulecode).FontSize(9).Bold();
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.severity).FontColor(color).Bold().FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.assetname);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.message).FontSize(9);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        var pdf = doc.GeneratePdf();
        return new GeneratedReportResult(pdf, "application/pdf", $"Validation_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    #endregion

    #region Feature History Report

    public async Task<GeneratedReportResult> GenerateFeatureHistoryReportAsync(FeatureHistoryReportRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string featSql = @"
            SELECT id, name, feature_type_code, status, ST_AsGeoJSON(geom) AS geom_geojson
            FROM (
                SELECT id, name, feature_type_code, status, geom FROM points WHERE id = @featureId
                UNION ALL
                SELECT id, name, feature_type_code, status, geom FROM lines WHERE id = @featureId
                UNION ALL
                SELECT id, name, feature_type_code, status, geom FROM polygons WHERE id = @featureId
            ) f LIMIT 1;";

        var feat = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(featSql, new { featureId = request.FeatureId }, cancellationToken: ct));
        var featureName = feat != null ? (string)feat.name : request.FeatureId.ToString();
        var featureType = feat != null ? (string)feat.feature_type_code : "Asset";

        const string historySql = @"
            SELECT al.id, al.operation, al.created_at AS CreatedAt, u.username AS EditorUsername,
                   al.new_state::text AS NewStateJson, al.diff::text AS DiffJson
            FROM audit_log al
            LEFT JOIN users u ON u.id = al.user_id
            WHERE al.feature_id = @featureId
            ORDER BY al.created_at DESC;";

        var historyRows = (await conn.QueryAsync<dynamic>(new CommandDefinition(historySql, new { featureId = request.FeatureId }, cancellationToken: ct))).ToList();

        if (request.Format == ReportFormat.Csv)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Operation,Editor,Feature ID,Feature Name,Diff");
            foreach (var r in historyRows)
            {
                csv.AppendLine(string.Join(",",
                    ((DateTime)r.createdat).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    EscapeCsv((string)r.operation),
                    EscapeCsv(r.editorusername ?? "System"),
                    request.FeatureId.ToString(),
                    EscapeCsv(featureName),
                    EscapeCsv(r.diffjson ?? "")
                ));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new GeneratedReportResult(bytes, "text/csv; charset=utf-8", $"Feature_{request.FeatureId}_History_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // PDF Generation
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, "Asset Audit History Report | تقرير سجل التدقيق التاريخي للمعلم", $"{featureName} ({featureType})"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "معرف الأصل | Feature ID", string.Concat(request.FeatureId.ToString().AsSpan(0, 8), "..."), Colors.Blue.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "نوع المعلم | Layer Type", featureType, Colors.Teal.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "عدد التعديلات | Total Edits", historyRows.Count.ToString(CultureInfo.InvariantCulture), Colors.Purple.Darken2));
                    });

                    col.Item().Text("التسلسل الزمني للعمليات والتحويرات | Chronological Audit Trail").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.5f);
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(4.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("التاريخ والوقت (Date/Time)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("العملية (Action)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("المحرر (Editor)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("تفاصيل التغيير (Changes)").Bold();
                        });

                        foreach (var r in historyRows)
                        {
                            var op = (string)r.operation;
                            var color = op == "INSERT" ? Colors.Green.Darken2 : op == "UPDATE" ? Colors.Blue.Darken2 : Colors.Red.Darken2;

                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(((DateTime)r.createdat).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(op).Bold().FontColor(color);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)(r.editorusername ?? "System"));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)(r.diffjson ?? "-")).FontSize(8);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = doc.GeneratePdf();
        return new GeneratedReportResult(pdfBytes, "application/pdf", $"Feature_{request.FeatureId}_History_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    #endregion

    #region User Activity Report

    public async Task<GeneratedReportResult> GenerateUserActivityReportAsync(UserActivityReportRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT u.id AS UserId, u.username AS Username, u.role AS Role,
                   COUNT(al.id) AS TotalEdits,
                   COUNT(DISTINCT al.changeset_id) AS TotalChangesets,
                   MAX(al.created_at) AS LastActive
            FROM users u
            LEFT JOIN audit_log al ON al.user_id = u.id
              AND (@fromDate IS NULL OR al.created_at >= @fromDate)
              AND (@toDate IS NULL OR al.created_at <= @toDate)
            GROUP BY u.id, u.username, u.role
            ORDER BY TotalEdits DESC, u.username ASC;";

        var rows = (await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new
        {
            fromDate = request.FromDate,
            toDate = request.ToDate
        }, cancellationToken: ct))).ToList();

        if (request.Format == ReportFormat.Csv)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Username,Role,Total Edits,Total Changesets,Last Active");
            foreach (var r in rows)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv((string)r.username),
                    EscapeCsv((string)r.role),
                    r.totaledits.ToString(),
                    r.totalchangesets.ToString(),
                    r.lastactive != null ? ((DateTime)r.lastactive).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "-"
                ));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new GeneratedReportResult(bytes, "text/csv; charset=utf-8", $"User_Activity_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // PDF Generation
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, "User Activity & Audit Productivity Report | تقرير نشاط المستخدمين والمحررين", "ACUD GeoManager Team"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    int totalEdits = rows.Sum(r => (int)r.totaledits);
                    int activeUsers = rows.Count(r => (int)r.totaledits > 0);

                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "إجمالي العمليات | Total Operations", totalEdits.ToString(CultureInfo.InvariantCulture), Colors.Blue.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "المحررين النشطين | Active Editors", activeUsers.ToString(CultureInfo.InvariantCulture), Colors.Green.Darken2));
                        r.RelativeItem().Element(c => ComposeKpiCard(c, "إجمالي الحسابات | Total Users", rows.Count.ToString(CultureInfo.InvariantCulture), Colors.Grey.Darken2));
                    });

                    col.Item().Text("بيانات نشاط المستخدمين في المنصة | Users Audit Metrics").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("اسم المستخدم (Username)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الدور (Role)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("العمليات (Edits)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("حزم التعديل (Changesets)").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("آخر نشاط (Last Active)").Bold();
                        });

                        foreach (var r in rows)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.username).Bold();
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text((string)r.role).FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(((long)r.totaledits).ToString(CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(((long)r.totalchangesets).ToString(CultureInfo.InvariantCulture));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.lastactive != null ? ((DateTime)r.lastactive).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "-").FontSize(9);
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = doc.GeneratePdf();
        return new GeneratedReportResult(pdfBytes, "application/pdf", $"User_Activity_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    #endregion

    #region Generic Analysis Widget Export (Buffer, Measure, Spatial Join, Stats)

    public Task<GeneratedReportResult> GenerateAnalysisWidgetReportAsync(AnalysisWidgetExportRequest request, CancellationToken ct = default)
    {
        if (request.Format == ReportFormat.Csv)
        {
            var csv = new StringBuilder();
            if (request.Headers.Count > 0)
            {
                csv.AppendLine(string.Join(",", request.Headers.Select(EscapeCsv)));
            }

            foreach (var row in request.Rows)
            {
                csv.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            var sanitizedTitle = SanitizeFileName(request.Title);
            return Task.FromResult(new GeneratedReportResult(bytes, "text/csv; charset=utf-8", $"{sanitizedTitle}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"));
        }

        // PDF Generation
        byte[]? mapImageBytes = ParseBase64Image(request.MapSnapshotBase64);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, request.Title, $"Analysis: {request.AnalysisType} | ACUD Spatial Engine"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    if (!string.IsNullOrWhiteSpace(request.Description))
                    {
                        col.Item().Text(request.Description).FontSize(11).FontColor(Colors.Grey.Darken2);
                    }

                    if (mapImageBytes != null && mapImageBytes.Length > 0)
                    {
                        col.Item().Column(imgCol =>
                        {
                            imgCol.Item().Text("الخريطة المكانية للتحليل | Spatial Analysis View").Bold().FontSize(11).FontColor(Colors.Grey.Darken2);
                            imgCol.Item().Border(1).BorderColor(Colors.Grey.Lighten2).MaxHeight(220).Image(mapImageBytes).FitArea();
                        });
                    }

                    if (request.Headers.Count > 0 && request.Rows.Count > 0)
                    {
                        col.Item().Text("نتائج التحليل المكاني | Analysis Output Data").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (int i = 0; i < request.Headers.Count; i++)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var h in request.Headers)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(h).Bold();
                                }
                            });

                            foreach (var r in request.Rows)
                            {
                                foreach (var val in r)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(val);
                                }
                            }
                        });
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = doc.GeneratePdf();
        var fileName = $"{SanitizeFileName(request.Title)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return Task.FromResult(new GeneratedReportResult(pdfBytes, "application/pdf", fileName));
    }

    #endregion

    #region Layout Helpers

    private static void ComposeHeader(IContainer container, string title, string subtitle)
    {
        container.BorderBottom(1.5f).BorderColor(Colors.Blue.Darken3).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("العاصمة الإدارية للتنمية العمرانية | ACUD GeoManager").FontSize(9).FontColor(Colors.Grey.Darken1).Bold();
                col.Item().Text(title).FontSize(15).Bold().FontColor(Colors.Blue.Darken4);
                col.Item().Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(120).AlignRight().Column(col =>
            {
                col.Item().Text(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text("Official Report").FontSize(8).FontColor(Colors.Blue.Darken2).Bold();
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text("ACUD Geographic Information Systems Directorate | شركة العاصمة الإدارية للتنمية العمرانية").FontSize(8).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }

    private static void ComposeKpiCard(IContainer container, string label, string value, string color)
    {
        container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
            c.Item().Text(value).FontSize(16).Bold().FontColor(color);
        });
    }

    /// <summary>
    /// Escapes CSV field and protects against CSV Formula Injection (=, +, -, @, \t, \r).
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        var val = value;
        // Protect against CSV formula injection
        if (val.StartsWith('=') || val.StartsWith('+') || val.StartsWith('-') || val.StartsWith('@') || val.StartsWith('\t') || val.StartsWith('\r'))
        {
            val = "'" + val;
        }

        if (val.Contains('"') || val.Contains(',') || val.Contains('\n') || val.Contains('\r'))
        {
            val = "\"" + val.Replace("\"", "\"\"") + "\"";
        }

        return val;
    }

    private static byte[]? ParseBase64Image(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;

        try
        {
            var commaIdx = base64.IndexOf(',', StringComparison.Ordinal);
            var clean = commaIdx >= 0 ? base64.Substring(commaIdx + 1) : base64;
            return Convert.FromBase64String(clean);
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) || c == ' ' ? '_' : c);
        }
        return sb.ToString();
    }

    #endregion
}
