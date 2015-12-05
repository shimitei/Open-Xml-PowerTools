﻿/***************************************************************************

Copyright (c) Microsoft Corporation 2012-2015.

This code is licensed using the Microsoft Public License (Ms-PL).  The text of the license can be found here:

http://www.microsoft.com/resources/sharedsource/licensingbasics/publiclicense.mspx

Published at http://OpenXmlDeveloper.org
Resource Center and Documentation: http://openxmldeveloper.org/wiki/w/wiki/powertools-for-open-xml.aspx

Developer: Eric White
Blog: http://www.ericwhite.com
Twitter: @EricWhiteDev
Email: eric@ericwhite.com

***************************************************************************/

#undef DisplayWorkingSet

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using OpenXmlPowerTools;
using System.Drawing;

namespace OpenXmlPowerTools
{
    // The classes in SpreadsheetWriter are still a work-in-progress.  While they are useful in their current state, I will be enhancing and
    // changing them in the future.  In particular, I will be augmenting the various definition classes (WorkbookDfn, WorksheetDfn,
    // RowDfn, and CellDfn.
    
    // They are robust enough in their current form to be used in enterprise, mission critical.

    public class WorkbookDfn
    {
        public IEnumerable<WorksheetDfn> Worksheets;
        public CellStyleFont DefaultFont;
    }

    public class WorksheetDfn
    {
        public string Name;
        public string TableName;
        public IEnumerable<ColDfn> Cols;
        public IEnumerable<CellDfn> ColumnHeadings;
        public IEnumerable<RowDfn> Rows;
    }

    public class RowDfn
    {
        public IEnumerable<CellDfn> Cells;
        public decimal? Height;
    }

    // Value can be:
    // - string
    // - bool
    // - DateTime
    // - int32, int64, uint, double, float, etc.

    // Standard formats
    public class CellDfn
    {
        public static Dictionary<string, int> StandardFormats = new Dictionary<string, int>
        {
            { "0",                        1   },
            { "0.00",                     2   },
            { "#,##0",                    3   },
            { "#,##0.00",                 4   },
            { "0%",                       9   },
            { "0.00%",                    10  },
            { "0.00E+00",                 11  },
            { "# ?/?",                    12  },
            { "# ??/??",                  13  },
            { "mm-dd-yy",                 14  },
            { "d-mmm-yy",                 15  },
            { "d-mmm",                    16  },
            { "mmm-yy",                   17  },
            { "h:mm AM/PM",               18  },
            { "h:mm:ss AM/PM",            19  },
            { "h:mm",                     20  },
            { "h:mm:ss",                  21  },
            { "h/d/yy h:mm",              22  },
            { "#,##0;(#,##0)",            37  },
            { "#,##0;[Red](#,##0)",       38  },
            { "#,##0.00;(#,##0.00)",      39  },
            { "#,##0.00;[Red](#,##0.00)", 40  },
            { "mm:ss",                    45  },
            { "[h]:mm:ss",                46  },
            { "mmss.0",                   47  },
            { "##0.0E+0",                 48  },
            { "@",                        49  },
        };
        public object Value;
        public string Formula;
        public CellDataType? CellDataType;
        public CellStyleDfn Style;
    }

    public enum HorizontalCellAlignment
    {
        Left,
        Center,
        Right,
    }

    public enum VerticalCellAlignment
    {
        Bottom,
        Center,
        Top,
    }

    public enum CellDataType
    {
        Boolean,
        Date,
        Number,
        String,
    }

    public static class SpreadsheetWriter
    {
        public static void Write(out MemoryStream stream, WorkbookDfn workbook)
        {
            try
            {
                stream = new MemoryStream();

                // create the blank workbook
                char[] base64CharArray = _EmptyXlsx
                    .Where(c => c != '\r' && c != '\n').ToArray();
                byte[] byteArray =
                    System.Convert.FromBase64CharArray(base64CharArray,
                    0, base64CharArray.Length);
                stream.Write(byteArray, 0, byteArray.Count());

                // open the workbook, and create the TableProperties sheet, populate it
                Write(SpreadsheetDocument.Open(stream, true), workbook);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception: {0} in {1}",
                    e.ToString(), e.Source);
                throw e;
            }
        }

