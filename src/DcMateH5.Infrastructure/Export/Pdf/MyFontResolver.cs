using DcMateH5.Abstractions.Export.Pdf.Models;
using PdfSharp.Fonts;
using System;
using System.IO;

public class MyFontResolver : IFontResolver
{
    private readonly PdfExportOptions _options;

    public MyFontResolver(PdfExportOptions options)
    {
        _options = options ?? new PdfExportOptions();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // 預設檔名 (如果 JSON 沒讀到)
        string fallbackPath = isBold ? "msjhbd.ttc" : "msjh.ttc";

        if (_options?.FontFiles != null && _options.FontFiles.Any())
        {
            if (familyName.Equals(_options.FontFamilyName, StringComparison.OrdinalIgnoreCase))
            {
                string key = isBold ? "Bold" : "Regular";
                if (_options.FontFiles.ContainsKey(key))
                    return new FontResolverInfo(_options.FontFiles[key]); // 這裡回傳的是 JSON 裡的完整路徑
            }
        }
        return new FontResolverInfo(fallbackPath);
    }

    public byte[] GetFont(string faceName)
    {
        // 1. 如果 faceName 本身就是 JSON 給的完整路徑
        if (File.Exists(faceName))
        {
            byte[] data = File.ReadAllBytes(faceName);
            if (data.Length > 0) return data;
        }

        // 2. 如果 faceName 只是檔名，我們手動拼接 Areas\Font 路徑測試
        // 注意：路徑分隔符號在 Windows 建議用 Path.Combine
        string areaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Areas", "Font", Path.GetFileName(faceName));

        if (File.Exists(areaPath))
        {
            return File.ReadAllBytes(areaPath);
        }

        // 3. 【關鍵】如果都找不到，不要回傳 null，直接拋出 Exception 炸掉它
        // 這樣你就能看到到底是哪個路徑讀不到檔案，而不是看到 NullReferenceException
        throw new Exception($"[字體讀取失敗] 引擎找不到檔案：{faceName}。嘗試過的本地路徑：{areaPath}");
    }
}