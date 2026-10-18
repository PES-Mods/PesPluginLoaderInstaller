using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PesPluginLoaderInstaller
{
    // Text with an optional outline and shadow, so it stays readable on the bright background.
    // Only the shadow layer is blurred; the text layer has no effect, so it keeps ClearType.
    public class OutlinedTextBlock : FrameworkElement
    {
        readonly DrawingVisual blurredShadowVisual = new DrawingVisual();
        readonly DrawingVisual textVisual = new DrawingVisual();

        public OutlinedTextBlock()
        {
            UseLayoutRounding = true;
            AddVisualChild(blurredShadowVisual);
            AddVisualChild(textVisual);
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index)
        {
            return index == 0 ? blurredShadowVisual : textVisual;
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            "Fill", typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            "Stroke", typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            "StrokeThickness", typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowBrushProperty = DependencyProperty.Register(
            "ShadowBrush", typeof(Brush), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowBlurRadiusProperty = DependencyProperty.Register(
            "ShadowBlurRadius", typeof(double), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
            "TextAlignment", typeof(TextAlignment), typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text { get { return (string)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
        public Brush Fill { get { return (Brush)GetValue(FillProperty); } set { SetValue(FillProperty, value); } }
        public Brush Stroke { get { return (Brush)GetValue(StrokeProperty); } set { SetValue(StrokeProperty, value); } }
        // Shadow color: a soft halo when ShadowBlurRadius > 0, otherwise a sharp copy 1 device pixel down and right
        public Brush ShadowBrush { get { return (Brush)GetValue(ShadowBrushProperty); } set { SetValue(ShadowBrushProperty, value); } }
        // Blur radius of a soft copy of the shadow; 0 means no soft shadow
        public double ShadowBlurRadius { get { return (double)GetValue(ShadowBlurRadiusProperty); } set { SetValue(ShadowBlurRadiusProperty, value); } }
        public double StrokeThickness { get { return (double)GetValue(StrokeThicknessProperty); } set { SetValue(StrokeThicknessProperty, value); } }
        public TextAlignment TextAlignment { get { return (TextAlignment)GetValue(TextAlignmentProperty); } set { SetValue(TextAlignmentProperty, value); } }
        public FontFamily FontFamily { get { return (FontFamily)GetValue(FontFamilyProperty); } set { SetValue(FontFamilyProperty, value); } }
        public double FontSize { get { return (double)GetValue(FontSizeProperty); } set { SetValue(FontSizeProperty, value); } }
        public FontWeight FontWeight { get { return (FontWeight)GetValue(FontWeightProperty); } set { SetValue(FontWeightProperty, value); } }

        FormattedText CreateFormattedText(double maxWidth, Brush brush = null)
        {
            var formattedText = new FormattedText(
                Text ?? "",
                CultureInfo.CurrentUICulture,
                FlowDirection,
                new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
                FontSize,
                brush ?? Fill,
                null,
                TextFormattingMode.Display,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formattedText.TextAlignment = TextAlignment;
            formattedText.Trimming = TextTrimming.None;
            if (!double.IsInfinity(maxWidth) && maxWidth > 0)
                formattedText.MaxTextWidth = maxWidth;
            return formattedText;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var formattedText = CreateFormattedText(Math.Max(0, availableSize.Width - StrokeThickness));
            double width = double.IsInfinity(availableSize.Width) ? formattedText.WidthIncludingTrailingWhitespace : availableSize.Width - StrokeThickness;
            if (TextAlignment == TextAlignment.Left)
                width = Math.Min(width, formattedText.WidthIncludingTrailingWhitespace);
            return new Size(Math.Ceiling(width + StrokeThickness), Math.Ceiling(formattedText.Height + StrokeThickness));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // measured width is exact, so leave a little slack to avoid unwanted wrapping
            double maxWidth = Math.Max(1, ActualWidth - StrokeThickness / 2 + 1);
            Point origin = new Point(StrokeThickness / 2, StrokeThickness / 2);
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Point shadowOrigin = new Point(origin.X + 1 / dpi.DpiScaleX, origin.Y + 1 / dpi.DpiScaleY);

            using (DrawingContext dc = blurredShadowVisual.RenderOpen())
            {
                if (ShadowBrush != null && ShadowBlurRadius > 0)
                {
                    // copies around the text, shifted half a pixel down-right, drawn twice so the halo stays dense after blurring,
                    // like the shadow under desktop icon labels
                    var shadowText = CreateFormattedText(maxWidth, ShadowBrush);
                    for (int pass = 0; pass < 2; pass++)
                        for (int y = -1; y <= 1; y++)
                            for (int x = -1; x <= 1; x++)
                                dc.DrawText(shadowText, new Point(origin.X + (x + 0.5) / dpi.DpiScaleX, origin.Y + (y + 0.5) / dpi.DpiScaleY));
                }
            }
            blurredShadowVisual.Effect = ShadowBlurRadius > 0 ? new BlurEffect { Radius = ShadowBlurRadius, KernelType = KernelType.Gaussian } : null;

            using (DrawingContext dc = textVisual.RenderOpen())
            {
                if (ShadowBrush != null && ShadowBlurRadius <= 0)
                    dc.DrawText(CreateFormattedText(maxWidth, ShadowBrush), shadowOrigin);
                // Glyph outlines from BuildGeometry are unhinted, so a thin stroke around them lands
                // between device pixels and looks blurry. Instead draw the hinted text several times,
                // shifted by whole device pixels, in the outline color, then the text itself on top.
                if (StrokeThickness > 0 && Stroke != null)
                {
                    int radiusX = Math.Max(1, (int)Math.Round(StrokeThickness / 2 * dpi.DpiScaleX));
                    int radiusY = Math.Max(1, (int)Math.Round(StrokeThickness / 2 * dpi.DpiScaleY));
                    var outlineText = CreateFormattedText(maxWidth, Stroke);
                    for (int y = -radiusY; y <= radiusY; y++)
                    {
                        for (int x = -radiusX; x <= radiusX; x++)
                        {
                            if (x == 0 && y == 0)
                                continue;
                            dc.DrawText(outlineText, new Point(origin.X + x / dpi.DpiScaleX, origin.Y + y / dpi.DpiScaleY));
                        }
                    }
                }
                dc.DrawText(CreateFormattedText(maxWidth, Fill), origin);
            }
        }
    }
}