        public static void Write(string fileName, WorkbookDfn workbook)
        {
            try
            {
                if (fileName == null) throw new ArgumentNullException("fileName");
                if (workbook == null) throw new ArgumentNullException("workbook");

                FileInfo fi = new FileInfo(fileName);
                if (fi.Exists)
                    fi.Delete();

                // create the blank workbook
                char[] base64CharArray = _EmptyXlsx
                    .Where(c => c != '\r' && c != '\n').ToArray();
                byte[] byteArray =
                    System.Convert.FromBase64CharArray(base64CharArray,
                    0, base64CharArray.Length);
                File.WriteAllBytes(fi.FullName, byteArray);

                // open the workbook, and create the TableProperties sheet, populate it
                Write(SpreadsheetDocument.Open(fi.FullName, true), workbook);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unhandled exception: {0} in {1}",
                    e.ToString(), e.Source);
                throw e;
            }
        }

        private static void Write(SpreadsheetDocument doc, WorkbookDfn workbook)
        {
            using (SpreadsheetDocument sDoc = doc)
            {
                WorkbookPart workbookPart = sDoc.WorkbookPart;
                XDocument wXDoc = workbookPart.GetXDocument();
                XElement sheetElement = wXDoc
                    .Root
                    .Elements(S.sheets)
                    .Elements(S.sheet)
                    .Where(s => (string)s.Attribute(SSNoNamespace.name) == "Sheet1")
                    .FirstOrDefault();
                if (sheetElement == null)
                    throw new SpreadsheetWriterInternalException();
                string id = (string)sheetElement.Attribute(R.id);
                sheetElement.Remove();
                workbookPart.PutXDocument();

                WorksheetPart sPart = (WorksheetPart)workbookPart.GetPartById(id);
                workbookPart.DeletePart(sPart);

                XDocument appXDoc = sDoc
                    .ExtendedFilePropertiesPart
                    .GetXDocument();
                XElement vector = appXDoc
                    .Root
                    .Elements(EP.TitlesOfParts)
                    .Elements(VT.vector)
                    .FirstOrDefault();
                if (vector != null)
                {
                    vector.SetAttributeValue(SSNoNamespace.size, 0);
                    XElement lpstr = vector.Element(VT.lpstr);
                    lpstr.Remove();
                }
                XElement vector2 = appXDoc
                    .Root
                    .Elements(EP.HeadingPairs)
                    .Elements(VT.vector)
                    .FirstOrDefault();
                XElement variant = vector2
                    .Descendants(VT.i4)
                    .FirstOrDefault();
                if (variant != null)
                    variant.Value = "1";
                sDoc.ExtendedFilePropertiesPart.PutXDocument();

                SetDefaultFont(sDoc, workbook.DefaultFont);
                workbookPart.WorkbookStylesPart.PutXDocument();

                if (workbook.Worksheets != null)
                    foreach (var worksheet in workbook.Worksheets)
                        AddWorksheet(sDoc, worksheet);

                workbookPart.WorkbookStylesPart.PutXDocument();
            }
        }

        private static void SetDefaultFont(SpreadsheetDocument sDoc, CellStyleFont style)
        {
            if (style == null) style = new CellStyleFont();
            if (style.Name == null) style.Name = "Calibri";
            if (style.Size == null) style.Size = 11;

            XDocument sXDoc = sDoc.WorkbookPart.WorkbookStylesPart.GetXDocument();
            XElement fonts = sXDoc.Root.Element(S.fonts);
            if (fonts == null)
            {
                fonts = new XElement(S.fonts,
                    new XAttribute(SSNoNamespace.count, 1),
                    CellStyleUtil.ToXElement(style));
                sXDoc.Root.Add(fonts);
            }
            else
            {
                XElement font = fonts.Element(S.font);
                font.Remove();
                fonts.Add(CellStyleUtil.ToXElement(style));
            }
        }

        private static CellStyleFont GetDefaultFont(SpreadsheetDocument sDoc)
        {
            XDocument sXDoc = sDoc.WorkbookPart.WorkbookStylesPart.GetXDocument();
            XElement font = sXDoc.Root.Element(S.fonts).Element(S.font);
            var result = new CellStyleFont {
                id = 0,
                Name = font.Element(S.name).Attribute("val").Value,
                Size = uint.Parse(font.Element(S.sz).Attribute("val").Value),
            };
            return result;
        }

        public static void AddWorksheet(SpreadsheetDocument sDoc, WorksheetDfn worksheetData)
        {
            Regex validSheetName = new Regex(@"^[^'*\[\]/\\:?][^*\[\]/\\:?]{0,30}$");
            if (!validSheetName.IsMatch(worksheetData.Name))
                throw new InvalidSheetNameException(worksheetData.Name);

            // throw WorksheetAlreadyExistsException if a sheet with the same name (case-insensitive) already exists in the workbook
            string UCName = worksheetData.Name.ToUpper();
            XDocument wXDoc = sDoc.WorkbookPart.GetXDocument();
            if (wXDoc
                .Root
                .Elements(S.sheets)
                .Elements(S.sheet)
                .Attributes(SSNoNamespace.name)
                .Select(a => ((string)a).ToUpper())
                .Contains(UCName))
                throw new WorksheetAlreadyExistsException(worksheetData.Name);

            // create the worksheet with the supplied name
            XDocument appXDoc = sDoc
                .ExtendedFilePropertiesPart
                .GetXDocument();
            XElement vector = appXDoc
                .Root
                .Elements(EP.TitlesOfParts)
                .Elements(VT.vector)
                .FirstOrDefault();
            if (vector != null)
            {
                int? size = (int?)vector.Attribute(SSNoNamespace.size);
                if (size == null)
                    size = 1;
                else
                    size = size + 1;
                vector.SetAttributeValue(SSNoNamespace.size, size);
                vector.Add(
                    new XElement(VT.lpstr, worksheetData.Name));
                XElement i4 = appXDoc
                    .Root
                    .Elements(EP.HeadingPairs)
                    .Elements(VT.vector)
                    .Elements(VT.variant)
                    .Elements(VT.i4)
                    .FirstOrDefault();
                if (i4 != null)
                    i4.Value = ((int)i4 + 1).ToString();
                sDoc.ExtendedFilePropertiesPart.PutXDocument();
            }

            WorkbookPart workbook = sDoc.WorkbookPart;
            string rId = "R" + Guid.NewGuid().ToString().Replace("-", "");
            WorksheetPart worksheetPart = workbook.AddNewPart<WorksheetPart>(rId);

            XDocument wbXDoc = workbook.GetXDocument();
            XElement sheets = wbXDoc.Descendants(S.sheets).FirstOrDefault();
            sheets.Add(
                new XElement(S.sheet,
                    new XAttribute(SSNoNamespace.name, worksheetData.Name.ToString()),
                    new XAttribute(SSNoNamespace.sheetId, sheets.Elements(S.sheet).Count() + 1),
                    new XAttribute(R.id, rId)));
            workbook.PutXDocument();

            string ws = S.s.ToString();
            string relns = R.r.ToString();

            using (Stream partStream = worksheetPart.GetStream(FileMode.Create, FileAccess.Write))
            {
                using (XmlWriter partXmlWriter = XmlWriter.Create(partStream))
                {
                    partXmlWriter.WriteStartDocument();
                    partXmlWriter.WriteStartElement("worksheet", ws);
                    if (worksheetData.Cols != null)
                    {
                        SerializeCols(sDoc, partXmlWriter, worksheetData);
                    }
                    partXmlWriter.WriteStartElement("sheetData", ws);

                    int numColumnHeadingRows = 0;
                    int numColumns = 0;
                    int numColumnsInRows = 0;
                    int numRows;
                    if (worksheetData.ColumnHeadings != null)
                    {
                        RowDfn row = new RowDfn
                        {
                            Cells = worksheetData.ColumnHeadings
                        };
                        SerializeRows(sDoc, partXmlWriter, new[] { row }, 1, out numColumns, out numColumnHeadingRows);
                    }
                    SerializeRows(sDoc, partXmlWriter, worksheetData.Rows, numColumnHeadingRows + 1, out numColumnsInRows,
                        out numRows);
                    int totalRows = numColumnHeadingRows + numRows;
                    int totalColumns = Math.Max(numColumns, numColumnsInRows);
                    if (worksheetData.ColumnHeadings != null && worksheetData.TableName != null)
                    {
                        partXmlWriter.WriteEndElement();
                        string rId2 = "R" + Guid.NewGuid().ToString().Replace("-", "");
                        partXmlWriter.WriteStartElement("tableParts", ws);
                        partXmlWriter.WriteStartAttribute("count");
                        partXmlWriter.WriteValue(1);
                        partXmlWriter.WriteEndAttribute();
                        partXmlWriter.WriteStartElement("tablePart", ws);
                        partXmlWriter.WriteStartAttribute("id", relns);
                        partXmlWriter.WriteValue(rId2);
                        TableDefinitionPart tdp = worksheetPart.AddNewPart<TableDefinitionPart>(rId2);
                        XDocument tXDoc = tdp.GetXDocument();
                        XElement table = new XElement(S.table,
                            new XAttribute(SSNoNamespace.id, 1),
                            new XAttribute(SSNoNamespace.name, worksheetData.TableName),
                            new XAttribute(SSNoNamespace.displayName, worksheetData.TableName),
                            new XAttribute(SSNoNamespace._ref, "A1:" + SpreadsheetMLUtil.IntToColumnId(totalColumns - 1) + totalRows.ToString()),
                            new XAttribute(SSNoNamespace.totalsRowShown, 0),
                            new XElement(S.autoFilter,
                                new XAttribute(SSNoNamespace._ref, "A1:" + SpreadsheetMLUtil.IntToColumnId(totalColumns - 1) + totalRows.ToString())),
                            new XElement(S.tableColumns,
                                new XAttribute(SSNoNamespace.count, totalColumns),
                                worksheetData.ColumnHeadings.Select((ch, i) =>
                                    new XElement(S.tableColumn,
                                        new XAttribute(SSNoNamespace.id, i + 1),
                                        new XAttribute(SSNoNamespace.name, ch.Value)))),
                            new XElement(S.tableStyleInfo,
                                new XAttribute(SSNoNamespace.name, "TableStyleMedium2"),
                                new XAttribute(SSNoNamespace.showFirstColumn, 0),
                                new XAttribute(SSNoNamespace.showLastColumn, 0),
                                new XAttribute(SSNoNamespace.showRowStripes, 1),
                                new XAttribute(SSNoNamespace.showColumnStripes, 0)));
                        tXDoc.Add(table);
                        tdp.PutXDocument();
                    }
                }
            }
            sDoc.WorkbookPart.WorkbookStylesPart.PutXDocument();
            sDoc.WorkbookPart.WorkbookStylesPart.Stylesheet.Save();
        }

        private static decimal? MeasureCellValueWidth(Graphics g, SpreadsheetDocument sDoc, CellDfn cell, Dictionary<int, Font> fonts, float scaleSize)
        {
            if (cell.Value == null) return null;
            Font font = fonts[0];
            if (cell.Style != null && cell.Style.Font != null)
            {
                var fontStyle = cell.Style.Font;
                XDocument sXDoc = sDoc.WorkbookPart.WorkbookStylesPart.GetXDocument();
                var fontId = GetFontId(sXDoc, fontStyle);
                if (fonts.ContainsKey(fontId))
                {
                    font = fonts[fontId];
                }
                else
                {
                    var fontName = (fontStyle.Name != null) ? fontStyle.Name : font.Name;
                    var fontSize = (fontStyle.Size != null) ? (float)fontStyle.Size : font.Size;
                    var fs = FontStyle.Regular;
                    if (fontStyle.Bold == true) fs |= FontStyle.Bold;
                    if (fontStyle.Italic == true) fs |= FontStyle.Italic;
                    //if (UnderLine) fs |= FontStyle.Underline;
                    //if (Strike) fs |= FontStyle.Strikeout;
                    font = new Font(fontName, fontSize, fs);
                    fonts[fontId] = font;
                }
            }

            //fit width to cell-value (not using cell.Style.NumFmt)
            string cellValue = null;
            if (cell.CellDataType == CellDataType.Date && cell.Value is DateTime)
            {
                cellValue = "00-00-0000";//(cell.Value as DateTime?).Value.ToShortDateString();
            }
            else
            {
                cellValue = cell.Value.ToString();
            }
            var width = g.MeasureString(cellValue, font, 1000, StringFormat.GenericTypographic).Width;
            width = (width + 5) / scaleSize;
            return (decimal)width;
        }

        private static void SerializeCols(SpreadsheetDocument sDoc, XmlWriter xw, WorksheetDfn worksheet)
        {
            var cols = worksheet.Cols;
            if (cols.Where(col => col != null && col.AutoFit != null).Any())
            {
                var fonts = new Dictionary<int, Font>();
                var defaultFont = GetDefaultFont(sDoc);
                var scaleFont = new Font(defaultFont.Name, (int)defaultFont.Size, FontStyle.Regular);
                fonts[0] = scaleFont;
                using (var b = new Bitmap(1, 1))
                {
                    using (var g = Graphics.FromImage(b))
                    {
                        float scaleSize = (float)Math.Truncate(g.MeasureString("00", scaleFont, 1000, StringFormat.GenericTypographic).Width - g.MeasureString("0", scaleFont, 1000, StringFormat.GenericTypographic).Width);
                        int index = 0;
                        foreach (var col in cols)
                        {
                            if (col != null && col.AutoFit != null)
                            {
                                if (col.AutoFit.Standard == null)
                                {
                                    //fit width to cell.Value
                                    var list = new List<CellDfn>();
                                    if (worksheet.ColumnHeadings != null && worksheet.ColumnHeadings.Count() > index)
                                    {
                                        list.AddRange(worksheet.ColumnHeadings.Skip(index).Take(1));
                                    }
                                    list.AddRange(worksheet.Rows.Select(row => (row.Cells.Count() > index) ? row.Cells.Skip(index).Take(1).First() : (CellDfn)null));
                                    col.Width = list.Where(cell => cell != null).Select(cell => MeasureCellValueWidth(g, sDoc, cell, fonts, scaleSize)).Where(w => w != null).Max();
                                }
                                else
                                {
                                    //fit width to AutoFit.Standard.Value
                                    col.Width = MeasureCellValueWidth(g, sDoc, col.AutoFit.Standard, fonts, scaleSize);
                                }
                            }
                            index++;
                        }
                    }
                }
            }

            string ns = S.s.NamespaceName;

            xw.WriteStartElement("cols", ns);
            int colIndex = 1;
            foreach (var col in cols)
            {
                if (col != null)
                {
                    decimal? width = null;
                    if (col.AutoFit != null && col.Width != null)
                    {
                        width = col.Width;

                        if (col.AutoFit.MinWidth != null)
                        {
                            decimal minWidth = (decimal)col.AutoFit.MinWidth;
                            width = Math.Max((decimal)width, minWidth);
                        }
                        if (col.AutoFit.MaxWidth != null)
                        {
                            decimal maxWidth = (decimal)col.AutoFit.MaxWidth;
                            width = Math.Min((decimal)width, maxWidth);
                        }
                    }
                    else //if (col.Width != null)
                    {
                        width = col.Width;
                    }

                    if (width != null)
                    {
                        xw.WriteStartElement("col", ns);

                        // min
                        xw.WriteStartAttribute("min");
                        xw.WriteValue(colIndex);
                        xw.WriteEndAttribute();
                        // max
                        xw.WriteStartAttribute("max");
                        xw.WriteValue(colIndex);
                        xw.WriteEndAttribute();
                        // width
                        xw.WriteStartAttribute("width");
                        xw.WriteValue(width);
                        xw.WriteEndAttribute();
                        // bestFit
                        xw.WriteStartAttribute("bestFit");
                        xw.WriteValue(1);
                        xw.WriteEndAttribute();
                        // customWidth
                        xw.WriteStartAttribute("customWidth");
                        xw.WriteValue(1);
                        xw.WriteEndAttribute();

                        xw.WriteEndElement();
                    }
                }
                colIndex++;
            }
            xw.WriteEndElement();
        }

        private static void SerializeRows(SpreadsheetDocument sDoc, XmlWriter xmlWriter, IEnumerable<RowDfn> rows,
            int startingRowNumber, out int numColumns, out int numRows)
        {
            int rowCount = 0;
            int rowNumber = startingRowNumber;
            int maxColumns = 0;
            int localNumColumns;
#if DisplayWorkingSet
            int workingSetInterval = 10000;
            int workingSetCount = 0;
#endif
            foreach (var row in rows)
            {
                SerializeRow(sDoc, xmlWriter, rowNumber, row, out localNumColumns);
                maxColumns = Math.Max(maxColumns, localNumColumns);
                rowNumber++;
                rowCount++;
#if DisplayWorkingSet
                if (workingSetCount++ > workingSetInterval)
                {
                    workingSetCount = 0;
                    Console.WriteLine(Environment.WorkingSet);
                }
#endif
            }
            numColumns = maxColumns;
            numRows = rowCount;
        }

        private static void SerializeRow(SpreadsheetDocument sDoc, XmlWriter xw, int rowCount, RowDfn row, out int numColumns)
        {
            string ns = S.s.NamespaceName;

            xw.WriteStartElement("row", ns);
            xw.WriteStartAttribute("r");
            xw.WriteValue(rowCount);
            xw.WriteEndAttribute();
            xw.WriteStartAttribute("spans");
            xw.WriteValue("1:" + row.Cells.Count().ToString());
            xw.WriteEndAttribute();
            if (row != null && row.Height != null)
            {
                xw.WriteStartAttribute("ht");
                xw.WriteValue(row.Height);
                xw.WriteEndAttribute();
                xw.WriteStartAttribute("customHeight");
                xw.WriteValue(1);
                xw.WriteEndAttribute();
            }
            int cellCount = 0;
            foreach (var cell in row.Cells)
            {
                if (cell != null)
                {
                    xw.WriteStartElement("c", ns);
                    xw.WriteStartAttribute("r");
                    xw.WriteValue(SpreadsheetMLUtil.IntToColumnId(cellCount) + rowCount.ToString());
                    xw.WriteEndAttribute();
                    if (cell.Style != null)
                    {
                        xw.WriteStartAttribute("s");
                        xw.WriteValue(GetCellStyle(sDoc, cell));
                        xw.WriteEndAttribute();
                    }
                    switch (cell.CellDataType)
                    {
                        case CellDataType.Boolean:
                            xw.WriteStartAttribute("t");
                            xw.WriteValue("b");
                            xw.WriteEndAttribute();
                            break;
                        case CellDataType.Date:
                            xw.WriteStartAttribute("t");
                            xw.WriteValue("d");
                            xw.WriteEndAttribute();
                            break;
                        case CellDataType.Number:
                            xw.WriteStartAttribute("t");
                            xw.WriteValue("n");
                            xw.WriteEndAttribute();
                            break;
                        case CellDataType.String:
                            xw.WriteStartAttribute("t");
                            xw.WriteValue("str");
                            xw.WriteEndAttribute();
                            break;
                        default:
                            xw.WriteStartAttribute("t");
                            xw.WriteValue("str");
                            xw.WriteEndAttribute();
                            break;
                    }
                    if (cell.Formula != null)
                    {
                        xw.WriteStartElement("f", ns);
                        xw.WriteValue(cell.Formula);
                        xw.WriteEndElement();
                    }
                    if (cell.Value != null)
                    {
                        xw.WriteStartElement("v", ns);
                        xw.WriteValue(cell.Value);
                        xw.WriteEndElement();
                    }
                    xw.WriteEndElement();
                }
                cellCount++;
            }
            xw.WriteEndElement();
            numColumns = cellCount;
        }

        private static int GetCellStyle(SpreadsheetDocument sDoc, CellDfn cell)
        {
            if (cell.Style.id != null)
            {
                return (int)cell.Style.id;
            }

            // if no match, then create a style
            XDocument sXDoc = sDoc.WorkbookPart.WorkbookStylesPart.GetXDocument();
            int newId = CreateNewStyle(sXDoc, cell, sDoc);
            cell.Style.id = newId;
            return newId;
        }

        private static int CreateNewStyle(XDocument sXDoc, CellDfn cell, SpreadsheetDocument sDoc)
        {
            var style = cell.Style;
            XAttribute applyFont = null;
            XAttribute fontId = null;
            if (style.Font != null)
            {
                applyFont = new XAttribute(SSNoNamespace.applyFont, 1);
                fontId = new XAttribute(SSNoNamespace.fontId, GetFontId(sXDoc, style.Font));
            }
            XAttribute applyAlignment = null;
            XElement alignment = null;
            if (style.Alignment != null)
            {
                applyAlignment = new XAttribute(SSNoNamespace.applyAlignment, 1);
                alignment = CellStyleUtil.ToXElement(style.Alignment);
            }
            XAttribute applyNumberFormat = null;
            XAttribute numFmtId = null;
            if (style.NumFmt != null)
            {
                applyNumberFormat = new XAttribute(SSNoNamespace.applyNumberFormat, 1);
                numFmtId = new XAttribute(SSNoNamespace.numFmtId, GetNumFmtId(sXDoc, style.NumFmt));
            }
            XAttribute applyFill = null;
            XAttribute fillId = null;
            if (style.Fill != null)
            {
                applyFill = new XAttribute(NoNamespace.applyFill, 1);
                fillId = new XAttribute(SSNoNamespace.fillId, GetFillId(sXDoc, style.Fill));
            }
            XAttribute applyBorder = null;
            XAttribute borderId = null;
            if (style.Border != null)
            {
                applyBorder = new XAttribute(NoNamespace.applyBorder, 1);
                borderId = new XAttribute(SSNoNamespace.borderId, GetBorderId(sXDoc, style.Border));
            }
            XElement newXf = new XElement(S.xf,
                applyFont,
                fontId,
                applyAlignment,
                alignment,
                applyNumberFormat,
                numFmtId,
                applyFill,
                fillId,
                applyBorder,
                borderId);
            XElement cellXfs = sXDoc
                .Root
                .Element(S.cellXfs);
            if (cellXfs == null)
            {
                cellXfs = new XElement(S.cellXfs,
                    new XAttribute(SSNoNamespace.count, 1),
                    newXf);
                return 0;
            }
            else
            {
                int currentCount = (int)cellXfs.Attribute(SSNoNamespace.count);
                cellXfs.SetAttributeValue(SSNoNamespace.count, currentCount + 1);
                cellXfs.Add(newXf);
                return currentCount;
            }
        }

        private static int GetFontId(XDocument sXDoc, CellStyleFont style)
        {
            XElement fonts = sXDoc.Root.Element(S.fonts);
            if (fonts == null)
            {
                fonts = new XElement(S.fonts,
                    new XAttribute(SSNoNamespace.count, 1),
                    CellStyleUtil.ToXElement(style));
                sXDoc.Root.Add(fonts);
                style.id = 0;
                return 0;
            }
            if (style.id != null)
            {
                return (int)style.id;
            }
            XElement font = CellStyleUtil.ToXElement(style);
            fonts.Add(font);
            int count = (int)fonts.Attribute(SSNoNamespace.count);
            fonts.SetAttributeValue(SSNoNamespace.count, count + 1);
            style.id = count;
            return count;
        }

        private static int GetNumFmtId(XDocument sXDoc, CellStyleNumFmt style)
        {
            if (style.id != null)
            {
                return (int)style.id;
            }
            string formatCode = style.formatCode;
            if (CellDfn.StandardFormats.ContainsKey(formatCode))
            {
                style.id = CellDfn.StandardFormats[formatCode];
                return (int)style.id;
            }

            int xfNumber = 81;
            while (true)
            {
                if (!sXDoc
                    .Root
                    .Elements(S.numFmts)
                    .Elements(S.numFmt)
                    .Any(nf => (int)nf.Attribute(SSNoNamespace.numFmtId) == xfNumber))
                    break;
                ++xfNumber;
            }
            style.id = xfNumber;
            XElement numFmts = sXDoc.Root.Element(S.numFmts);
            if (numFmts == null)
            {
                numFmts = new XElement(S.numFmts,
                    new XAttribute(SSNoNamespace.count, 1),
                    CellStyleUtil.ToXElement(style));
                sXDoc.Root.AddFirst(numFmts);
                return xfNumber;
            }
            XElement numFmt = CellStyleUtil.ToXElement(style);
            numFmts.Add(numFmt);
            return xfNumber;
        }

        private static int GetFillId(XDocument sXDoc, CellStyleFill style)
        {
            XElement fills = sXDoc.Root.Element(S.fills);
            if (fills == null)
            {
                fills = new XElement(S.fills,
                    new XAttribute(SSNoNamespace.count, 1),
                    CellStyleUtil.ToXElement(style));
                sXDoc.Root.Add(fills);
                style.id = 0;
                return 0;
            }
            if (style.id != null)
            {
                return (int)style.id;
            }
            XElement fill = CellStyleUtil.ToXElement(style);
            fills.Add(fill);
            int count = (int)fills.Attribute(SSNoNamespace.count);
            fills.SetAttributeValue(SSNoNamespace.count, count + 1);
            style.id = count;
            return count;
        }

        private static int GetBorderId(XDocument sXDoc, CellStyleBorder style)
        {
            XElement borders = sXDoc.Root.Element(S.borders);
            if (borders == null)
            {
                borders = new XElement(S.borders,
                    new XAttribute(SSNoNamespace.count, 1),
                    CellStyleUtil.ToXElement(style));
                sXDoc.Root.Add(borders);
                style.id = 0;
                return 0;
            }
            if (style.id != null)
            {
                return (int)style.id;
            }
            XElement border = CellStyleUtil.ToXElement(style);
            borders.Add(border);
            int count = (int)borders.Attribute(SSNoNamespace.count);
            borders.SetAttributeValue(SSNoNamespace.count, count + 1);
            style.id = count;
            return count;
        }

        private static string _EmptyXlsx = @"UEsDBBQABgAIAAAAIQBi7p1oYQEAAJAEAAATAAgCW0NvbnRlbnRfVHlwZXNdLnhtbCCiBAIooAAC
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACs
lE1PwzAMhu9I/IcqV9Rm44AQWrcDH0eYxPgBoXHXaGkSxd7Y/j1u9iGEyqaJXRq1sd/3iWtnNFm3
NltBRONdKYbFQGTgKq+Nm5fiY/aS34sMSTmtrHdQig2gmIyvr0azTQDMONthKRqi8CAlVg20Cgsf
wPFO7WOriF/jXAZVLdQc5O1gcCcr7wgc5dRpiPHoCWq1tJQ9r/nzliSCRZE9bgM7r1KoEKypFDGp
XDn9yyXfORScmWKwMQFvGEPIXodu52+DXd4blyYaDdlURXpVLWPItZVfPi4+vV8Ux0V6KH1dmwq0
r5YtV6DAEEFpbACotUVai1YZt+c+4p+CUaZleGGQ7nxJ+AQH8f8GmZ7/R0gyJwyRNhbwwqfdip5y
blQE/U6RJ+PiAD+1j3Fw30yjD8gTFOH8KuxHpMvOAwtBJAOHIelrtoMjT9/5hr+6Hbr51qB7vGW6
T8bfAAAA//8DAFBLAwQUAAYACAAAACEAtVUwI/UAAABMAgAACwAIAl9yZWxzLy5yZWxzIKIEAiig
AAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AIySz07DMAzG70i8Q+T76m5ICKGlu0xIuyFUHsAk7h+1jaMkQPf2hAOCSmPb0fbnzz9b3u7maVQf
HGIvTsO6KEGxM2J712p4rZ9WD6BiImdpFMcajhxhV93ebF94pJSbYtf7qLKLixq6lPwjYjQdTxQL
8exypZEwUcphaNGTGahl3JTlPYa/HlAtPNXBaggHeweqPvo8+bK3NE1veC/mfWKXToxAnhM7y3bl
Q2YLqc/bqJpCy0mDFfOc0xHJ+yJjA54m2lxP9P+2OHEiS4nQSODzPN+Kc0Dr64Eun2ip+L3OPOKn
hOFNZPhhwcUPVF8AAAD//wMAUEsDBBQABgAIAAAAIQCBPpSX9AAAALoCAAAaAAgBeGwvX3JlbHMv
d29ya2Jvb2sueG1sLnJlbHMgogQBKKAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
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";

    }

    public class SpreadsheetWriterInternalException : Exception
    {
        public SpreadsheetWriterInternalException()
            : base("Internal error - unexpected content in _EmptyXlsx.")
        {
        }
    }

    public class InvalidSheetNameException : Exception
    {
        public InvalidSheetNameException(string name)
            : base(string.Format("The supplied name ({0}) is not a valid XLSX worksheet name.", name))
        {
        }
    }
}