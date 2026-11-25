using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Input;
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
    private bool _modelDownloadActive;
    private const string FirstRunDownloadMessage =
        "First run downloads the model (this can take some time).";

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

    private readonly GpuCapabilities _gpuCapabilities;

    public MainWindow()
    {
        this.InitializeComponent();
        ResultsPanel.ItemsSource = Results;
        StyleComboBox.ItemsSource = StyleNames;
        StyleComboBox.SelectedItem = "None";

        _gpuCapabilities = GpuCapabilitiesProbe.Detect();
        InitializeResolutionOptions();
        ToolTipService.SetToolTip(ResolutionComboBox, _gpuCapabilities.SupportsExtendedLightningSteps
            ? "768 or 896 (long edge). 896 is the GPU cap; up to 44 Lightning steps at 896."
            : _gpuCapabilities.SupportsHighVramQuality
                ? "768 or 896 (long edge). 896 is the GPU cap; this GPU uses a stronger denoise recipe at 896."
                : "768 or 896 (long edge). 896 is the highest safe GPU resolution on this card.");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1160, 1090));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        UpdateStyleDescription();
        RootGrid.Loaded += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);
            DispatcherQueue.TryEnqueue(RefreshHeaderSecondLine);
        };
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange)
                DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);
        };
    }

    private string SelectedStyle => StyleComboBox.SelectedItem?.ToString() ?? "None";

    private void RefreshHeaderSecondLine()
    {
        if (NewVariationHintText is null || FirstRunDownloadText is null)
            return;
        if (_modelDownloadActive)
            return;

        var present = ModelDownloader.IsModelPresent(ModelCatalog.DreamShaperXLLightning);
        if (present)
        {
            FirstRunDownloadText.Visibility = Visibility.Collapsed;
            NewVariationHintText.Visibility = Visibility.Visible;
            return;
        }

        // Bold yellow — notice that the first Generate will download the model.
        SetFirstRunWarningStyle();
        FirstRunDownloadText.Text = FirstRunDownloadMessage;
        FirstRunDownloadText.Visibility = Visibility.Visible;
        NewVariationHintText.Visibility = Visibility.Collapsed;
    }

    // Second line: bold yellow first-run notice → light-green download status → muted "New Variation" hint.
    // Progress lives on the Generate button fill only — this line is status text.
    private static string ActiveDownloadMessage =>
        $"Currently downloading the {ModelCatalog.DreamShaperXLLightning.DisplayName} model.";

    private void BeginHeaderModelDownloadIfNeeded()
    {
        if (ModelDownloader.IsModelPresent(ModelCatalog.DreamShaperXLLightning))
            return;

        _modelDownloadActive = true;
        NewVariationHintText.Visibility = Visibility.Collapsed;
        SetDownloadingStyle();
        FirstRunDownloadText.Text = ActiveDownloadMessage;
        FirstRunDownloadText.Visibility = Visibility.Visible;
    }

    private void EndHeaderModelDownload()
    {
        _modelDownloadActive = false;
        RefreshHeaderSecondLine();
    }

    private void SetFirstRunWarningStyle()
    {
        FirstRunDownloadText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        FirstRunDownloadText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 224, 64));
    }

    private void SetDownloadingStyle()
    {
        FirstRunDownloadText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        FirstRunDownloadText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 220, 140));
    }

    private Progress<double> CreateProgressFillCallback() =>
        new Progress<double>(SetGenerateButtonFill);

    // Button status goes Downloading → Loading → Generating. As soon as download is done,
    // flip the second line to the muted "New Variation" hint (don't wait for the batch to finish).
    private Progress<string> CreateStatusCallback() =>
        new Progress<string>(status =>
        {
            GenerateButton.Content = status;
            if (_modelDownloadActive
                && !status.StartsWith("Downloading", StringComparison.OrdinalIgnoreCase))
            {
                EndHeaderModelDownload();
            }
        });

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

    private void PromptTextContextFlyout_Opening(object sender, object e)
    {
        var hasSelection = PromptTextBox.SelectionLength > 0;
        PromptCutMenuItem.IsEnabled = hasSelection;
        PromptCopyMenuItem.IsEnabled = hasSelection;
        PromptPasteMenuItem.IsEnabled = Clipboard.GetContent().Contains(StandardDataFormats.Text);
    }

    private void PromptContextCut_Click(object sender, RoutedEventArgs e)
    {
        if (PromptTextBox.SelectionLength <= 0) return;
        var start = PromptTextBox.SelectionStart;
        var len = PromptTextBox.SelectionLength;
        var slice = PromptTextBox.Text.Substring(start, len);
        var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        data.SetText(slice);
        Clipboard.SetContent(data);
        PromptTextBox.Text = PromptTextBox.Text.Remove(start, len);
        PromptTextBox.SelectionStart = start;
        PromptTextBox.SelectionLength = 0;
    }

    private void PromptContextCopy_Click(object sender, RoutedEventArgs e)
    {
        if (PromptTextBox.SelectionLength <= 0) return;
        var start = PromptTextBox.SelectionStart;
        var len = PromptTextBox.SelectionLength;
        var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        data.SetText(PromptTextBox.Text.Substring(start, len));
        Clipboard.SetContent(data);
    }

    private async void PromptContextPaste_Click(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text)) return;

        var text = await content.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return;

        var start = Math.Clamp(PromptTextBox.SelectionStart, 0, PromptTextBox.Text.Length);
        var len = Math.Min(PromptTextBox.SelectionLength, PromptTextBox.Text.Length - start);
        var current = PromptTextBox.Text;
        PromptTextBox.Text = current[..start] + text + current[(start + len)..];
        PromptTextBox.SelectionStart = start + text.Length;
        PromptTextBox.SelectionLength = 0;
        DispatcherQueue.TryEnqueue(ScrollPromptBoxForCaret);
    }

    private void PromptContextSelectAll_Click(object sender, RoutedEventArgs e)
    {
        PromptTextBox.SelectionStart = 0;
        PromptTextBox.SelectionLength = PromptTextBox.Text.Length;
    }

    private void PromptTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (shift)
            return;

        e.Handled = true;
        _ = GenerateAsync();
    }

    private void PromptTextBox_GotFocus(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ScrollPromptBoxForCaret);

    private void PromptTextBox_SelectionChanged(object sender, RoutedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ScrollPromptBoxForCaret);

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ScrollPromptBoxForCaret);

    private ScrollViewer? GetPromptScrollViewer() =>
        FindDescendant<ScrollViewer>(PromptTextBox);

    /// <summary>Two-line viewport: when content overflows, keep the tail visible while typing at the end.</summary>
    private void ScrollPromptBoxForCaret()
    {
        try
        {
            if (GetPromptScrollViewer() is not ScrollViewer scrollViewer)
                return;

            if (scrollViewer.ScrollableHeight <= 0)
                return;

            var textLen = PromptTextBox.Text?.Length ?? 0;
            var atEnd = PromptTextBox.SelectionStart >= textLen;
            if (atEnd)
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, true);
        }
        catch
        {
            // Ignore re-entrant scroll while paste/context menu updates the box.
        }
    }

    private static void ScrollPromptBoxToEnd(TextBox box)
    {
        if (FindDescendant<ScrollViewer>(box) is not ScrollViewer scrollViewer)
            return;

        if (scrollViewer.ScrollableHeight > 0)
            scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, true);
    }

    private void PromptBox_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (GetPromptScrollViewer() is not ScrollViewer scrollViewer)
            return;

        var delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        e.Handled = true;
        var step = delta > 0 ? -20.0 : 20.0;
        var offset = Math.Clamp(scrollViewer.VerticalOffset + step, 0, scrollViewer.ScrollableHeight);
        scrollViewer.ChangeView(null, offset, null, true);
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e) => _ = GenerateAsync();

    private void VaryButton_Click(object sender, RoutedEventArgs e) => _ = VaryAsync();

    private GenerationProfile.QualityTier SelectedQualityTier
    {
        get
        {
            if (ResolutionComboBox.SelectedItem is ComboBoxItem item)
                return GenerationProfile.ParseTier(item.Tag, item.Content?.ToString());
            return GenerationProfile.QualityTier.Fast;
        }
    }

    private bool Upscale4K => Upscale4KCheckBox.IsChecked == true;

    private void InitializeResolutionOptions()
    {
        ResolutionComboBox.Items.Clear();
        ResolutionComboBox.Items.Add(new ComboBoxItem { Content = "768", Tag = GenerationProfile.QualityTier.Fast });
        ResolutionComboBox.Items.Add(new ComboBoxItem { Content = "896", Tag = GenerationProfile.QualityTier.Quality });

        ResolutionComboBox.SelectedIndex = 0;
    }

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
            var plan = BuildGenerationPlan(prompt, style);
            var generationAspect = GetSelectedAspectLabel();

            BeginHeaderModelDownloadIfNeeded();
            var statusCallback = CreateStatusCallback();
            var progressFillCallback = CreateProgressFillCallback();

            // Every generate produces a brand new set of results, replacing whatever was shown
            // before; each image is dropped into its slot as soon as it's ready instead of
            // waiting for the whole batch, so the row fills in live.
            for (var i = 0; i < Results.Count; i++)
            {
                Results[i].IsActive = false;
                Results[i].Bytes = null;
                Results[i].Thumbnail = null;
                Results[i].GenerationFailed = false;
                Results[i].GenerationAspectLabel = null;
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
                    if (await PngSanity.IsMostlyBlankAsync(bytes))
                    {
                        Results[index].GenerationFailed = true;
                        return;
                    }

                    if (style == "Black & White")
                        bytes = await PngGrayscale.ConvertAsync(bytes);

                    Results[index].NativeGenerationBytes = bytes;
                    if (Upscale4K)
                        bytes = await ImageUpscale.UpscaleTo4KLongEdgeAsync(bytes);

                    Results[index].GenerationFailed = false;
                    Results[index].Bytes = bytes;
                    Results[index].RawPrompt = prompt;
                    Results[index].Style = style;
                    Results[index].GenerationAspectLabel = generationAspect;
                    Results[index].UpdateViewStretch(GetSelectedAspectLabel());
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
                plan.Model, plan.FullPrompt, baseImageBytes: null, statusCallback, imageCallback,
                plan.ExtraNegative, plan.Width, plan.Height, progressFillCallback,
                guidanceScaleOverride: plan.GuidanceScaleOverride,
                inferenceStepsOverride: plan.InferenceSteps);
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
            EndHeaderModelDownload();
        }
    }

    private async Task VaryAsync()
    {
        if (CurrentImageBytes is null || _currentPreviewIndex < 0)
        {
            await ShowMessageAsync("Select a result first, then New Variation.");
            return;
        }

        var varyIndex = _currentPreviewIndex;
        var item = Results[varyIndex];
        var prompt = item.RawPrompt;
        var style = item.Style ?? "None";
        if (string.IsNullOrWhiteSpace(prompt))
        {
            await ShowMessageAsync("This slot has no stored prompt — generate or pick another result.");
            return;
        }

        var sourceBytes = item.NativeGenerationBytes ?? CurrentImageBytes;

        GenerateButton.IsEnabled = false;
        GenerateButton.Opacity = 0.55;
        PromptTextBox.IsEnabled = false;
        GenerateButton.Content = "New Variation...";
        SetControlsEnabled(false);

        try
        {
            var aspectChoice = item.GenerationAspectLabel ?? GetSelectedAspectLabel();
            var plan = BuildGenerationPlan(prompt, style, aspectChoice);
            var varySteps = GenerationProfile.ComputeVarySteps(plan.InferenceSteps);

            BeginHeaderModelDownloadIfNeeded();
            var statusCallback = CreateStatusCallback();
            var progressFillCallback = CreateProgressFillCallback();

            var (srcWidth, srcHeight) = await PngSanity.GetPixelSizeAsync(sourceBytes);

            var images = await _imageGenerator.GenerateImagesAsync(
                plan.Model, plan.FullPrompt, baseImageBytes: sourceBytes, statusCallback, imageCallback: null,
                plan.ExtraNegative, srcWidth, srcHeight, progressFillCallback,
                strength: GenerationProfile.VaryStrength,
                guidanceScaleOverride: plan.GuidanceScaleOverride,
                inferenceStepsOverride: varySteps,
                batchSizeOverride: 1);

            if (images.Count == 0 || images[0] is not { } varied)
            {
                await ShowMessageAsync("New Variation did not return an image.");
                return;
            }

            if (await PngSanity.IsMostlyBlankAsync(varied))
            {
                await ShowMessageAsync("New Variation returned a blank image — try again or lower resolution.");
                return;
            }

            if (style == "Black & White")
                varied = await PngGrayscale.ConvertAsync(varied);

            item.NativeGenerationBytes = varied;
            if (Upscale4K)
                varied = await ImageUpscale.UpscaleTo4KLongEdgeAsync(varied);

            item.Bytes = varied;
            item.RawPrompt = prompt;
            item.Style = style;
            item.GenerationFailed = false;
            item.UpdateViewStretch(GetSelectedAspectLabel());
            item.Thumbnail = await LoadBitmapImageAsync(varied);
            ShowPreview(varyIndex);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"New Variation failed:\n\n{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            GenerateButton.Opacity = 1;
            PromptTextBox.IsEnabled = true;
            GenerateButton.Content = "Generate";
            ResetGenerateButtonBackground();
            SetControlsEnabled(true);
            EndHeaderModelDownload();
        }
    }

    private sealed record GenerationPlan(
        ModelDefinition Model,
        string FullPrompt,
        string ExtraNegative,
        int Width,
        int Height,
        int InferenceSteps,
        float? GuidanceScaleOverride);

    private GenerationPlan BuildGenerationPlan(string userPrompt, string style, string? aspectChoiceOverride = null)
    {
        var aspectChoice = aspectChoiceOverride
            ?? ((ComboBoxItem)AspectComboBox.SelectedItem).Content?.ToString() ?? "Square";
        var (fullPrompt, extraNegative, guidanceMultiplier) = BuildPrompt(userPrompt, style);

        var model = ModelCatalog.DreamShaperXLLightning;
        var tier = GenerationProfile.ResolveEffectiveTier(SelectedQualityTier, _gpuCapabilities.SupportsHighVramQuality);
        var baseGuidance = GenerationProfile.GetBaseGuidance(model, tier);
        var guidanceScaleOverride = guidanceMultiplier == 1f ? (float?)null : baseGuidance * guidanceMultiplier;
        var sampleSize = GenerationProfile.GetSampleSize(model, tier);
        var (width, height) = ComputeDimensions(sampleSize, aspectChoice);

        var baseSteps = GenerationProfile.GetBaseSteps(model, tier);
        var inferenceSteps = GenerationProfile.ComputeInferenceSteps(
            baseSteps, aspectChoice, style, _gpuCapabilities.SupportsExtendedLightningSteps, tier);

        return new GenerationPlan(model, fullPrompt, extraNegative, width, height, inferenceSteps, guidanceScaleOverride);
    }

    private void SetControlsEnabled(bool enabled)
    {
        StyleComboBox.IsEnabled = enabled;
        AspectComboBox.IsEnabled = enabled;
        ResolutionComboBox.IsEnabled = enabled;
        Upscale4KCheckBox.IsEnabled = enabled;
        CopyButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
        VaryButton.IsEnabled = enabled && CurrentImageBytes is not null;
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

        CurrentImageBytes = item.Bytes;
        CurrentRawPrompt = item.RawPrompt;
        CurrentStyle = item.Style;
        _currentPreviewIndex = index;
        VaryButton.IsEnabled = item.Bytes is not null;
        if (!string.IsNullOrEmpty(item.RawPrompt))
        {
            PromptTextBox.Text = item.RawPrompt;
            PromptTextBox.SelectionStart = item.RawPrompt.Length;
            DispatcherQueue.TryEnqueue(() => ScrollPromptBoxToEnd(PromptTextBox));
        }

        // Separate BitmapImage from thumbnails — sharing one instance across two Image controls
        // with different layout slots breaks measure when aspect changes.
        UpdatePreviewImageStretch();
        _ = RefreshPreviewImageAsync();
    }

    private async Task RefreshPreviewImageAsync()
    {
        if (_currentPreviewIndex < 0 || _currentPreviewIndex >= Results.Count)
        {
            PreviewImage.Source = null;
            return;
        }

        var bytes = Results[_currentPreviewIndex].Bytes;
        if (bytes is null)
        {
            PreviewImage.Source = null;
            return;
        }

        PreviewImage.Source = await LoadBitmapImageAsync(bytes);
        UpdatePreviewImageStretch();
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
            "Fantasy Art" => "D&D rulebook style — painted heroic fantasy, RPG handbook art",
            "Dark Fantasy" => "Dark medieval fantasy — muted tones, epic mood",
            "Pixel Art" => "16-bit RPG pixel art — clean hard pixels, limited palette",
            "3D Render" => "Pixar/Blender CGI — smooth natural studio lighting",
            "Futuristic" => "Clean sci-fi — chrome, blue-white light, sleek tech (not neon cyberpunk)",
            "Black & White" => "Sharp monochrome photo — full subject in scene, rich grayscale",
            "Vintage Photo" => "Yellowed film — grain, scratches, light leaks",
            "Concept Art" => "Film/game concept keyframe — cinematic digital painting, not comic",
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

        DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);
    }

    // Reshaping to the same total area as the native square (the previous approach) stretches the
    // long edge past SampleSize for every non-square aspect — e.g. a 768 base at 16:9 reshaped up
    // to 1024 wide — which reintroduces both the GPU-driver-watchdog risk and the anatomy/limb
    // duplication artifacts that the 768 baseline (see ModelCatalog — a TDR mitigation) exists to
    // avoid: observed in practice on landscape Oil Painting and Fantasy Art results. Capping the
    // long edge at SampleSize and scaling the short edge down from there keeps every aspect ratio
    // within the same tested-safe resolution as the square default, trading a smaller total image
    // area on non-square aspects for reliably correct anatomy.
    private static (int Width, int Height) ComputeDimensions(int sampleSize, string aspect)
    {
        var (aspectWidth, aspectHeight) = GetAspectRatio(aspect);

        double width, height;
        if (aspectWidth >= aspectHeight)
        {
            width = sampleSize;
            height = sampleSize * aspectHeight / aspectWidth;
        }
        else
        {
            height = sampleSize;
            width = sampleSize * aspectWidth / aspectHeight;
        }

        return (RoundToMultipleOf64(width), RoundToMultipleOf64(height));
    }

    // SDXL / OnnxStack require multiples of 64 on both axes (432-high 16:9 is invalid → use 448).
    private static int RoundToMultipleOf64(double value) => Math.Max(64, (int)Math.Round(value / 64.0) * 64);

    private static (double Width, double Height) GetAspectRatio(string aspect) => aspect switch
    {
        "Landscape (16:9)" => (16.0, 9.0),
        "Portrait (9:16)" => (9.0, 16.0),
        "Landscape (4:3)" => (4.0, 3.0),
        "Portrait (3:4)" => (3.0, 4.0),
        _ => (1.0, 1.0),
    };

    private int _aspectLayoutPass;

    // Deferred to the next dispatcher tick rather than run inline: mutating Width/Height
    // synchronously from inside SizeChanged re-enters the native layout pass and can crash
    // WinUI's layout engine outright (observed as an access violation in Microsoft.UI.Xaml.dll,
    // not a catchable .NET exception) instead of just misbehaving.
    private static string GetSelectedAspectLabel(ComboBox aspectCombo) =>
        ((ComboBoxItem?)aspectCombo.SelectedItem)?.Content?.ToString() ?? "Square";

    private string GetSelectedAspectLabel() => GetSelectedAspectLabel(AspectComboBox);

    private void AspectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _aspectLayoutPass = 0;
        var previewIndex = _currentPreviewIndex;
        DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            RefreshAspectDependentLayout();
            ApplyViewStretchToAllSlots();
            if (previewIndex >= 0 && previewIndex < Results.Count && Results[previewIndex].Bytes is not null)
            {
                _currentPreviewIndex = previewIndex;
                _ = RefreshPreviewImageAsync();
            }
        });
    }

    private void ApplyViewStretchToAllSlots()
    {
        var viewAspect = GetSelectedAspectLabel();
        foreach (var item in Results)
            item.UpdateViewStretch(viewAspect);
        UpdatePreviewImageStretch();
    }

    private void UpdatePreviewImageStretch()
    {
        if (_currentPreviewIndex < 0 || _currentPreviewIndex >= Results.Count)
        {
            PreviewImage.Stretch = Stretch.Uniform;
            return;
        }

        var item = Results[_currentPreviewIndex];
        var viewAspect = GetSelectedAspectLabel();
        PreviewImage.Stretch = ShouldLetterboxView(item, viewAspect)
            ? Stretch.Uniform
            : Stretch.UniformToFill;
    }

    // Letterbox only when we know the PNG was generated at a different aspect than the view dropdown.
    private static bool ShouldLetterboxView(ResultItem item, string viewAspect) =>
        item.GenerationAspectLabel is { } generated
        && !string.Equals(generated, viewAspect, StringComparison.Ordinal);

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);

    private void PreviewCardGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);

    private void ResultsCardBorder_SizeChanged(object sender, SizeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateResultSlotsAspect);

    private void RefreshAspectDependentLayout()
    {
        UpdatePreviewAspect();
        UpdateResultSlotsAspect();

        var hostsReady = PreviewImageHost.ActualWidth > 0 && PreviewImageHost.ActualHeight > 0
                         && ResultsCardBorder.ActualWidth > 0;
        if (!hostsReady && _aspectLayoutPass < 6)
        {
            _aspectLayoutPass++;
            DispatcherQueue.TryEnqueue(RefreshAspectDependentLayout);
        }
        else
        {
            _aspectLayoutPass = 0;
            if (_currentPreviewIndex >= 0 && Results[_currentPreviewIndex].Bytes is not null)
                _ = RefreshPreviewImageAsync();
        }
    }

    // Reshapes the Preview box to match the selected aspect ratio, clamped to the image host.
    private void UpdatePreviewAspect()
    {
        if (AspectComboBox?.SelectedItem is not ComboBoxItem selected) return;
        var (aspectWidth, aspectHeight) = GetAspectRatio(selected.Content?.ToString() ?? "Square");

        var previewHeight = ApplyAspectBox(PreviewBorder, PreviewImageHost.ActualWidth, aspectWidth, aspectHeight);
        UpdatePreviewPlaceholderTypography(previewHeight, aspectWidth, aspectHeight);
    }

    private void UpdatePreviewPlaceholderTypography(double previewHeight, double aspectWidth, double aspectHeight)
    {
        if (PreviewPlaceholderText is null || previewHeight <= 0) return;

        var previewWidth = PreviewBorder.ActualWidth;
        if (previewWidth <= 0 || double.IsNaN(previewWidth))
            previewWidth = previewHeight * aspectWidth / aspectHeight;

        PreviewPlaceholderText.MaxWidth = Math.Max(40, previewWidth - 20);
        var minDim = Math.Min(previewWidth, previewHeight);
        PreviewPlaceholderText.FontSize = Math.Clamp(minDim * 0.085, 9, 18);
    }

    private double GetPreviewHostMaxHeight(FrameworkElement box)
    {
        var fromHost = PreviewImageHost.ActualHeight - box.Margin.Top - box.Margin.Bottom;
        if (fromHost > 1) return fromHost;

        if (PreviewCardGrid.ActualHeight <= 0) return 0;
        const double chromeRows = 26 + 8 + 10 + 40 + 10;
        return Math.Max(0, PreviewCardGrid.ActualHeight - chromeRows);
    }

    // Fits within the host width and height, whichever is tighter (matches published — button row stays put).
    private double ApplyAspectBox(FrameworkElement box, double hostWidth, double aspectWidth, double aspectHeight)
    {
        var maxHeight = GetPreviewHostMaxHeight(box);
        if (hostWidth <= 0 || maxHeight <= 0) return 0;

        if (aspectHeight > aspectWidth)
        {
            var tallHeight = maxHeight;
            var tallWidth = tallHeight * aspectWidth / aspectHeight;
            if (tallWidth <= hostWidth + 0.5)
            {
                box.Width = tallWidth;
                box.Height = tallHeight;
                box.HorizontalAlignment = HorizontalAlignment.Center;
                return tallHeight;
            }
        }

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
        // Match published: thumbs follow aspect; style panel stays landscape 16:9 (balances portrait rows).
        var widthPerUnitHeight = Results.Count * aspectWidth / aspectHeight + 16.0 / 9.0;
        var slotHeightFromWidth = (availableWidth - totalSpacing) / widthPerUnitHeight;

        var slotHeight = Math.Min(preferredSlotHeight, slotHeightFromWidth);
        if (slotHeight <= 0) return;
        var slotWidth = slotHeight * aspectWidth / aspectHeight;

        panel.Spacing = 10;

        StyleDescriptionBorder.Height = slotHeight;
        StyleDescriptionBorder.Width = slotHeight * 16.0 / 9.0;
        StyleDescriptionText.FontSize = Math.Clamp(slotHeight * 0.11, 10, 16);
        StyleDescriptionText.MaxWidth = Math.Max(80, StyleDescriptionBorder.Width - 16);

        ResultItem.ApplySlotLayout(slotWidth, slotHeight);
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

    private static double _slotLayoutWidth = 96;
    private static double _slotLayoutHeight = 96;

    static ResultItem()
    {
        SlotLayoutChanged += static () =>
        {
            foreach (var listener in SlotLayoutListeners)
                listener.OnSlotLayoutChanged();
        };
    }

    private static readonly List<ResultItem> SlotLayoutListeners = [];

    private static event Action? SlotLayoutChanged;

    public static void ApplySlotLayout(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        var sizeChanged = Math.Abs(_slotLayoutWidth - width) >= 0.5 || Math.Abs(_slotLayoutHeight - height) >= 0.5;
        _slotLayoutWidth = width;
        _slotLayoutHeight = height;
        if (sizeChanged)
            SlotLayoutChanged?.Invoke();
        else
        {
            foreach (var listener in SlotLayoutListeners)
                listener.RefreshThumbnailRender();
        }
    }

    public ResultItem()
    {
        lock (SlotLayoutListeners)
            SlotLayoutListeners.Add(this);
    }

    public double SlotWidth => _slotLayoutWidth;
    public double SlotHeight => _slotLayoutHeight;

    private void OnSlotLayoutChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotHeight)));
        RefreshThumbnailRender();
    }

    private void RefreshThumbnailRender()
    {
        if (_thumbnail is null) return;
        var saved = _thumbnail;
        _thumbnail = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        _thumbnail = saved;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
    }

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

    /// <summary>Diffusion resolution (img2img source for New Variation).</summary>
    public byte[]? NativeGenerationBytes { get; set; }

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

    /// <summary>Aspect dropdown value when this PNG was generated.</summary>
    public string? GenerationAspectLabel { get; set; }

    private Stretch _slotImageStretch = Stretch.Uniform;
    public Stretch SlotImageStretch
    {
        get => _slotImageStretch;
        private set
        {
            if (_slotImageStretch == value) return;
            _slotImageStretch = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotImageStretch)));
        }
    }

    public void UpdateViewStretch(string viewAspectLabel)
    {
        var letterbox = GenerationAspectLabel is { } generated
                        && !string.Equals(generated, viewAspectLabel, StringComparison.Ordinal);
        SlotImageStretch = letterbox ? Stretch.Uniform : Stretch.UniformToFill;
        RefreshThumbnailRender();
    }

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

    private bool _generationFailed;
    public bool GenerationFailed
    {
        get => _generationFailed;
        set
        {
            if (_generationFailed == value) return;
            _generationFailed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GenerationFailed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotMark)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotMarkBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hint)));
        }
    }

    public string SlotMark => GenerationFailed ? "×" : "+";

    public Microsoft.UI.Xaml.Media.SolidColorBrush SlotMarkBrush =>
        new(GenerationFailed
            ? Windows.UI.Color.FromArgb(255, 220, 90, 90)
            : Windows.UI.Color.FromArgb(255, 106, 106, 106));

    public Microsoft.UI.Xaml.Media.SolidColorBrush ActiveBrush =>
        new(_isActive ? Microsoft.UI.Colors.MediumPurple : Microsoft.UI.Colors.Transparent);

    public string Hint => GenerationFailed
        ? "Blank GPU output — try 768 or lower load (fewer concurrent apps)"
        : Bytes is null
            ? "Nothing generated yet"
            : _isActive
                ? "Currently previewed"
                : "Click to preview";
}
