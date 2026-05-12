using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DcMateH5.Abstractions.Export.Pdf.Models
{
    public class GridReportRequest
    {
        [JsonPropertyName("ReportTitle")]
        public string ReportTitle { get; set; }

        [JsonPropertyName("Config")]
        public GridConfig Config { get; set; }

        [JsonPropertyName("Cells")]
        public List<GridCell> Cells { get; set; }
    }

    public class GridConfig
    {
        [JsonPropertyName("totalColumns")]
        public double TotalColumns { get; set; } = 12;

        [JsonPropertyName("defaultRowHeightMm")]
        public double DefaultRowHeightMm { get; set; } = 8;

        [JsonPropertyName("orientation")]
        public string Orientation { get; set; } = "Portrait";

        [JsonPropertyName("TopMarginMm")]
        public double TopMarginMm { get; set; } = 10;

        [JsonPropertyName("LeftMarginMm")]
        public double LeftMarginMm { get; set; } = 10;

        [JsonPropertyName("QrCodes")]
        public List<QrCodeConfig> QrCodes { get; set; } = new List<QrCodeConfig>();
    }

    public class GridCell
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("w")]
        public int W { get; set; } = 1;

        [JsonPropertyName("h")]
        public int H { get; set; } = 1;

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 9;

        [JsonPropertyName("isBold")]
        public bool IsBold { get; set; } = false;

        [JsonPropertyName("align")]
        public string Align { get; set; } = "Center";

        [JsonPropertyName("borders")]
        public GridCellBorder? Borders { get; set; }

        [JsonPropertyName("IsPageBreak")]
        public bool IsPageBreak { get; set; } = false;
    }

    public class GridCellBorder
    {
        public bool Top { get; set; } = true;
        public bool Bottom { get; set; } = true;
        public bool Left { get; set; } = true;
        public bool Right { get; set; } = true;
    }

    public class QrCodeConfig
    {
        public bool Enabled { get; set; } = false;
        public string Value { get; set; }
        public double SizeMm { get; set; } = 20;
        public double XPosMm { get; set; } = 0;
        public double YPosMm { get; set; } = 0;
    }
}