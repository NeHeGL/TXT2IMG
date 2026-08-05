using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

namespace TXT2IMG;

public sealed partial class MainWindow : Window
{
    private readonly IImageGenerator _imageGenerator = new LocalImageGenerator();

    // Populated up front (rather than in the constructor body) because the XAML uses
    // x:Bind Results[0..4] directly; those bindings are evaluated during InitializeComponent,
    // before any constructor statements run, so the 5 slots must already exist by then.
    public ObservableCollection<ResultItem> Results { get; } = new(new[]
    {
        new ResultItem(), new ResultItem(), new ResultItem(), new ResultItem(), new ResultItem()
    });

    // Alphabetized style list (ComboBox ItemsSource).
    private static readonly string[] StyleNames =
    [
        "None",
        "3D Render",
        "Anime",
        "Black & White",
        "Cartoon",
        "Claymation",
        "Comic Book",
        "Colorful Vector",
        "Concept Art",
        "Cubist",
        "Cyberpunk",
        "Dark Fantasy",
        "Fantasy Art",
        "Film Noir",
        "Futuristic",
        "Graphic Oil",
        "Icon",
        "Impressionist",
        "Ink Wash",
        "Kirigami",
        "Line Art",
        "Low Poly",
        "Minecraft",
        "Minimalist",
        "Oil Painting",
        "Origami",
        "Paper Cut Collage",
        "Paper Quilling",
        "Papercut Shadow Box",
        "Pixel Art",
        "Pointillism",
        "Pop Art",
        "Silhouette",
        "Soft Oil",
        "Stacked Papercut",
        "Steampunk",
        "Tribal",
        "Vintage Photo",
        "Watercolor",
        "Zentangle",
    ];

    public MainWindow()
    {
        this.InitializeComponent();
        ResultsPanel.ItemsSource = Results;
        StyleComboBox.ItemsSource = StyleNames;
        StyleComboBox.SelectedItem = "None";
        // Height is tuned so the preview box — locked to the selected aspect ratio and therefore
        // limited by the card's width, not its height — leaves no vertical slack. Any surplus
        // becomes empty gaps above and below it, since the box is centered in its row.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1160, 1090));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        UpdateStyleDescription();
    }

    private string SelectedStyle => StyleComboBox.SelectedItem?.ToString() ?? "None";

    // Known WinUI3 bug (unpackaged desktop apps in particular): the window is misreported as
    // "inactive," which breaks the automatic mouse-wheel-to-ScrollViewer routing a ComboBox
    // dropdown normally relies on — the scrollbar shows and can be dragged, but the wheel does
    // nothing (see microsoft-ui-xaml#8764/#8161/#10480). Find the popup ScrollViewer on open and
    // apply wheel via ChangeView. AlignPopupTopToElement pins the popup to the Style box after
    // scrolling the list to the top (same as origin/master).
    private ScrollViewer? _dropdownScrollViewer;
    private UIElement? _dropdownWheelSource;
    private ComboBox? _dropdownOwner;

