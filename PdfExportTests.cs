using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TangerineWindows
{
    public sealed class ExportPageProbe { public int count { get; set; } public int rotation { get; set; } public string text { get; set; } public string title { get; set; } }
    public static class PdfExportTests
    {
        const string Fixture = @"import sys,json
from pathlib import Path
from pypdf import PdfWriter,PdfReader
from pypdf.generic import DictionaryObject,NameObject,DecodedStreamObject,RectangleObject,FloatObject
def make(path, sizes, rotations=None, units=None, crop=None, password=None):
    writer=PdfWriter()
    try:
        writer.add_metadata({'/Title':'تجربة الصفحات'})
        font=writer._add_object(DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')}))
        for i,(w,h) in enumerate(sizes):
            page=writer.add_blank_page(width=w,height=h)
            color='1 0 0' if i==0 else '0 0.7 0'
            stream=DecodedStreamObject()
            stream.set_data((f'{color} rg 0 0 {w} {h} re f\n0 0 1 rg {w/2} 0 {w/2} {h/2} re f\n0 0 0 rg BT /F1 10 Tf 4 10 Td (PAGE {i+1}) Tj ET').encode('ascii'))
            page[NameObject('/Contents')]=writer._add_object(stream)
            page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):font})})
            if rotations: page.rotate(rotations[i])
            if units: page[NameObject('/UserUnit')]=FloatObject(units[i])
            if crop: page.cropbox=RectangleObject(crop)
        if password: writer.encrypt(password)
        with open(path,'xb') as target: writer.write(target)
    finally: writer.close()
if sys.argv[1]=='fixture':
    root=Path(sys.argv[2])
    make(root/'صفحات تجربة.pdf',[(144,72),(108,144)],[0,90])
    make(root/'crop.pdf',[(144,72)],crop=[0,0,72,36])
    make(root/'units.pdf',[(72,72)],units=[2])
    make(root/'locked.pdf',[(72,72)],password='test-password')
    make(root/'oversize.pdf',[(10000,10000)])
    make(root/'empty.pdf',[])
    make(root/'too-many.pdf',[(72,72)]*301)
    make(root/'progress.pdf',[(500,500)]*8)
    print('OK')
else:
    reader=PdfReader(sys.argv[2])
    try: print(json.dumps({'count':len(reader.pages),'rotation':int(reader.pages[0].get('/Rotate',0))%360,'text':reader.pages[0].extract_text(),'title':str((reader.metadata or {}).get('/Title',''))},ensure_ascii=False))
    finally: reader.close()
