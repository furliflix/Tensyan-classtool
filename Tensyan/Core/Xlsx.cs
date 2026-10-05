using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tensyan.Core
{
    /// <summary>零依赖的最小 XLSX 导出（内联字符串 + 表头样式 + 列宽）。</summary>
    public static class Xlsx
    {
        public static bool Write(string path, string sheetName, string[] headers, List<string[]> rows, int[] widths = null, string title = null)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (var zip = new ZipWriter(fs, true))
                {
                    AddEntry(zip, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                        "</Types>");

                    AddEntry(zip, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "</Relationships>");

                    AddEntry(zip, "xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                        "<sheets><sheet name=\"" + Esc(Str.Blank(sheetName) ? "Sheet1" : sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

                    AddEntry(zip, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                        "</Relationships>");

                    AddEntry(zip, "xl/styles.xml", StylesXml());

                    AddEntry(zip, "xl/worksheets/sheet1.xml", SheetXml(headers, rows, widths, title));
                }
                return true;
            }
            catch (Exception ex)
            {
                Paths.Log("导出 Excel 失败: " + ex.Message);
                return false;
            }
        }

        private static void AddEntry(ZipWriter zip, string name, string content)
        {
            using (var s = zip.CreateEntry(name))
            using (var w = new StreamWriter(s, new UTF8Encoding(false)))
                w.Write(content);
        }

        private static string StylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"4\">" +
                "<font><sz val=\"11\"/><name val=\"微软雅黑\"/></font>" +
                "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"微软雅黑\"/></font>" +
                "<font><b/><sz val=\"14\"/><name val=\"微软雅黑\"/></font>" +
                "<font><sz val=\"11\"/><color rgb=\"FF1F7A3D\"/><name val=\"微软雅黑\"/></font>" +
                "</fonts>" +
                "<fills count=\"4\">" +
                "<fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF2F6BFF\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF2F6FF\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "</fills>" +
                "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
                "<border><left style=\"thin\"><color rgb=\"FFD6DCE8\"/></left><right style=\"thin\"><color rgb=\"FFD6DCE8\"/></right><top style=\"thin\"><color rgb=\"FFD6DCE8\"/></top><bottom style=\"thin\"><color rgb=\"FFD6DCE8\"/></bottom><diagonal/></border>" +
                "</borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"5\">" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "</cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"常规\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                "</styleSheet>";
        }

        private static string SheetXml(string[] headers, List<string[]> rows, int[] widths, string title)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            if (widths != null && widths.Length > 0)
            {
                sb.Append("<cols>");
                for (int i = 0; i < widths.Length; i++)
                    sb.Append("<col min=\"" + (i + 1) + "\" max=\"" + (i + 1) + "\" width=\"" + widths[i] + "\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }

            sb.Append("<sheetData>");
            int r = 1;
            if (!string.IsNullOrEmpty(title))
            {
                sb.Append("<row r=\"" + r + "\" ht=\"22\" customHeight=\"1\">");
                sb.Append(Cell("A" + r, title, 3));
                sb.Append("</row>");
                r++;
                sb.Append("<row r=\"" + r + "\"/>");
                r++;
            }
            if (headers != null && headers.Length > 0)
            {
                sb.Append("<row r=\"" + r + "\" ht=\"20\" customHeight=\"1\">");
                for (int c = 0; c < headers.Length; c++)
                    sb.Append(Cell(ColName(c) + r, headers[c], 1));
                sb.Append("</row>");
                r++;
            }
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    sb.Append("<row r=\"" + r + "\">");
                    for (int c = 0; c < row.Length; c++)
                        sb.Append(Cell(ColName(c) + r, row[c], 2));
                    sb.Append("</row>");
                    r++;
                }
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string Cell(string reference, string value, int style)
        {
            string v = value ?? "";
            bool numeric = v.Length > 0 && IsNumber(v);
            var sb = new StringBuilder();
            sb.Append("<c r=\"" + reference + "\" s=\"" + style + "\"");
            if (numeric) sb.Append("><v>" + v + "</v></c>");
            else sb.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + Esc(v) + "</t></is></c>");
            return sb.ToString();
        }

        private static bool IsNumber(string s)
        {
            if (s.Length == 0 || s.Length > 15) return false;
            bool dot = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '-' && i == 0) continue;
                if (c == '.' && !dot) { dot = true; continue; }
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        private static string ColName(int index)
        {
            string s = "";
            index++;
            while (index > 0)
            {
                int m = (index - 1) % 26;
                s = (char)('A' + m) + s;
                index = (index - m) / 26;
            }
            return s;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    case '\r': break;
                    case '\n': sb.Append("&#10;"); break;
                    default:
                        if (c < 0x20) { } else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