    private void ScrollableComboBox_DropDownOpened(object sender, object e)
    {
        _dropdownOwner = sender as ComboBox;
        var xamlRoot = ((FrameworkElement)sender).XamlRoot;
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            if (popup.Child is not UIElement popupRoot) continue;
            if (FindDescendant<ScrollViewer>(popupRoot) is not { } scrollViewer)
                continue;

            _dropdownScrollViewer = scrollViewer;

            if (_dropdownWheelSource is { } previous)
                previous.RemoveHandler(UIElement.PointerWheelChangedEvent, (PointerEventHandler)DropdownWheelChanged);

            popupRoot.AddHandler(
                UIElement.PointerWheelChangedEvent,
                (PointerEventHandler)DropdownWheelChanged,
                handledEventsToo: true);
            _dropdownWheelSource = popupRoot;

            scrollViewer.ChangeView(null, 0, null, true);

            var source = (FrameworkElement)sender;
            DispatcherQueue.TryEnqueue(() => AlignPopupTopToElement(popup, popupRoot, source));
            break;
        }
    }

    private static void AlignPopupTopToElement(Popup popup, UIElement popupRoot, FrameworkElement anchor)
    {
        if (!popup.IsOpen) return;

        var anchorTop = anchor.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        var popupTop = popupRoot.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;

        popup.VerticalOffset += anchorTop - popupTop;
    }

    private void DropdownWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_dropdownScrollViewer is not { } scrollViewer) return;
        if (_dropdownOwner is not { } comboBox) return;

        var itemCount = comboBox.Items.Count;
        if (itemCount == 0) return;

        var delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        if (delta == 0) return;
        e.Handled = true;

        var itemHeight = scrollViewer.ExtentHeight / itemCount;
        if (itemHeight <= 0) return;

        var step = delta > 0 ? -itemHeight : itemHeight;
        var offset = Math.Clamp(scrollViewer.VerticalOffset + step, 0, scrollViewer.ScrollableHeight);
        scrollViewer.ChangeView(null, offset, null, true);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            if (FindDescendant<T>(child) is { } nested)
                return nested;
        }

        return null;
    }

    private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateStyleDescription();

    private void PromptTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        _ = GenerateAsync();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e) => _ = GenerateAsync();

    private async Task GenerateAsync()
    {
        var prompt = PromptTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            await ShowMessageAsync("Please enter a prompt.");
            return;
        }

        // IsEnabled alone doesn't visibly dim this button (its Background is set directly
        // in XAML, which overrides the template's disabled-state visuals), so force an
        // explicit opacity change too — otherwise pressing Enter looks like nothing happened.
        GenerateButton.IsEnabled = false;
        GenerateButton.Opacity = 0.55;
        PromptTextBox.IsEnabled = false;
        GenerateButton.Content = "Starting...";

        // Changing style/aspect mid-batch would apply to nothing — the in-flight batch already
        // captured its own settings — while looking like it should affect what's currently
        // generating. Locking everything until the batch finishes avoids that confusion.
        SetControlsEnabled(false);

        try
        {
            var style = SelectedStyle;
            var (fullPrompt, extraNegative, guidanceMultiplier) = BuildPrompt(prompt, style);

            var model = ModelCatalog.DreamShaperXLLightning;
            var guidanceScaleOverride = guidanceMultiplier == 1f ? (float?)null : model.GuidanceScale * guidanceMultiplier;

            var aspectChoice = ((ComboBoxItem)AspectComboBox.SelectedItem).Content?.ToString() ?? "Square";
            var (widthOverride, heightOverride) = ComputeDimensions(model, aspectChoice);

            // Capping the long edge at the model's native resolution (see ComputeDimensions) still
            // wasn't enough on its own to prevent duplicated/ghosted content on Landscape/Portrait
            // results. The actual bottleneck is denoising budget, not canvas size: DreamShaper XL
            // Lightning's LCM pipeline is tuned for resolving its native square shape, and a
            // non-square canvas leaves it less budget per unit of "unfamiliar layout" to work
            // with. Giving every non-square aspect 50% more steps buys back that budget; Square
            // is left untouched since it isn't affected.
            var inferenceStepsOverride = aspectChoice == "Square" ? (int?)null : (int)Math.Round(model.InferenceSteps * 1.5);

            // Impasto + Graphic Oil: same step boost Impasto used when it landed the liked look.
            if (style == "Impasto" || style == "Graphic Oil")
            {
                var steps = inferenceStepsOverride ?? model.InferenceSteps;
                inferenceStepsOverride = (int)Math.Round(steps * 1.5);
            }

            // Max Detail: more denoising steps only (not higher resolution). Doubles whatever
            // step count we would have used — slower, often cleaner anatomy/detail.
            if (MaxDetailCheckBox.IsChecked == true)
            {
                var steps = inferenceStepsOverride ?? model.InferenceSteps;
                inferenceStepsOverride = (int)Math.Round(steps * 2.0);
            }

            var statusCallback = new Progress<string>(status => GenerateButton.Content = status);
            var progressFillCallback = new Progress<double>(SetGenerateButtonFill);

            // Every generate produces a brand new set of results, replacing whatever was shown
            // before; each image is dropped into its slot as soon as it's ready instead of
            // waiting for the whole batch, so the row fills in live.
            for (var i = 0; i < Results.Count; i++)
            {
                Results[i].IsActive = false;
                Results[i].Bytes = null;
                Results[i].Thumbnail = null;
            }
            PreviewImage.Source = null;
            CurrentImageBytes = null;
            CurrentStyle = null;
            _currentPreviewIndex = -1;

            var firstImageShown = false;

            var imageCallback = new Progress<(int Index, byte[] Bytes)>(async result =>
            {
                try
                {
                    var (index, bytes) = result;
                    if (index >= Results.Count) return;

                    // Black & White: model still leaks color from prompts/lighting (live: orange water).
                    // Force grayscale on pixels — prompt strip alone is not enough.
                    if (style == "Black & White")
                        bytes = await PngGrayscale.ConvertAsync(bytes);

                    Results[index].Bytes = bytes;
                    Results[index].RawPrompt = prompt;
                    Results[index].Style = style;
                    Results[index].Thumbnail = await LoadBitmapImageAsync(bytes);

                    if (!firstImageShown)
                    {
                        firstImageShown = true;
                        ShowPreview(index);
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync($"Failed to load a generated image:\n\n{ex.GetType().Name}: {ex.Message}");
                }
            });

            await _imageGenerator.GenerateImagesAsync(
                model, fullPrompt, baseImageBytes: null, statusCallback, imageCallback,
                extraNegative, widthOverride, heightOverride, progressFillCallback,
                guidanceScaleOverride: guidanceScaleOverride,
                inferenceStepsOverride: inferenceStepsOverride);
        }
        catch (Exception ex)
        {
            // Nothing previously surfaced generation failures — a fire-and-forget task that
            // faults just silently resets the button, with zero indication anything went
            // wrong. This is almost certainly why "Generate" appeared to do nothing.
            await ShowMessageAsync($"Generation failed:\n\n{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            GenerateButton.Opacity = 1;
            PromptTextBox.IsEnabled = true;
            GenerateButton.Content = "Generate";
            ResetGenerateButtonBackground();
            SetControlsEnabled(true);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        StyleComboBox.IsEnabled = enabled;
        AspectComboBox.IsEnabled = enabled;
        MaxDetailCheckBox.IsEnabled = enabled;
        CopyButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
    }

    // While a model is downloading, the button's own background becomes the progress bar —
    // an accent-colored fill up to the download fraction, plain dark past that point — instead
    // of just a percentage in the (very narrow) button text.
    private void SetGenerateButtonFill(double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        var unfilled = Windows.UI.Color.FromArgb(255, 42, 41, 57);
        var filledStart = Windows.UI.Color.FromArgb(255, 140, 108, 255);
        var filledEnd = Windows.UI.Color.FromArgb(255, 53, 214, 196);

        var brush = new Microsoft.UI.Xaml.Media.LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 0)
        };
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = filledStart, Offset = 0 });
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = filledEnd, Offset = Math.Max(0.001, fraction) });
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = unfilled, Offset = Math.Min(1.0, fraction + 0.001) });
        brush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = unfilled, Offset = 1 });
        GenerateButton.Background = brush;
    }

    private void ResetGenerateButtonBackground()
    {
        GenerateButton.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentGradientBrush"];
    }

    private async void PreviewImage_Click(object sender, RoutedEventArgs e) => await CopyCurrentImageAsync();

    private void PreviewImageButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (CurrentImageBytes is null) return;
        PreviewHoverBorder.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentGradientBrush"];
    }

    private void PreviewImageButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        PreviewHoverBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await CopyCurrentImageAsync();

    private async Task CopyCurrentImageAsync()
    {
        if (CurrentImageBytes is null)
        {
            await ShowMessageAsync("No image to copy.");
            return;
        }

        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(await BytesToStreamAsync(CurrentImageBytes)));
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        await ShowCopiedToastAsync();
    }

    // A brief flash-and-fade toast instead of a dialog the user has to click through — this
    // matches how copy confirmation normally works elsewhere (clipboard managers, browsers).
    private async Task ShowCopiedToastAsync()
    {
        var fadeIn = new Storyboard();
        var fadeInAnimation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(150)) };
        Storyboard.SetTarget(fadeInAnimation, CopiedToast);
        Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");
        fadeIn.Children.Add(fadeInAnimation);
        fadeIn.Begin();

        await Task.Delay(1300);

        var fadeOut = new Storyboard();
        var fadeOutAnimation = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(500)) };
        Storyboard.SetTarget(fadeOutAnimation, CopiedToast);
        Storyboard.SetTargetProperty(fadeOutAnimation, "Opacity");
        fadeOut.Children.Add(fadeOutAnimation);
        fadeOut.Begin();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentImageBytes is null)
        {
            await ShowMessageAsync("No image to save.");
            return;
        }

        var picker = new FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        var fileName = SlugifyFileName(CurrentRawPrompt);
        if (_currentPreviewIndex >= 0)
        {
            fileName += $"-{_currentPreviewIndex + 1}";
        }
        if (!string.IsNullOrWhiteSpace(CurrentStyle))
        {
            fileName += $"-{SlugifyFileName(CurrentStyle)}";
        }
        picker.SuggestedFileName = fileName;
        picker.FileTypeChoices.Add("PNG Image", new[] { ".png" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        await FileIO.WriteBytesAsync(file, EmbedSaveMetadata(CurrentImageBytes));
    }

    // Invisible PNG tEXt — prompt and style only.
    private byte[] EmbedSaveMetadata(byte[] png)
    {
        var withPrompt = PngTextChunk.Embed(png, "TXT2IMG Prompt", CurrentRawPrompt ?? "");
        return PngTextChunk.Embed(withPrompt, "TXT2IMG Style", CurrentStyle ?? "");
    }

    private void ResultSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ResultItem item) return;
        if (item.Bytes is null) return;

        var index = Results.IndexOf(item);
        if (index < 0) return;
        ShowPreview(index);
    }

    private void ShowPreview(int index)
    {
        if (_currentPreviewIndex >= 0 && _currentPreviewIndex < Results.Count)
            Results[_currentPreviewIndex].IsActive = false;

        var item = Results[index];
        item.IsActive = true;

        PreviewImage.Source = item.Thumbnail;
        CurrentImageBytes = item.Bytes;
        CurrentRawPrompt = item.RawPrompt;
        CurrentStyle = item.Style;
        _currentPreviewIndex = index;
        if (!string.IsNullOrEmpty(item.RawPrompt))
        {
            PromptTextBox.Text = item.RawPrompt;
        }
    }

    // Turns the user's own prompt into a filesystem-safe filename — this is a local text
    // slugify, not a call to an AI service: an actual AI summarization call would need a
    // network round-trip (and likely an API fee), which breaks the 100%-local/no-cost
    // guarantee the rest of this app is built around.
    private static string SlugifyFileName(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "TXT2IMG";

        var builder = new StringBuilder();
        var lastWasDash = false;
        foreach (var ch in prompt.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 60)
        {
            slug = slug[..60].TrimEnd('-');
        }
        return string.IsNullOrEmpty(slug) ? "TXT2IMG" : slug;
    }

    private static (string Prompt, string ExtraNegative, float GuidanceScaleMultiplier) BuildPrompt(string prompt, string style) =>
        StylePromptBuilder.BuildPrompt(prompt, style);

    private void UpdateStyleDescription()
    {
        if (StyleDescriptionText is null) return;

        var style = SelectedStyle;
        StyleDescriptionText.Text = style switch
        {
            "Cartoon" => "Cartoon — bold outlines, bright colors, full scene",
            "Icon" => "Flat vector logo/icon — one centered glyph, minimal palette",
            "Comic Book" => "Graphic novel comic — bold ink, halftone, dynamic angle",
            "Anime" => "Manga/cel anime — clean line art, masterpiece quality tags",
            "Watercolor" => "Wet-on-wet watercolor — paper texture, pastel tones",
            "Graphic Oil" => "Hard-edged textured oil — blocky palette-knife paint on the subject",
            "Oil Painting" => "Classical oil — thick brushstrokes, canvas texture, museum",
            "Soft Oil" => "Soft atmospheric oil — blended light, gentle painterly scene",
            "Claymation" => "Stop-motion clay — sculpted texture, no stray hands",
            "Fantasy Art" => "Epic fantasy digital painting — dramatic lighting, mythical mood",
            "Dark Fantasy" => "Dark medieval fantasy — muted tones, epic mood",
            "Pixel Art" => "16-bit RPG pixel art — clean hard pixels, limited palette",
            "3D Render" => "Pixar/Blender CGI — smooth natural studio lighting",
            "Futuristic" => "Clean sci-fi — chrome, blue-white light, sleek tech (not neon cyberpunk)",
            "Black & White" => "Sharp monochrome photo — full subject in scene, rich grayscale",
            "Vintage Photo" => "Yellowed film — grain, scratches, light leaks",
            "Concept Art" => "ArtStation digital painting — epic composition, cinematic",
            "Cyberpunk" => "Neon night city — rain, holograms, magenta/cyan dystopia",
            "Colorful Vector" => "Flat colorful vector — black background, bold saturated colors",
            "Minimalist" => "Flat vector minimalist — 5 colors, no gradients",
            "Cubist" => "Cubist — geometric facets, abstract planes, fragmented form",
            "Ink Wash" => "Chinese ink wash / sumi-e — soft washes, brush calligraphy",
            "Kirigami" => "Kirigami — cut-and-fold paper, 3D Japanese precision",
            "Line Art" => "Line art — sleek minimalist vector outlines",
            "Low Poly" => "Low poly — blocky game-art meshes, hard edges",
            "Minecraft" => "Minecraft — blocky pixel voxels, vibrant game look",
            "Film Noir" => "Film noir — high-contrast mono, 1940s dramatic shadows",
            "Impressionist" => "Impressionist — loose brushwork, light over form",
            "Pointillism" => "Pointillism — image built from tiny dots of color",
            "Pop Art" => "Pop art — bright colors, bold outlines, kitsch",
            "Steampunk" => "Steampunk — brass, copper, gears, antique mechanical",
            "Origami" => "Origami — folded paper, pleats, clean geometric form",
            "Paper Quilling" => "Paper quilling — coiled strips, ornamental loops",
            "Paper Cut Collage" => "Paper cut collage — layered mixed-media paper cuts",
            "Papercut Shadow Box" => "Papercut shadow box — layered silhouettes with depth",
            "Silhouette" => "Silhouette — high-contrast black-and-white shapes",
            "Stacked Papercut" => "Stacked papercut — precision-cut layered depth",
            "Tribal" => "Tribal — bold indigenous patterns, natural colors",
            "Zentangle" => "Zentangle — intricate monochrome meditative patterns",
            _ => "No style applied — just your prompt, as the model interprets it by default", // None
        };
    }

    // Reshaping to the same total area as the native square (the previous approach) stretches the
    // long edge past SampleSize for every non-square aspect — e.g. a 768 base at 16:9 reshaped up
    // to 1024 wide — which reintroduces both the GPU-driver-watchdog risk and the anatomy/limb
    // duplication artifacts that the 768 baseline (see ModelCatalog — a TDR mitigation) exists to
    // avoid: observed in practice on landscape Oil Painting and Fantasy Art results. Capping the
    // long edge at SampleSize and scaling the short edge down from there keeps every aspect ratio
    // within the same tested-safe resolution as the square default, trading a smaller total image
    // area on non-square aspects for reliably correct anatomy.
    private static (int Width, int Height) ComputeDimensions(ModelDefinition model, string aspect)
    {
        var (aspectWidth, aspectHeight) = GetAspectRatio(aspect);

        double width, height;
        if (aspectWidth >= aspectHeight)
        {
            width = model.SampleSize;
            height = model.SampleSize * aspectHeight / aspectWidth;
        }
        else
        {
            height = model.SampleSize;
            width = model.SampleSize * aspectWidth / aspectHeight;
        }

        return (RoundToMultipleOf64(width), RoundToMultipleOf64(height));
    }

    // SDXL's VAE downsamples by a factor of 8; rounding to a multiple of 64 (rather than just 8)
    // keeps every internal U-Net feature map size even, avoiding the seam artifacts that odd
    // intermediate sizes can produce.
    private static int RoundToMultipleOf64(double value) => Math.Max(64, (int)Math.Round(value / 64.0) * 64);

    private static (double Width, double Height) GetAspectRatio(string aspect) => aspect switch
    {
        "Landscape (16:9)" => (16.0, 9.0),
        "Portrait (9:16)" => (9.0, 16.0),
        "Landscape (4:3)" => (4.0, 3.0),
        "Portrait (3:4)" => (3.0, 4.0),
        _ => (1.0, 1.0),
    };

    // Deferred to the next dispatcher tick rather than run inline: mutating Width/Height
    // synchronously from inside SizeChanged re-enters the native layout pass and can crash
    // WinUI's layout engine outright (observed as an access violation in Microsoft.UI.Xaml.dll,
    // not a catchable .NET exception) instead of just misbehaving.
    private void AspectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdatePreviewAspect);
        DispatcherQueue.TryEnqueue(UpdateResultSlotsAspect);
    }

    private void PreviewCardGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdatePreviewAspect);

    private void ResultsCardBorder_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateResultSlotsAspect);

    // Reshapes the Preview box to match the selected aspect ratio, clamped to the image host.
    private void UpdatePreviewAspect()
    {
        if (AspectComboBox?.SelectedItem is not ComboBoxItem selected) return;
        var (aspectWidth, aspectHeight) = GetAspectRatio(selected.Content?.ToString() ?? "Square");

        var previewHeight = ApplyAspectBox(PreviewBorder, PreviewImageHost.ActualWidth, aspectWidth, aspectHeight);

        if (previewHeight > 0) PreviewPlaceholderText.FontSize = Math.Clamp(previewHeight * 0.065, 14, 26);
    }

    // Fits within the host width and height, whichever is tighter.
    private double ApplyAspectBox(FrameworkElement box, double hostWidth, double aspectWidth, double aspectHeight)
    {
        var maxHeight = PreviewImageHost.ActualHeight - box.Margin.Top - box.Margin.Bottom;
        if (hostWidth <= 0 || maxHeight <= 0) return 0;

        var height = hostWidth * aspectHeight / aspectWidth;
        if (height > maxHeight)
        {
            height = maxHeight;
            box.Width = maxHeight * aspectWidth / aspectHeight;
            box.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            box.Width = double.NaN;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        box.Height = height;
        return height;
    }

    // Compact results strip: preferred thumbnail height, then shrink if width can't fit
    // 5 slots + style description. Independent of Preview height (no layout feedback loop).
    private void UpdateResultSlotsAspect()
    {
        if (AspectComboBox?.SelectedItem is not ComboBoxItem selected) return;
        if (ResultsPanel.ItemsPanelRoot is not StackPanel panel) return;
        var (aspectWidth, aspectHeight) = GetAspectRatio(selected.Content?.ToString() ?? "Square");

        const double preferredSlotHeight = 96;
        const double widthSafetyMargin = 6;
        var availableWidth = ResultsCardBorder.ActualWidth
            - ResultsCardBorder.Padding.Left - ResultsCardBorder.Padding.Right
            - ResultsSpacerColumn.ActualWidth - widthSafetyMargin;
        if (availableWidth <= 0) return;

        var totalSpacing = panel.Spacing * (Results.Count - 1);
        var widthPerUnitHeight = Results.Count * aspectWidth / aspectHeight + 16.0 / 9.0;
        var slotHeightFromWidth = (availableWidth - totalSpacing) / widthPerUnitHeight;

        var slotHeight = Math.Min(preferredSlotHeight, slotHeightFromWidth);
        if (slotHeight <= 0) return;
        var slotWidth = slotHeight * aspectWidth / aspectHeight;

        StyleDescriptionBorder.Height = slotHeight;
        StyleDescriptionBorder.Width = slotHeight * 16.0 / 9.0;
        StyleDescriptionText.FontSize = Math.Clamp(slotHeight * 0.11, 10, 16);

        foreach (var child in panel.Children)
        {
            if (child is ContentPresenter presenter
                && VisualTreeHelper.GetChildrenCount(presenter) > 0
                && VisualTreeHelper.GetChild(presenter, 0) is FrameworkElement root)
            {
                root.Width = slotWidth;
                root.Height = slotHeight;
            }
        }
    }

    private async Task<BitmapImage> LoadBitmapImageAsync(byte[] imageBytes)
    {
        var image = new BitmapImage();
        using var ms = new MemoryStream(imageBytes);
        await image.SetSourceAsync(ms.AsRandomAccessStream());
        return image;
    }

    private async Task<Windows.Storage.Streams.IRandomAccessStream> BytesToStreamAsync(byte[] imageBytes)
    {
        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var writer = new Windows.Storage.Streams.DataWriter(stream);
        writer.WriteBytes(imageBytes);
        await writer.StoreAsync();
        stream.Seek(0);
        return stream;
    }

    private async Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "TXT2IMG",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private byte[]? CurrentImageBytes;
    private string? CurrentRawPrompt;
    private string? CurrentStyle;
    private int _currentPreviewIndex = -1;
}

public class ResultItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    private byte[]? _bytes;
    public byte[]? Bytes
    {
        get => _bytes;
        set
        {
            _bytes = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bytes)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasImage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        }
    }

    public string? RawPrompt { get; set; }
    public string? Style { get; set; }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        }
    }

    public bool HasImage => Bytes is not null;

    public Microsoft.UI.Xaml.Media.SolidColorBrush ActiveBrush =>
        new(_isActive ? Microsoft.UI.Colors.MediumPurple : Microsoft.UI.Colors.Transparent);

    public string Hint =>
        Bytes is null
            ? "Nothing generated yet"
            : _isActive
                ? "Currently previewed"
                : "Click to preview";
}