";
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
        static string Python(string helper, string operation, string value) {
            string python = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "python.exe");
            var start = new ProcessStartInfo(python, string.Join(" ", new[] { helper, operation, value }.Select(Engine.Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8 }; start.EnvironmentVariables["PYTHONUTF8"] = "1";
            using (var p = new Process { StartInfo = start }) { p.Start(); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync(); if (!p.WaitForExit(30000)) { p.Kill(); p.WaitForExit(); throw new TimeoutException("PDF fixture timed out."); } if (p.ExitCode != 0) throw new Exception(error.Result); return output.Result; }
        }
        static byte[] Pixel(BitmapSource source, int x, int y) { var result = new byte[4]; new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(x, y, 1, 1), result, 4, 0); return result; }
        static string[] Pages(string folder) { return Directory.GetFiles(folder).OrderBy(f => f, StringComparer.Ordinal).ToArray(); }
        public static int Run(string root) {
            var results = new List<string>(); Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); results.Add("PASS " + message); };
            try {
                root = Path.GetFullPath(root); if (Directory.Exists(root)) throw new IOException("Use a new PDF export test folder."); Directory.CreateDirectory(root);
                string helper = Path.Combine(root, "pdf-fixture.py"); File.WriteAllText(helper, Fixture, new UTF8Encoding(false)); Python(helper, "fixture", root);
                string source = Path.Combine(root, "صفحات تجربة.pdf"), before = Hash(source); var token = CancellationToken.None;
                check(Catalog.Choices(new[] { source }, 0).Select(c => c.Id).SequenceEqual(new[] { "jpg", "png" }) && Catalog.Choices(new[] { source }, 1).Any(c => c.Id == "pdf-split"), "PDF wheel exposes real JPG/PNG formats and a split tool");
                check(new[] { "jpg", "png", "pdf-split" }.All(op => !MediaTools.NeedsEngine(new[] { source }, op)), "PDF exports work without FFmpeg");
                var progress = new List<string>(); string png = Engine.Convert(new[] { source }, "png", new Parameters(), token, message => progress.Add(message)); var images = Pages(png);
                var first = ImageEdits.Load(images[0]); var second = ImageEdits.Load(images[1]);
                check(images.Length == 2 && first.PixelWidth == 600 && first.PixelHeight == 300 && second.PixelWidth == 600 && second.PixelHeight == 450, "PNG export renders every full-size page at 300 DPI with rotation");
                var pixel = Pixel(first, 20, 20); var green = Pixel(second, 300, 100); check(pixel[2] > 245 && pixel[1] < 10 && green[1] > 150 && green[2] < 10, "PNG output preserves page colors and order");
                check(Math.Abs(first.DpiX - 300) < .1 && (File.GetAttributes(png) & FileAttributes.Hidden) == 0, "PNG resolution is tagged correctly and the published folder is visible");
                check(progress.SequenceEqual(new[] { "Page 1 of 2", "Page 2 of 2" }), "Page progress reports every completed page in order");
                string jpg = Engine.Convert(new[] { source }, "jpg", new Parameters(), token, null); var jpeg = ImageEdits.Load(Pages(jpg)[0]); var jpegPixel = Pixel(jpeg, 20, 20);
                check(Pages(jpg).Length == 2 && jpeg.PixelWidth == 600 && jpeg.PixelHeight == 300 && jpegPixel[2] > 240 && jpegPixel[1] < 15 && Math.Abs(jpeg.DpiX - 300) < .1, "JPG export retains dimensions, visible content and 300 DPI metadata");
                string split = Engine.Convert(new[] { source }, "pdf-split", new Parameters(), token, null); var documents = Pages(split);
                var p1 = new JavaScriptSerializer().Deserialize<ExportPageProbe>(Python(helper, "probe", documents[0])); var p2 = new JavaScriptSerializer().Deserialize<ExportPageProbe>(Python(helper, "probe", documents[1]));
                check(documents.Length == 2 && p1.count == 1 && p2.count == 1 && p1.text.Contains("PAGE 1") && p2.text.Contains("PAGE 2"), "Split outputs retain one real text page each in source order");
                check(p2.rotation == 90 && p2.title == "تجربة الصفحات", "Split retains page rotation and Unicode document metadata");
                string copied = PdfExport.Save(source, "PNG", token, null); check(copied != png && Pages(png).Length == 2 && Hash(source) == before, "Repeated exports choose a new folder and preserve previous output and source bytes");
                string cropped = PdfExport.Save(Path.Combine(root, "crop.pdf"), "png", token, null); var crop = ImageEdits.Load(Pages(cropped)[0]); check(crop.PixelWidth == 300 && crop.PixelHeight == 150, "Raster export respects the PDF crop box");
                string units = PdfExport.Save(Path.Combine(root, "units.pdf"), "png", token, null); var physical = ImageEdits.Load(Pages(units)[0]); check(physical.PixelWidth == 600 && physical.PixelHeight == 600, "PDF custom physical units retain true 300 DPI output");
                using (var cts = new CancellationTokenSource()) { int count = Directory.GetDirectories(root).Length; cts.Cancel(); bool stopped = false; try { PdfExport.Save(source, "png", cts.Token, null); } catch (OperationCanceledException) { stopped = true; } check(stopped && Directory.GetDirectories(root).Length == count, "Pre-cancel publishes no output or staging folder"); }
                using (var cts = new CancellationTokenSource()) {
                    int count = Directory.GetDirectories(root).Length; bool stopped = false, during = false;
                    try { PdfExport.Save(Path.Combine(root, "progress.pdf"), "png", cts.Token, message => { during = Directory.GetDirectories(root, ".madardrop-pages-*").Any(d => !File.Exists(Path.Combine(d, "response.json"))); cts.Cancel(); }); } catch (OperationCanceledException) { stopped = true; }
                    check(stopped && during && Directory.GetDirectories(root).Length == count, "Cancellation during page rendering removes all partial output");
                }
                File.WriteAllText(Path.Combine(root, "corrupt.pdf"), "%PDF-1.4\ninvalid xref\n");
                foreach (string name in new[] { "locked.pdf", "corrupt.pdf", "empty.pdf", "too-many.pdf", "oversize.pdf" }) {
                    int count = Directory.GetDirectories(root).Length; bool failed = false; try { PdfExport.Save(Path.Combine(root, name), "png", token, null); } catch (InvalidOperationException) { failed = true; }
                    check(failed && Directory.GetDirectories(root).Length == count, "Invalid export is rejected and cleaned: " + name);
                }
                string largeSplit = PdfExport.Save(Path.Combine(root, "oversize.pdf"), "pdf-split", token, null); check(Pages(largeSplit).Length == 1, "Large vector pages can split without allocating a huge raster");
                bool badFormat = false; try { PdfExport.Save(source, "exe", token, null); } catch (ArgumentException) { badFormat = true; } check(badFormat, "Unsupported formats are rejected");
                check(Hash(source) == before && !Directory.GetDirectories(root, ".madardrop-pages-*").Any(), "Original bytes remain unchanged and private staging is removed");
                results.Add("RESULT: " + results.Count + " PDF export checks passed."); File.WriteAllLines(Path.Combine(root, "pdf-export-test-results.txt"), results, Encoding.UTF8); return 0;
            } catch (Exception e) { Directory.CreateDirectory(root); results.Add("FAIL " + e); File.WriteAllLines(Path.Combine(root, "pdf-export-test-results.txt"), results, Encoding.UTF8); return 1; }
        }
    }
}
