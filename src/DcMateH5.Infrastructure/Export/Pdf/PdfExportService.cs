using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 1. 【解決模稜兩可】使用別名 (Alias) 指向正確的類別
using Document = MigraDoc.DocumentObjectModel.Document;
using Section = MigraDoc.DocumentObjectModel.Section;

// 2. 【補足缺失的枚舉】RelativeVertical/Horizontal 在 Shapes 命名空間下
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.DocumentObjectModel.Shapes; // <--- 補上這行，QR Code 定位就不會報錯了
using MigraDoc.Rendering;
using QRCoder;

// 3. 引用你的 Abstractions
using DcMateH5.Abstractions.Export.Pdf.Models;
using DcMateH5.Abstractions.Export.Pdf;

namespace DcMateH5.Infrastructure.Export.Pdf
{
    public class PdfExportService : IPdfExportService
    {
        public byte[] GenerateGridTableReport(GridReportRequest request)
        {
            MigraDoc.DocumentObjectModel.Document document = new Document();
            MigraDoc.DocumentObjectModel.Section section = document.AddSection();

            // 1. 紙張設定
            section.PageSetup.PageFormat = PageFormat.A4;
            bool isLandscape = request.Config?.Orientation?.Equals("Landscape", StringComparison.OrdinalIgnoreCase) == true;
            section.PageSetup.Orientation = isLandscape ? Orientation.Landscape : Orientation.Portrait;

            section.PageSetup.TopMargin = Unit.FromMillimeter(request.Config.TopMarginMm > 0 ? request.Config.TopMarginMm : 10);
            section.PageSetup.LeftMargin = Unit.FromMillimeter(10);
            section.PageSetup.RightMargin = Unit.FromMillimeter(10);

            // 2. 處理 QR Code
            ProcessQrCodes(section, request.Config.QrCodes);

            // 3. 寬度計算
            double pageWid = isLandscape ? 297 : 210;
            double usableWidthMm = pageWid - 20; // 扣掉左右邊距各 10

            // 4. 逐列處理表格 (支援分頁邏輯)
            if (request.Cells != null && request.Cells.Count > 0)
            {
                int maxRowsNeeded = request.Cells.Max(c => c.Y + c.H);
                double rowHeight = (request.Config.DefaultRowHeightMm > 0) ? request.Config.DefaultRowHeightMm : 8;

                Table table = section.AddTable();
                SetupTableColumns(table, request.Config.TotalColumns, usableWidthMm);

                var cellsByY = request.Cells.GroupBy(c => c.Y).ToDictionary(g => g.Key, g => g.ToList());

                for (int i = 0; i < maxRowsNeeded; i++)
                {
                    // 檢查換頁 Flag
                    if (request.Cells.Any(c => c.Y == i && c.IsPageBreak) && i > 0)
                    {
                        section.AddPageBreak();
                        table = section.AddTable();
                        SetupTableColumns(table, request.Config.TotalColumns, usableWidthMm);
                    }

                    Row row = table.AddRow();
                    row.Height = Unit.FromMillimeter(rowHeight);
                    row.VerticalAlignment = VerticalAlignment.Center;

                    if (cellsByY.ContainsKey(i))
                    {
                        foreach (var c in cellsByY[i])
                        {
                            Cell cell = row.Cells[c.X];
                            if (c.W > 1) cell.MergeRight = c.W - 1;
                            if (c.H > 1) cell.MergeDown = c.H - 1;

                            // 邊框控制
                            if (c.Borders != null)
                            {
                                cell.Borders.Left.Width = c.Borders.Left ? 0.5 : 0;
                                cell.Borders.Top.Width = c.Borders.Top ? 0.5 : 0;
                                cell.Borders.Bottom.Width = c.Borders.Bottom ? 0.5 : 0;
                                row.Cells[c.X + c.W - 1].Borders.Right.Width = c.Borders.Right ? 0.5 : 0;
                                if (!c.Borders.Right) cell.Borders.Right.Width = 0;
                            }

                            Paragraph p = cell.AddParagraph(c.Text ?? "");
                            p.Format.Font.Size = c.FontSize > 0 ? c.FontSize : 9;
                            p.Format.Font.Bold = c.IsBold;
                            p.Format.Alignment = GetAlignment(c.Align);
                        }
                    }
                }
            }

            PdfDocumentRenderer renderer = new PdfDocumentRenderer(true) { Document = document };
            renderer.RenderDocument();
            using (MemoryStream ms = new MemoryStream())
            {
                renderer.PdfDocument.Save(ms);
                return ms.ToArray();
            }
        }

        private void SetupTableColumns(Table table, double totalCols, double usableWidthMm)
        {
            table.Borders.Width = 0.5;
            double colWidth = usableWidthMm / (totalCols > 0 ? totalCols : 12);
            for (int i = 0; i < (totalCols > 0 ? totalCols : 12); i++)
                table.AddColumn(Unit.FromMillimeter(colWidth));
        }

        private void ProcessQrCodes(Section section, List<QrCodeConfig> qrCodes)
        {
            if (qrCodes == null) return;
            foreach (var qr in qrCodes.Where(x => x.Enabled && !string.IsNullOrEmpty(x.Value)))
            {
                byte[] qrBytes = CreateQrCodeBytes(qr.Value);
                if (qrBytes.Length > 0)
                {
                    var img = section.AddImage("base64:" + Convert.ToBase64String(qrBytes));
                    img.Width = Unit.FromMillimeter(qr.SizeMm);
                    img.RelativeVertical = RelativeVertical.Page;
                    img.RelativeHorizontal = RelativeHorizontal.Page;
                    img.Top = Unit.FromMillimeter(qr.YPosMm);
                    img.Left = Unit.FromMillimeter(qr.XPosMm);
                }
            }
        }

        private byte[] CreateQrCodeBytes(string text)
        {
            using (QRCodeGenerator qg = new QRCodeGenerator())
            using (QRCodeData qd = qg.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qr = new PngByteQRCode(qd)) return qr.GetGraphic(20);
        }

        private ParagraphAlignment GetAlignment(string align)
        {
            if (align == "Left") return ParagraphAlignment.Left;
            if (align == "Right") return ParagraphAlignment.Right;
            return ParagraphAlignment.Center;
        }
    }
}