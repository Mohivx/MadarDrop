using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace TangerineWindows
{
    public sealed class UiPreferences { public string Language = "ar"; public bool Dark; }
    public static class Ui
    {
        static string store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TangerineWindows", "settings.json");
        public static UiPreferences Preferences = Load();
        public static bool Arabic { get { return Preferences.Language == "ar"; } }
        public static bool Dark { get { return Preferences.Dark; } }
        public static event EventHandler Changed;
        static readonly Dictionary<string, string> translations = Parse(@"MadarDrop 0.3.1|مدار دروب 0.3.1
Set up media tools…|إعداد أدوات الوسائط…
Check for updates…|البحث عن تحديثات…
Crop Image|قص الصورة
WINDOWS PREVIEW|نسخة ويندوز
WORKSPACE|مساحة العمل
◉   Convert files|◉   تحويل الملفات
◷   Activity|◷   النشاط
⚙   Settings|⚙   الإعدادات
Less busywork. More flow.|عمل أقل، إنجاز أكثر.
Convert files right where you are. Drag, choose, and drop.|حوّل ملفاتك من مكانها. اسحب الملف واختر الصيغة وأفلته.
Your files.\nA new format.|ملفاتك.\nبصيغة جديدة.
Drop files here, or choose them below. Your original files stay safe.|أفلت ملفاتك هنا أو اخترها بالزر. تبقى الملفات الأصلية محفوظة.
Choose files…|اختيار ملفات…
Clear selection|مسح الاختيار
No files selected|لم تختر ملفات
Convert|تحويل
Tools|أدوات
Actions|إجراءات
Ready when you are|جاهز للعمل
Cancel|إلغاء
All files are processed on this PC|تُعالَج جميع الملفات على هذا الجهاز
●  Everything stays on your PC|●  ملفاتك تبقى على جهازك
SHIFT + DRAG\n\nA little shortcut.\nA lot less busywork.|SHIFT + سحب\n\nاختصار بسيط.\nيوفّر عليك خطوات كثيرة.
In File Explorer: drag a file, then press Shift.\nShift + Alt opens tools · Shift + Ctrl opens actions · Esc cancels|اسحب ملفًا من المستكشف ثم اضغط Shift.\nAlt لأدوات التعديل · Ctrl للإجراءات · Esc للإلغاء
A little history.|سجل ملفاتك.
Results from this session. Click an output to show it in its folder.|نتائج الجلسة الحالية. اضغط على نتيجة لفتح موقعها.
Your completed files will appear here.|ستظهر ملفاتك المكتملة هنا.
Make it yours.|على ذوقك.
A few small preferences for this session.|اختر اللغة والمظهر وطريقة عمل التطبيق.
Language|اللغة
Appearance|المظهر
Light|فاتح
Dark|داكن
Open MadarDrop|فتح مدار دروب
Pause / resume wheel|إيقاف / تشغيل الدائرة
Quit|خروج
Quit MadarDrop|إغلاق مدار دروب
Show the wheel with Shift while dragging in File Explorer|إظهار الدائرة مع Shift أثناء سحب الملفات
Show a notification when conversion finishes|إظهار إشعار عند انتهاء التحويل
Open the output folder after conversion|فتح مجلد الناتج بعد التحويل
Formats in this preview|الصيغ المتاحة في هذه النسخة
Images · Video · Audio · Image-to-PDF · Subtitles\nText & DOCX · CSV / TSV / XLSX values · ZIP archives|صور · فيديو · صوت · صور إلى PDF · ترجمة\nنصوص وDOCX · جداول CSV وTSV وXLSX · ملفات ZIP
Drop a file|أفلت ملفًا
Choose a format|اختر الصيغة
Drop on a format to convert|أفلت الملف على الصيغة المطلوبة
Choose a file tool|اختر أداة التعديل
Choose an action|اختر الإجراء
COMPRESS|ضغط
METADATA|بيانات
EDIT|تعديل
ANNOTATE|تعليقات
ADD BG|خلفية
CROP|قص
EXACT CROP|قص بالأبعاد
REDACT|طمس
RESIZE|تحجيم
MAKE PDF|إنشاء PDF
COLLAGE|تجميع صور
MORE|المزيد
INFO|معلومات
COPY PATH|نسخ المسار
FOLDER|المجلد
PAGES|صفحات
MERGE|دمج
EXTRACT|استخراج
TRIM|اقتطاع
SPEED|سرعة
Create a smaller copy|إنشاء نسخة أصغر
Adjust light, color, and detail|تعديل الإضاءة والألوان والتفاصيل
Add arrows, text, and shapes|إضافة الأسهم والنصوص والأشكال
Cover or pixelate private areas|تغطية المناطق الخاصة أو تمويهها
Add a background and rounded corners|إضافة خلفية وزوايا مدوّرة
Crop visually, rotate, or flip|قص مرئي وتدوير وقلب
Inspect, edit, or remove metadata|عرض البيانات المضمّنة وتعديلها أو إزالتها
Resize to a maximum width|تغيير العرض مع حفظ التناسب
Crop using exact dimensions|قص بأبعاد دقيقة
Combine images into one PDF|جمع الصور في ملف PDF
Arrange these images in a collage|ترتيب الصور في لوحة واحدة
Reorder, rotate, duplicate, or remove pages|ترتيب الصفحات وتدويرها وتكرارها وحذفها
Combine PDFs and organize their pages|دمج ملفات PDF وتنظيم صفحاتها
Compress PDF streams without rasterizing text|ضغط بنية PDF مع الاحتفاظ بالنص
Edit PDF title, author, and metadata|تعديل عنوان PDF ومؤلفه وبياناته
View file information|عرض معلومات الملف
Copy paths to the clipboard|نسخ المسارات إلى الحافظة
Open the containing folder|فتح المجلد الذي يحتوي الملف
Create a ZIP archive|إنشاء ملف ZIP
Open all file tools|عرض جميع أدوات الملف
A tool for every little task.|أدوات لملفاتك اليومية.
File tools|أدوات الملفات
Make it your own.|عدّلها على ذوقك.
Edit photo|تعديل الصورة
Annotate photo|التعليق على الصورة
Redact photo|طمس الصورة
Add background|إضافة خلفية
Undo|تراجع
Redo|إعادة
Reset|إعادة ضبط
Save a copy|حفظ نسخة
Adjust|ضبط
Annotate|تعليقات
Redact|طمس
Background|خلفية
Crop & rotate|قص وتدوير
Photo adjustments|ضبط الصورة
Changes appear in the preview. Save exports a new image.|تظهر التغييرات في المعاينة. الحفظ ينشئ صورة جديدة.
Exposure|التعريض
Contrast|التباين
Saturation|التشبّع
Warmth|الدفء
Sharpness|الحدة
Black and white|أبيض وأسود
Output width (pixels)|عرض الناتج بالبكسل
Apply size|تطبيق الحجم
Cover private areas|تغطية المناطق الخاصة
Draw on your image|ارسم على صورتك
Drag a box to cover or pixelate an area. Export flattens the result into image pixels.|اسحب مربعًا لتغطية منطقة أو تمويهها. يدمج الحفظ الطمس داخل بكسلات الصورة.
Choose a tool, then drag on the image. Click to add text or a numbered callout.|اختر أداة ثم اسحب على الصورة. اضغط لإضافة نص أو رقم توضيحي.
Stroke / text size|حجم الخط والنص
Remove last mark|إزالة آخر علامة
A new background|خلفية جديدة
Add a border or place the image on another photo.|أضف إطارًا أو ضع الصورة على خلفية أخرى.
Enable background|تفعيل الخلفية
Gradient|تدرّج لوني
Drop shadow|ظل
Spacing|المسافة
Rounded corners|الزوايا المدوّرة
Use a background image…|اختيار صورة للخلفية…
Remove background image|إزالة صورة الخلفية
Drag a rectangle over the image to crop. Undo brings the previous image back.|اسحب مستطيلًا للقص. يعيد التراجع الصورة السابقة.
Rotate left ↶|تدوير لليسار ↶
Rotate right ↷|تدوير لليمين ↷
Flip horizontally|قلب أفقي
Flip vertically|قلب عمودي
Crop is applied to the current visible image, including any marks or background.|يُطبّق القص على الصورة الظاهرة مع العلامات والخلفية.
Add text|إضافة نص
Your text|اكتب النص
Add|إضافة
arrow|سهم
line|خط
rectangle|مستطيل
ellipse|دائرة
pen|قلم
highlight|تظليل
text|نص
number|رقم
cover|تغطية
pixelate|تمويه مربّع
Aspect ratio|نسبة القص
Free|حر
Apply|تطبيق
W|ع
H|ط
Every page in its place.|كل صفحة في مكانها.
Reorder, rotate, duplicate, remove, or combine PDFs. Save creates a new document.|رتّب الصفحات أو دوّرها أو كرّرها أو احذفها أو ادمج ملفات PDF. يحفظ الناتج كمستند جديد.
Organize PDF pages|تنظيم صفحات PDF
Loading pages…|جارٍ تحميل الصفحات…
Rendering page previews…|جارٍ إنشاء معاينات الصفحات…
Add another PDF…|إضافة ملف PDF…
Move earlier ↑|نقل للأعلى ↑
Move later ↓|نقل للأسفل ↓
Rotate page ↻|تدوير الصفحة ↻
Duplicate page|تكرار الصفحة
Remove page|حذف الصفحة
Remove document metadata|إزالة بيانات المستند
Keep at least one page.|أبقِ صفحة واحدة على الأقل.
Saving PDF…|جارٍ حفظ PDF…
The original PDFs stay untouched. Digital signatures are not retained in the edited document.|تبقى الملفات الأصلية محفوظة. لا يحتفظ المستند المعدّل بالتوقيعات الرقمية.
The details behind the file.|البيانات خلف الملف.
Edit the fields below, or clear existing metadata. Save creates a new copy.|عدّل الحقول أو أزل البيانات الحالية. الحفظ ينشئ نسخة جديدة.
Title|العنوان
Author|المؤلف
Subject|الموضوع
Keywords|الكلمات المفتاحية
Copyright|حقوق النشر
CameraMake|الشركة المصنّعة للكاميرا
CameraModel|طراز الكاميرا
Date|التاريخ
Remove existing metadata (including hidden EXIF / GPS)|إزالة البيانات الحالية، بما فيها EXIF وGPS
No embedded metadata found.|لا توجد بيانات مضمّنة.
Loading metadata…|جارٍ تحميل البيانات…
Original file untouched|الملف الأصلي محفوظ
Resize image|تغيير حجم الصورة
Crop image|قص الصورة
Trim recording|اقتطاع التسجيل
Playback speed|سرعة التشغيل
Left (pixels)|اليسار بالبكسل
Top (pixels)|الأعلى بالبكسل
Width (pixels)|العرض بالبكسل
Height (pixels)|الارتفاع بالبكسل
Maximum width (pixels)|أقصى عرض بالبكسل
Start (seconds)|البداية بالثواني
Duration (seconds)|المدة بالثواني
Speed (0.5 to 2.0)|السرعة من 0.5 إلى 2
Create a copy|إنشاء نسخة
Enter valid values within the range shown.|أدخل قيمًا صحيحة ضمن النطاق الموضّح.
Paths copied to clipboard|نُسخت المسارات
File information|معلومات الملف
Choose files to convert|اختر الملفات المراد تحويلها
Could not save|تعذّر الحفظ
Saved|حُفظ
Select a page to change it|اختر صفحة لتعديلها
Independent prototype|نسخة مستقلة
NEW: Photo editor, annotations, redaction, backgrounds,\nmetadata, collage, and PDF page editing.\nPDF-to-Office is still in development.\nThis independent prototype is not affiliated with Tangerine for Mac.|جديد: تعديل الصور والتعليقات والطمس والخلفيات،\nوالبيانات وتجميع الصور وتحرير صفحات PDF.\nتحويل PDF إلى Office ما زال قيد التطوير.\nهذا التطبيق مستقل عن Tangerine للماك.
Choose a destination folder|اختر مجلد الوجهة
Edited copy|نسخة معدّلة
Saved beside the original|حُفظ بجانب الأصل
MadarDrop — Shift + drag to convert|مدار دروب — اسحب الملف مع Shift
MadarDrop — wheel paused|مدار دروب — الدائرة متوقفة
Keep a section of the recording|اقتطاع جزء من التسجيل
Change playback speed|تغيير سرعة التشغيل
Organize pages in this PDF|تنظيم صفحات ملف PDF
Combine this with other PDFs|دمج هذا الملف مع ملفات PDF أخرى
Compress the PDF|ضغط ملف PDF
View or edit metadata|عرض البيانات وتعديلها
Extract ZIP into a new folder|استخراج ZIP في مجلد جديد
Bundle these files into one archive|جمع الملفات في ملف مضغوط
Copy file paths|نسخ مسارات الملفات
Create a PDF from these images|إنشاء PDF من هذه الصور
Keep this editing session under 300 pages.|أبقِ عدد الصفحات أقل من 300 في الجلسة.");
        static Dictionary<string, string> Parse(string data) { var d = new Dictionary<string, string>(StringComparer.Ordinal); foreach (string row in data.Replace("\r", "").Split('\n')) { int i = row.IndexOf('|'); if (i > 0) d[row.Substring(0, i).Replace("\\n", "\n")] = row.Substring(i + 1).Replace("\\n", "\n"); } return d; }
        static UiPreferences Load() { try { if (File.Exists(store)) { var p = new JavaScriptSerializer().Deserialize<UiPreferences>(File.ReadAllText(store)); if (p != null && (p.Language == "ar" || p.Language == "en")) return p; } } catch { } return new UiPreferences(); }
        public static string T(string value)
        {
            if (!Arabic || string.IsNullOrEmpty(value)) return value;
            string translated; if (translations.TryGetValue(value, out translated)) return translated;
            if (value.StartsWith("Edit · ")) return "تعديل · " + value.Substring(7);
            if (value.StartsWith("Edit metadata · ")) return "تعديل البيانات · " + value.Substring(16);
            if (value.StartsWith("Convert to ")) return "تحويل إلى " + value.Substring(11);
            if (value.StartsWith("Saved ")) return "حُفظ " + value.Substring(6);
            if (Regex.IsMatch(value, @"^\d+ files selected$")) return value.Split(' ')[0] + " ملفات محددة";
            if (Regex.IsMatch(value, @"^\d+ files$")) return value.Split(' ')[0] + " ملفات";
            if (Regex.IsMatch(value, @"^\d+ pages · Select a page to change it$")) return value.Split(' ')[0] + " صفحات · اختر صفحة لتعديلها";
            var imageStatus = Regex.Match(value, @"^(\d+) × (\d+) source · (\d+) marks · Original file untouched$");
            if (imageStatus.Success) return imageStatus.Groups[1].Value + " × " + imageStatus.Groups[2].Value + " الأصل · " + imageStatus.Groups[3].Value + " علامات · الملف الأصلي محفوظ";
            if (value.Contains(" · ")) return string.Join(" · ", value.Split(new[] { " · " }, StringSplitOptions.None).Select(s => translations.TryGetValue(s, out translated) ? translated : s));
            return value;
        }
        public static void SetLanguage(string language) { if (language != "ar" && language != "en") return; Preferences.Language = language; Save(); Notify(); }
        public static void SetDark(bool dark) { Preferences.Dark = dark; Save(); Notify(); }
        static void Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(store)); File.WriteAllText(store, new JavaScriptSerializer().Serialize(Preferences), new UTF8Encoding(false)); } catch { } }
        static void Notify() { if (Changed != null) Changed(null, EventArgs.Empty); }
        public static void TestStore(string file) { store = file; Preferences = Load(); }
        public static void Reload() { Preferences = Load(); Notify(); }
        static readonly DependencyProperty RecordsProperty = DependencyProperty.RegisterAttached("TranslationRecords", typeof(Dictionary<DependencyProperty, TextRecord>), typeof(Ui), new PropertyMetadata(null));
        static readonly DependencyProperty SkipProperty = DependencyProperty.RegisterAttached("PreserveAppearance", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("UiAttached", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        sealed class TextRecord { public string Original, Rendered; }
        public static void Preserve(DependencyObject element) { element.SetValue(SkipProperty, true); }
        public static void Translate(DependencyObject element, DependencyProperty property)
        {
            var current = element.GetValue(property) as string; if (current == null) return;
            var records = element.GetValue(RecordsProperty) as Dictionary<DependencyProperty, TextRecord>;
            if (records == null) { records = new Dictionary<DependencyProperty, TextRecord>(); element.SetValue(RecordsProperty, records); }
            TextRecord record; if (!records.TryGetValue(property, out record)) { record = new TextRecord { Original = current }; records[property] = record; }
            else if (current != record.Rendered) record.Original = current;
            record.Rendered = T(record.Original); element.SetValue(property, record.Rendered);
        }
        static readonly Dictionary<string, string> colors = new Dictionary<string, string> {
            { "#F7F5F2", "#181A1D" }, { "#FFFFFF", "#25282D" }, { "#232624", "#ECEFEC" }, { "#7B817D", "#A3AAA5" }, { "#E5E8E5", "#3D4440" }, { "#E9EDE7", "#303630" }, { "#FDE8DB", "#472D22" }, { "#CA4A16", "#FFAA75" }, { "#5B7460", "#A4CCAA" }, { "#E8EBE6", "#24282D" }, { "#F0F2EE", "#30353B" }, { "#B33A2E", "#FF8E82" }, { "#725335", "#C4C9CB" }, { "#694B32", "#D4D7D8" }
        };
        static Brush Remap(Brush brush, bool foreground)
        {
            var solid = brush as SolidColorBrush; if (solid == null || solid.Color.A == 0) return brush;
            Color c = solid.Color; string key = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"), target = null;
            if (foreground && key == "#FFFFFF") return brush;
            if (Dark) { if (foreground && key == "#000000") target = "#ECEFEC"; else colors.TryGetValue(key, out target); }
            else { var match = colors.FirstOrDefault(p => p.Value == key); if (match.Key != null) target = match.Key; }
            if (target == null) return brush; Color result = (Color)ColorConverter.ConvertFromString(target); result.A = c.A; return new SolidColorBrush(result);
        }
        static void Recolor(DependencyObject item, DependencyProperty property, bool foreground) { var value = item.GetValue(property) as Brush; if (value != null) item.SetValue(property, Remap(value, foreground)); }
        static Binding Parent(string name) { return new Binding(name) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) }; }
        static Style ComboStyle()
        {
            var style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("UiSurface"))); style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("UiInk"))); style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("UiLine"))); style.Setters.Add(new Setter(Control.MinHeightProperty, 31.0));
            var template = new ControlTemplate(typeof(ComboBox)); var grid = new FrameworkElementFactory(typeof(Grid));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetBinding(Border.BackgroundProperty, Parent("Background")); border.SetBinding(Border.BorderBrushProperty, Parent("BorderBrush")); grid.AppendChild(border);
            var toggle = new FrameworkElementFactory(typeof(ToggleButton)); toggle.SetValue(UIElement.FocusableProperty, false);
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton)); var hit = new FrameworkElementFactory(typeof(Border)); hit.SetValue(Border.BackgroundProperty, Brushes.Transparent); toggleTemplate.VisualTree = hit; toggle.SetValue(Control.TemplateProperty, toggleTemplate);
            var openBinding = Parent("IsDropDownOpen"); openBinding.Mode = BindingMode.TwoWay; toggle.SetBinding(ToggleButton.IsCheckedProperty, openBinding); grid.AppendChild(toggle);
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 5, 26, 5)); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); content.SetValue(UIElement.IsHitTestVisibleProperty, false); content.SetBinding(ContentPresenter.ContentProperty, Parent("SelectionBoxItem")); content.SetBinding(ContentPresenter.ContentTemplateProperty, Parent("SelectionBoxItemTemplate")); content.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "UiInk"); grid.AppendChild(content);
            var arrow = new FrameworkElementFactory(typeof(TextBlock)); arrow.SetValue(TextBlock.TextProperty, "▾"); arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right); arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 9, 0)); arrow.SetValue(UIElement.IsHitTestVisibleProperty, false); arrow.SetResourceReference(TextBlock.ForegroundProperty, "UiInk"); grid.AppendChild(arrow);
            var popup = new FrameworkElementFactory(typeof(Popup)); popup.Name = "PART_Popup"; popup.SetBinding(Popup.IsOpenProperty, Parent("IsDropDownOpen")); popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true); popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade); popup.SetValue(Popup.StaysOpenProperty, false);
            var panel = new FrameworkElementFactory(typeof(Border)); panel.SetValue(Border.CornerRadiusProperty, new CornerRadius(7)); panel.SetValue(Border.BorderThicknessProperty, new Thickness(1)); panel.SetValue(FrameworkElement.MaxHeightProperty, 260.0); panel.SetBinding(FrameworkElement.MinWidthProperty, Parent("ActualWidth")); panel.SetResourceReference(Border.BackgroundProperty, "UiSurface"); panel.SetResourceReference(Border.BorderBrushProperty, "UiLine");
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto); var items = new FrameworkElementFactory(typeof(ItemsPresenter)); scroll.AppendChild(items); panel.AppendChild(scroll); popup.AppendChild(panel); grid.AppendChild(popup); template.VisualTree = grid; style.Setters.Add(new Setter(Control.TemplateProperty, template)); return style;
        }
        static Style ComboItemStyle()
        {
            var style = new Style(typeof(ComboBoxItem)); style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("UiInk"))); style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            var template = new ControlTemplate(typeof(ComboBoxItem)); var border = new FrameworkElementFactory(typeof(Border)); border.Name = "ItemBorder"; border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4)); border.SetValue(FrameworkElement.MarginProperty, new Thickness(3)); border.SetBinding(Border.BackgroundProperty, Parent("Background")); var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 6, 9, 6)); border.AppendChild(content); template.VisualTree = border;
            var hover = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true }; hover.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Brush("#553F2E"))); hover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); template.Triggers.Add(hover); style.Setters.Add(new Setter(Control.TemplateProperty, template)); return style;
        }
        public static void Apply(Window window)
        {
            window.Resources["UiSurface"] = Theme.Surface; window.Resources["UiInk"] = Theme.Ink; window.Resources["UiLine"] = Theme.Line;
            var visited = new HashSet<DependencyObject>(); var pending = new Stack<DependencyObject>(); pending.Push(window);
            while (pending.Count > 0) {
                var item = pending.Pop(); if (!visited.Add(item)) continue;
                bool preserve = (bool)item.GetValue(SkipProperty);
                if (preserve) continue;
                if (!preserve) {
                    var w = item as Window; if (w != null) { Translate(w, Window.TitleProperty); if (!w.AllowsTransparency) w.Background = Theme.Paper; }
                    var text = item as TextBlock; if (text != null) { Translate(text, TextBlock.TextProperty); text.FlowDirection = Arabic && text.Text.Any(c => c >= 0x0600 && c <= 0x06FF) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; Recolor(text, TextBlock.ForegroundProperty, true); }
                    var content = item as ContentControl; if (content != null && content.Content is string) Translate(content, ContentControl.ContentProperty);
                    var control = item as Control; if (control != null) {
                        if (control is ComboBox && window.Resources.Contains(typeof(ComboBox))) { control.Style = (Style)window.Resources[typeof(ComboBox)]; control.Background = Theme.Surface; control.Foreground = Theme.Ink; control.BorderBrush = Theme.Line; }
                        if (control is ComboBoxItem && window.Resources.Contains(typeof(ComboBoxItem))) control.Style = (Style)window.Resources[typeof(ComboBoxItem)];
                        Recolor(control, Control.ForegroundProperty, true); Recolor(control, Control.BackgroundProperty, false); Recolor(control, Control.BorderBrushProperty, false);
                    }
                    var panel = item as Panel; if (panel != null) Recolor(panel, Panel.BackgroundProperty, false);
                    var border = item as Border; if (border != null) { Recolor(border, Border.BackgroundProperty, false); Recolor(border, Border.BorderBrushProperty, false); }
                }
                if (item is Visual) { int n = VisualTreeHelper.GetChildrenCount(item); for (int i = 0; i < n; i++) pending.Push(VisualTreeHelper.GetChild(item, i)); }
                foreach (object child in LogicalTreeHelper.GetChildren(item)) if (child is DependencyObject) pending.Push((DependencyObject)child);
            }
            window.InvalidateVisual();
        }
        public static void Attach(Window window, Action refresh = null)
        {
            if ((bool)window.GetValue(AttachedProperty)) return; window.SetValue(AttachedProperty, true);
            if (!window.Resources.Contains(typeof(ComboBox))) window.Resources.Add(typeof(ComboBox), ComboStyle());
            if (!window.Resources.Contains(typeof(ComboBoxItem))) window.Resources.Add(typeof(ComboBoxItem), ComboItemStyle());
            foreach (Type type in new[] { typeof(TextBox), typeof(ComboBox), typeof(CheckBox), typeof(ListBox) }) {
                if (window.Resources.Contains(type)) continue;
                var style = new Style(type); style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("UiInk")));
                if (type != typeof(CheckBox)) { style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("UiSurface"))); style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("UiLine"))); }
                if (type == typeof(TextBox)) style.Setters.Add(new Setter(TextBox.CaretBrushProperty, new DynamicResourceExtension("UiInk")));
                window.Resources.Add(type, style);
            }
            EventHandler changed = delegate { if (refresh != null) refresh(); Apply(window); };
            Changed += changed; window.Closed += delegate { Changed -= changed; }; window.Loaded += delegate { if (refresh != null) refresh(); Apply(window); };
            Apply(window);
        }
    }
}
