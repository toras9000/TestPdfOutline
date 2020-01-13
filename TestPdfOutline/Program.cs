using iText.IO.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TestPdfOutline
{
    class Program
    {
        static void Main(string[] args)
        {
            var inputFile = @"d:\temp\a.pdf";
            var outputFile = @"d:\temp\b.pdf";

            try
            {
                using (var reader = new PdfReader(inputFile))
                using (var writer = new PdfWriter(outputFile))
                {
                    var pdfDocument = new PdfDocument(reader, writer);
                    var extractor = new TextLinesExtractionStrategy();
                    var processor = new PdfCanvasProcessor(extractor);

                    var numPages = pdfDocument.GetNumberOfPages();
                    var outlineHead = pdfDocument.GetOutlines(true);
                    var captionPattern = new Regex(@"^\s*\d+\.\s*.*$");
                    for (var i = 0; i < numPages; i++)
                    {
                        var page = pdfDocument.GetPage(1 + i);

                        processor.ProcessPageContent(page);

                        System.Diagnostics.Debug.WriteLine("PageSize:" + page.GetPageSize());

                        foreach (var line in extractor.Lines)
                        {
                            var captionMatch = captionPattern.Match(line.Text);
                            if (!captionMatch.Success)
                            {
                                continue;
                            }

                            var captionText = captionMatch.Groups[0].Value;
                            var captionHead = line.Ascent.GetBoundingRectangle();
                            var captionDest = PdfExplicitDestination.CreateXYZ(page, captionHead.GetLeft(), captionHead.GetTop(), float.NaN);

                            outlineHead.AddOutline(captionText).AddDestination(captionDest);
                        }
                        extractor.Clear();
                    }

                    pdfDocument.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Err:" + ex.Message);
            }
            finally
            {
                var resultFile = new FileInfo(outputFile);
                if (resultFile.Exists && resultFile.Length <= 0)
                {
                    resultFile.Delete();
                }

                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
    }

    class EmptyDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new EmptyDisposable();

        public void Dispose() { }
    }

    class TextLinesExtractionStrategy : ITextExtractionStrategy
    {
        public TextLinesExtractionStrategy()
        {
            this.lineTexts = new List<LineInfo>();
            this.lineBuffer = new StringBuilder();
            this.Lines = this.lineTexts.AsReadOnly();
        }

        public class LineInfo
        {
            public LineInfo(TextRenderInfo info, string text)
            {
                this.Text = text;
                this.Font = info.GetFont().GetFontProgram().GetFontNames();
                this.Object = info.GetPdfString();
                this.Ascent = info.GetAscentLine();
                this.Descent = info.GetDescentLine();
            }

            public string Text { get; }
            public FontNames Font { get; }
            public PdfString Object { get; }
            public LineSegment Ascent { get; }
            public LineSegment Descent { get; }
        }

        public IReadOnlyList<LineInfo> Lines { get; }

        public ICollection<EventType> GetSupportedEvents()
        {
            return TextTypes;
        }

        public void EventOccurred(IEventData data, EventType type)
        {
            if (data is TextRenderInfo info)
            {
                var baseLine = info.GetBaseline();
                var textChunk = info.GetText();

                if (this.lineHead == null)
                {
                    this.lineHead = info;
                }
                else
                {
                    var prevStart = this.prevLine.GetStartPoint();
                    var curStart = baseLine.GetStartPoint();

                    var distance = Math.Abs(curStart.Get(Vector.I2) - prevStart.Get(Vector.I2));

                    if (info.GetFontSize() <= distance)
                    {
                        var lineText = this.lineBuffer.ToString();
                        var lineInfo = new LineInfo(info, lineText);
                        this.lineTexts.Add(lineInfo);
                        this.lineBuffer.Clear();
                        this.lineHead = null;
                    }
                    else if (this.lineBuffer[this.lineBuffer.Length - 1] != ' '
                          && 0 < textChunk?.Length && !textChunk.StartsWith(" "))
                    {
                        var prevEnd = this.prevLine.GetEndPoint();
                        var gapPrev = prevEnd.Get(Vector.I2) - curStart.Get(Vector.I2);
                        if (info.GetSingleSpaceWidth() <= gapPrev)
                        {
                            this.lineBuffer.Append(' ');
                        }
                    }
                }

                this.lineBuffer.Append(textChunk);
                this.prevLine = baseLine;
            }
        }

        public string GetResultantText()
        {
            return string.Join("\n", this.lineTexts.Select(i => i.Text));
        }

        public void Clear()
        {
            this.lineTexts.Clear();
        }

        private static readonly EventType[] TextTypes = new[]
        {
            EventType.RENDER_TEXT,
        };

        private List<LineInfo> lineTexts;
        private StringBuilder lineBuffer;
        private TextRenderInfo lineHead;
        private LineSegment prevLine;
    }
}
