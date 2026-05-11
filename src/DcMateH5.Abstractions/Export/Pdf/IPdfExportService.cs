using DcMateH5.Abstractions.Export.Pdf.Models;

namespace DcMateH5.Abstractions.Export.Pdf
{
    public interface IPdfExportService
    {
        byte[] GenerateGridTableReport(GridReportRequest request);
    }
}