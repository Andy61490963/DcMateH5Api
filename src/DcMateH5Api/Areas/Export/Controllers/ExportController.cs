using Microsoft.AspNetCore.Mvc;
using DcMateH5.Abstractions.Export.Pdf;
using DcMateH5.Abstractions.Export.Pdf.Models;

namespace DcMateH5Api.Areas.Export.Controllers
{
    [Area("Export")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly IPdfExportService _pdfService;

        public ExportController(IPdfExportService pdfService)
        {
            _pdfService = pdfService;
        }

        [HttpPost("PdfGrid")]
        public IActionResult ExportGridPdf([FromBody] GridReportRequest request)
        {
            if (request == null) return BadRequest("Request Body is empty.");

            var pdfBytes = _pdfService.GenerateGridTableReport(request);
            string fileName = string.IsNullOrEmpty(request.ReportTitle) ? "Export.pdf" : $"{request.ReportTitle}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}