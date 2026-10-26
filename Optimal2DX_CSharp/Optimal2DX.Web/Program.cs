using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Optimal2DX.Web.Models;
using Optimal2DX.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<CuttingOptimizationService>();
builder.Services.AddSingleton<ExcelService>();
builder.Services.AddSingleton<HtmlReportService>();

// Configure CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// 1. Optimize API Endpoint
app.MapPost("/api/optimize", (OptimizationRequestDto req, CuttingOptimizationService optService) =>
{
    try
    {
        var result = optService.Optimize(req);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, errorMessage = ex.Message });
    }
});

// 2. Generate HTML Report
app.MapPost("/api/report/html", (OptimizationRequestDto req, CuttingOptimizationService optService, HtmlReportService reportService) =>
{
    try
    {
        var result = optService.Optimize(req);
        if (!result.Success)
        {
            return Results.BadRequest(new { success = false, errorMessage = result.ErrorMessage });
        }
        string html = reportService.GenerateHtmlReport(req, result);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(html);
        return Results.File(bytes, "text/html; charset=utf-8", "BaoCao_SoDoCat_Cut2DX.html");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, errorMessage = ex.Message });
    }
});

// 3. Download Excel Sample Template
app.MapGet("/api/excel/template", (ExcelService excelService) =>
{
    byte[] bytes = excelService.GenerateTemplateExcel();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Nhap_Lieu_Cut2DX.xlsx");
});

// 4. Import from Excel / CSV
app.MapPost("/api/excel/import", async (HttpRequest request, ExcelService excelService) =>
{
    if (!request.HasFormContentType || request.Form.Files.Count == 0)
    {
        return Results.BadRequest(new { success = false, message = "Vui lòng chọn file Excel hoặc CSV để tải lên!" });
    }

    var file = request.Form.Files[0];
    if (file.Length == 0)
    {
        return Results.BadRequest(new { success = false, message = "File tải lên không có dữ liệu!" });
    }

    using var stream = file.OpenReadStream();
    var importResult = excelService.ImportExcel(stream);
    return Results.Ok(importResult);
});

// 5. Export Optimization Results to Excel
app.MapPost("/api/excel/export-results", (OptimizationResultDto result, ExcelService excelService) =>
{
    try
    {
        byte[] bytes = excelService.ExportResultsToExcel(result);
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Ket_Qua_Toi_Uu_Cut2DX.xlsx");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();
