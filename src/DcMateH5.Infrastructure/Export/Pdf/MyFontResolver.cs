using PdfSharp.Fonts;
using System;
using System.IO;

public class MyFontResolver : IFontResolver
{
    // 1. 決定字體名稱對應到哪個實體檔案
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // 不管程式要什麼字體（中文、英文、粗體），通通給它最穩的 arial.ttf
        // 這是為了測試「流程」是否打通。如果這關過了，我們再處理中文字體檔案。
        return new FontResolverInfo("arial.ttf");
    }

    public byte[] GetFont(string faceName)
    {
        // 直接去 Windows 資料夾抓最標準的 Arial
        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

        if (File.Exists(fontPath))
        {
            return File.ReadAllBytes(fontPath);
        }
        throw new Exception("連 arial.ttf 都找不到，系統路徑有問題！");
    }
}