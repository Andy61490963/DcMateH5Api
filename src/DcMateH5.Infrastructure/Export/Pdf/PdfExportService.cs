using DcMateH5.Abstractions.Export.Pdf;
// 3. 引用你的 Abstractions
using DcMateH5.Abstractions.Export.Pdf.Models;
// 2. 【補足缺失的枚舉】RelativeVertical/Horizontal 在 Shapes 命名空間下
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes; // <--- 補上這行，QR Code 定位就不會報錯了
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// 1. 【解決模稜兩可】使用別名 (Alias) 指向正確的類別
using Document = MigraDoc.DocumentObjectModel.Document;
using Section = MigraDoc.DocumentObjectModel.Section;

namespace DcMateH5.Infrastructure.Export.Pdf
{
    public class PdfExportService : IPdfExportService
    {
        public byte[] GenerateGridTableReport(GridReportRequest request)
        {
            // 1. 強制從 runtimes 路徑載入 Native DLL (解決 SkiaSharp 找不到眼睛的問題)
            string nativeDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "runtimes", "win-x64", "native", "libSkiaSharp.dll");
            if (File.Exists(nativeDllPath))
            {
                NativeLibrary.Load(nativeDllPath);
            }

            using var temp = new SkiaSharp.SKBitmap(1, 1);
            // 強制再次確認註冊，避免 Program.cs 沒跑到的情況
            if (GlobalFontSettings.FontResolver is not MyFontResolver)
            {
                GlobalFontSettings.FontResolver = new MyFontResolver();
            }

            MigraDoc.DocumentObjectModel.Document document = new Document();

            // ============================================================
            // 【新增】設定全域預設字體，這會與你的 MyFontResolver 對接
            // ============================================================
            var style = document.Styles["Normal"];
            style.Font.Name = "Microsoft JhengHei"; // 必須跟 Resolver 裡寫的名字一模一樣
            style.Font.Size = 9;
            // ============================================================

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
                            // 確保 Text 真的有東西，至少給個空格
                            if (string.IsNullOrEmpty(c.Text))
                            {
                                // 某些版本在空 Paragraph 上會噴錯，給它一個隱形字元測試
                                p.AddText(" ");
                            }

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
                if (qrBytes != null && qrBytes.Length > 0)
                {
                    // 去除所有可能干擾的換行符號
                    string base64Data = Convert.ToBase64String(qrBytes).Replace("\n", "").Replace("\r", "");

                    // 餵給我們剛才驗證成功的 base64 協定
                    var img = section.AddImage("base64:" + base64Data);

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
            // 1. 先用 QRCode 產生 Bitmap (這是最標準的 GDI+ 物件)
            using (QRCode qr = new QRCode(qd))
            using (System.Drawing.Bitmap bmp = qr.GetGraphic(5)) // 解析度設為 5 就夠了
            using (MemoryStream ms = new MemoryStream())
            {
                // 2. 強迫透過 System.Drawing 將 Bitmap 重新編碼為標準 PNG
                // 這會補齊所有 SkiaSharp 期待的 Header 資訊
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private ParagraphAlignment GetAlignment(string align)
        {
            if (align == "Left") return ParagraphAlignment.Left;
            if (align == "Right") return ParagraphAlignment.Right;
            return ParagraphAlignment.Center;
        }
    }
}