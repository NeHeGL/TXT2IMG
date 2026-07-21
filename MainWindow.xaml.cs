using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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

    // Purely which result slot's badge is highlighted as "currently the reference photo" — not
    // itself the source of truth for the base image (see _refImageBytes for that). -1 means no
    // result slot is highlighted.
    private int _selectedIndex = -1;

    // The img2img base for the next generate, whether it came from loading a file
    // (AddRefPhotoButton_Click) or selecting a result via its ↻ badge
    // (ResultSlot_ToggleBase_Click) — both now do the same thing: populate this and the
    // Reference Photo panel. Persists across generates until cleared, since it isn't wiped
    // along with the result slots on each new batch.
    private byte[]? _refImageBytes;

    public MainWindow()
    {
        this.InitializeComponent();
        ResultsPanel.ItemsSource = Results;
        // Tall enough that the Style dropdown's popup — capped at the window's own height, not
        // just MaxDropDownHeight, since it can't extend past the bottom of the window it lives
        // in — has room to show every style without needing to scroll.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 1100));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        // Shrinking below this made every box (and the results row with it) keep recomputing
        // to a smaller size to fit, so text and images kept shrinking the further down you
        // resized. Blocking resizes below the comfortable default instead means everything
        // always renders at that same fixed, legible size — maximizing still works, only
        // shrinking past the point things start looking cramped is disallowed.
        //if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        //{
        //    presenter.PreferredMinimumWidth = 1200;
        //    presenter.PreferredMinimumHeight = 900;
        //}

        UpdateStyleDescription();
    }

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

        // Changing style/aspect/model/ref photo/creativity mid-batch would apply to nothing —
        // the in-flight batch already captured its own settings — while looking like it should
        // affect what's currently generating. Locking everything until the batch finishes avoids
        // that confusion.
        SetControlsEnabled(false);

        try
        {
            var style = ((ComboBoxItem)StyleComboBox.SelectedItem).Content?.ToString() ?? "Cartoon";
            var (fullPrompt, extraNegative, guidanceMultiplier) = BuildPrompt(prompt, style);

            var modelChoice = ((ComboBoxItem)ModelComboBox.SelectedItem).Content?.ToString() ?? "";
            var model = modelChoice.StartsWith("SDXL Base") ? ModelCatalog.SdxlBase : ModelCatalog.DreamShaperXLLightning;
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

            var statusCallback = new Progress<string>(status => GenerateButton.Content = status);
            var progressFillCallback = new Progress<double>(SetGenerateButtonFill);

            // Selecting a result via the ↻ badge (ResultSlot_ToggleBase_Click) copies its bytes
            // into _refImageBytes immediately, so it's already the single source of truth here
            // regardless of whether the base came from a loaded file or a selected result — no
            // need to separately fall back to Results[_selectedIndex].Bytes, which the slot-clearing
            // loop below is about to wipe out anyway.
            byte[]? baseImageBytes = _refImageBytes;

            // Every generate produces a brand new set of results, replacing whatever was shown
            // before; each image is dropped into its slot as soon as it's ready instead of
            // waiting for the whole batch, so the row fills in live.
            for (var i = 0; i < Results.Count; i++)
            {
                Results[i].IsActive = false;
                Results[i].Bytes = null;
                Results[i].Thumbnail = null;
            }
            _selectedIndex = -1;
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

            await _imageGenerator.GenerateImagesAsync(model, fullPrompt, baseImageBytes, statusCallback, imageCallback, extraNegative, widthOverride, heightOverride, progressFillCallback, (float)CreativitySlider.Value, guidanceScaleOverride, inferenceStepsOverride);
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
        ModelComboBox.IsEnabled = enabled;
        AddRefPhotoButton.IsEnabled = enabled;
        // Clear Reference Photo's enabled state normally tracks whether one is actually loaded
        // (see AddRefPhotoButton_Click / ClearRefPhoto) — re-enabling it unconditionally here
        // would let you "clear" a reference photo that isn't there.
        ClearRefPhotoButton.IsEnabled = enabled && _refImageBytes is not null;
        CreativitySlider.IsEnabled = enabled;
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

        await FileIO.WriteBytesAsync(file, CurrentImageBytes);
    }

    private void ResultSlot_Click(object sender, RoutedEventArgs e)
    {
        // Preview only — this must never change _selectedIndex. Conflating "look at this"
        // with "build on this" was the bug behind every image inheriting the same shape:
        // a plain click to preview one result silently became the base for the next prompt.
        if (sender is not Button button || button.Tag is not ResultItem item) return;
        if (item.Bytes is null) return;

        var index = Results.IndexOf(item);
        if (index < 0) return;
        ShowPreview(index);
    }

    private async void ResultSlot_ToggleBase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ResultItem item) return;
        if (item.Bytes is null) return;

        var index = Results.IndexOf(item);
        if (index < 0) return;

        if (_selectedIndex == index)
        {
            // Tapping the already-selected badge again deselects it: the next
            // generate goes back to a fresh text2img batch instead of building on it.
            item.IsActive = false;
            _selectedIndex = -1;
            ClearRefPhoto();
        }
        else
        {
            if (_selectedIndex >= 0) Results[_selectedIndex].IsActive = false;
            item.IsActive = true;
            _selectedIndex = index;

            // Selecting a result now behaves exactly like loading it as a reference photo —
            // the Reference Photo panel updates to show it — rather than silently tracking an
            // index with no visible confirmation anywhere except the badge's own border.
            _refImageBytes = item.Bytes;
            RefPhotoThumbnail.Source = await LoadBitmapImageAsync(item.Bytes);
            ClearRefPhotoButton.IsEnabled = true;
        }
    }

    private async void AddRefPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var buffer = await FileIO.ReadBufferAsync(file);
        _refImageBytes = await Task.Run(() => NormalizeOrientation(buffer.ToArray()));
        RefPhotoThumbnail.Source = await LoadBitmapImageAsync(_refImageBytes);
        ClearRefPhotoButton.IsEnabled = true;

        // A result slot and a loaded ref photo are alternate ways to set the img2img
        // base; loading one here supersedes the other. This only affects what the next
        // generate builds on — the Preview panel keeps showing whatever result was last
        // clicked, independent of the ref photo shown in its own panel.
        if (_selectedIndex >= 0)
        {
            Results[_selectedIndex].IsActive = false;
            _selectedIndex = -1;
        }
    }

    private void ClearRefPhotoButton_Click(object sender, RoutedEventArgs e) => ClearRefPhoto();

    private void ClearRefPhoto()
    {
        _refImageBytes = null;
        RefPhotoThumbnail.Source = null;
        ClearRefPhotoButton.IsEnabled = false;
    }

    // Phone/camera photos are commonly stored in the sensor's native orientation with an EXIF
    // Orientation tag telling viewers to rotate them — WinUI's BitmapImage honors that tag when
    // displaying the thumbnail, but OnnxStack's image loader does not, so the model would
    // otherwise generate from the unrotated sensor orientation while the preview looks upright.
    // Baking the rotation into the pixels once here keeps what's displayed and what the model
    // sees in agreement.
    private static byte[] NormalizeOrientation(byte[] imageBytes)
    {
        // Fully qualified: Microsoft.UI.Xaml.Controls.Image (the XAML control, already in scope
        // via the using at the top of this file) would otherwise shadow ImageSharp's Image type.
        using var image = SixLabors.ImageSharp.Image.Load(imageBytes);
        image.Mutate(ctx => ctx.AutoOrient());
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private void ShowPreview(int index)
    {
        var item = Results[index];
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

    // Flat/vector styles (Cartoon, Icon, Cel-Shaded) ask the model to suppress shading/lighting
    // entirely — a much bigger jump away from its photographic training default than a painterly
    // style like Watercolor/Oil Painting, which keeps that shading. Negative-prompt wording alone
    // couldn't fix this (tried and reverted); a guidance-scale bump for just these styles forces
    // stronger adherence to the style/negative prompt without affecting the styles that already
    // work reliably.
    private const float FlatStyleGuidanceMultiplier = 1.6f;

    private static (string Prompt, string ExtraNegative, float GuidanceScaleMultiplier) BuildPrompt(string prompt, string style)
    {
        // The subject goes first: earlier tokens carry more weight in the text encoder, so
        // leading with style words buries the actual subject. Photorealistic also steers the
        // negative prompt away from the model's illustrative default — SDXL (and DreamShaper's
        // fine-tuning especially) leans painterly/stylized unless told otherwise, so "photo" in
        // the prompt alone doesn't reliably win on its own.
        var (styleSuffix, extraNegative, guidanceMultiplier) = style switch
        {
            "Icon" => ("flat vector icon, single bold silhouette shape, minimal geometric design, solid limited color palette, logo-like simplicity", "photo, photorealistic, realistic, 3d render, watercolor, oil painting, detailed illustration, gradient shading, cartoon character", FlatStyleGuidanceMultiplier),
            "Comic Book" => ("comic book illustration, bold ink outlines, halftone dot shading, dynamic pop art coloring, comic panel art style", "photo, photorealistic, realistic, 3d render, watercolor, oil painting", FlatStyleGuidanceMultiplier),
            // Cel-shading is really a rendering technique (flat, hard-edged toon shading), not
            // exclusive to anime — Western cartoons and games (Borderlands, Wind Waker) use it
            // too — so this is kept targeted at that non-anime "toon-shaded game/cartoon" look,
            // with each style's negative prompt pushing away from the other.
            "Cel-Shaded" => ("cel-shaded toon render, bold black outlines tracing distinct colored shapes, flat cel shading with clearly visible colored regions, saturated flat colors, stylized toon-shaded video game or cartoon look", "photo, realistic, painterly, 3d render, watercolor, oil painting, anime, manga, japanese animation, black silhouette, solid black shape, underexposed", FlatStyleGuidanceMultiplier),
            "Anime" => ("anime illustration, manga character design, expressive anime eyes, detailed manga line art, vibrant colors, Japanese animation studio aesthetic", "photo, photorealistic, realistic, 3d render, watercolor, oil painting, cel-shaded, flat toon shading", FlatStyleGuidanceMultiplier),
            "Watercolor" => ("watercolor painting, soft flowing pigment, visible paper texture, gentle color bleed, delicate brush strokes, fine art", "3d render, photo, vector art, flat colors, cartoon", 1f),
            "Oil Painting" => ("oil painting, rich impasto brush strokes, canvas texture, classical fine art style, painterly, dramatic lighting", "3d render, photo, vector art, flat colors, cartoon, smooth digital art", 1f),
            // A physical/handcrafted medium like Watercolor or Oil Painting, not a computer
            // render — grouped with them rather than with 3D Render below.
            "Claymation" => ("claymation, stop-motion clay animation style, sculpted clay figures, visible fingerprints and tool marks, handcrafted texture", "photo, photorealistic, realistic, flat colors, vector art, watercolor, oil painting, 2d illustration", FlatStyleGuidanceMultiplier),
            "Fantasy Art" => ("fantasy digital painting, concept art, dramatic cinematic lighting, intricate detail, epic fantasy illustration", "3d render, photo, vector art, flat colors, cartoon, anime", 1f),
            "Pixel Art" => ("pixel art, retro 8-bit video game sprite, blocky pixelated style, limited color palette", "blurry, smooth gradients, anti-aliased, photo, photorealistic, realistic, 3d render, watercolor, oil painting", FlatStyleGuidanceMultiplier),
            "3D Render" => ("3d render, Pixar-style character render, smooth CG shading, studio lighting, dimensional depth", "flat colors, vector art, 2d illustration, watercolor, oil painting, sketch", 1f),
            // Vintage Photo's negative already excludes "digital, modern" to stay away from this
            // style; this excludes "vintage, sepia, film grain" in return.
            "Photorealistic" => ("photorealistic, DSLR photograph, natural lighting, sharp focus, highly detailed skin and textures", "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, vintage, sepia, film grain", 1f),
            "Vintage Photo" => ("vintage photograph, retro film grain, faded sepia tones, old-fashioned analog photo aesthetic, nostalgic lighting", "cartoon, clipart, illustration, drawing, painting, anime, flat colors, vector art, digital, modern", 1f),
            _ => ("cartoon character illustration, bold clean outlines, bright cheerful colors, playful exaggerated proportions", "photo, photorealistic, realistic, 3d render, flat vector icon", FlatStyleGuidanceMultiplier), // Cartoon
        };

        return ($"{prompt}, {styleSuffix}", extraNegative, guidanceMultiplier);
    }

    private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateStyleDescription();

    // Known WinUI3 bug (unpackaged desktop apps in particular): the window is misreported as
    // "inactive," which breaks the automatic mouse-wheel-to-ScrollViewer routing a ComboBox
    // dropdown normally relies on — the scrollbar shows and can be dragged, but the wheel does
    // nothing (see microsoft-ui-xaml#8764/#8161/#10480; fixed once in WinAppSDK 1.4, regressed
    // again in 1.6.1, not called out as fixed as of the 2.2.0 this project references). Rather
    // than depend on an upstream fix, the dropdown's ScrollViewer is found once it opens and
    // wheel input is applied to it directly via ChangeView, bypassing whatever automatic routing
    // is broken.
    private void ScrollableComboBox_DropDownOpened(object sender, object e)
    {
        var xamlRoot = ((FrameworkElement)sender).XamlRoot;
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            if (FindDescendant<ScrollViewer>(popup.Child) is not { } scrollViewer)
                continue;

            // DropDownOpened can fire again for the same still-open popup; avoid stacking
            // duplicate handlers that would multiply the scroll amount per wheel tick.
            scrollViewer.PointerWheelChanged -= DropdownScrollViewer_PointerWheelChanged;
            scrollViewer.PointerWheelChanged += DropdownScrollViewer_PointerWheelChanged;

            // ComboBox's default behavior scrolls the popup so the *currently selected* item
            // lines up with the closed box, rather than always opening from the top of the list —
            // fine for a handful of items, but disorienting for a style list this long where the
            // selection could be scrolled anywhere. Forcing it back to the top on every open makes
            // it behave like the shorter Aspect/Model dropdowns, which never scroll to begin with.
            scrollViewer.ChangeView(null, 0, null, true);
            break;
        }
    }

    private void DropdownScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;
        var delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        scrollViewer.ChangeView(null, scrollViewer.VerticalOffset - delta, null);
        e.Handled = true;
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

    private void UpdateStyleDescription()
    {
        // StyleComboBox's declarative SelectedIndex="0" fires SelectionChanged during
        // InitializeComponent, before StyleDescriptionText — declared later in the visual tree —
        // has been constructed. Without this guard that's a null reference inside XAML's own
        // construction sequence, which doesn't surface as a catchable .NET exception; it crashes
        // the native XAML engine outright (observed as an access violation in
        // Microsoft.UI.Xaml.dll).
        if (StyleDescriptionText is null) return;

        var style = (StyleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cartoon";
        StyleDescriptionText.Text = style switch
        {
            "Icon" => "Flat vector logo/icon — single bold shape, minimal palette",
            "Comic Book" => "Bold ink outlines, halftone shading, pop art comic style",
            "Cel-Shaded" => "Toon-shaded game/cartoon look — bold outlines, flat hard-edged shading",
            "Anime" => "Manga character design, expressive eyes, Japanese animation look",
            "Watercolor" => "Soft, painterly, visible paper texture",
            "Oil Painting" => "Rich brush strokes, canvas texture, classical fine art",
            "Claymation" => "Stop-motion clay animation, handcrafted sculpted texture",
            "Fantasy Art" => "Painterly concept art, dramatic lighting, epic detail",
            "Pixel Art" => "Blocky 8-bit game sprite, retro pixelated look",
            "3D Render" => "Pixar-style CG character render, studio lighting",
            "Photorealistic" => "Photo-like — DSLR look, sharp focus, detailed skin/textures",
            "Vintage Photo" => "Retro film photo — faded tones, grain, nostalgic",
            _ => "Cartoon character — bold outlines, bright cheerful colors", // Cartoon
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

    // Reshapes the Ref Photo / Preview boxes to match the selected aspect ratio, clamped to
    // whatever vertical space the (now fixed-share, see MainWindow.xaml) Preview row actually
    // has, so the box never overflows the window.
    private void UpdatePreviewAspect()
    {
        if (AspectComboBox?.SelectedItem is not ComboBoxItem selected) return;
        var (aspectWidth, aspectHeight) = GetAspectRatio(selected.Content?.ToString() ?? "Square");

        var refPhotoHeight = ApplyAspectBox(RefPhotoBorder, RefPhotoColumn.ActualWidth, aspectWidth, aspectHeight);
        var previewHeight = ApplyAspectBox(PreviewBorder, PreviewImageColumn.ActualWidth, aspectWidth, aspectHeight);

        // Otherwise fixed at whatever looked right for one box size — barely visible once the
        // window's maximized, oversized once it's small. Scaling off the same measured height as
        // the box itself keeps it proportionate at any window size (same idea as the style
        // description text in UpdateResultSlotsAspect).
        if (refPhotoHeight > 0) RefPhotoPlaceholderText.FontSize = Math.Clamp(refPhotoHeight * 0.065, 14, 26);
        if (previewHeight > 0) PreviewPlaceholderText.FontSize = Math.Clamp(previewHeight * 0.065, 14, 26);
    }

    // Fits within the column width and the available row height, whichever is the tighter
    // constraint, so the box never overflows the window; the unconstrained dimension is then
    // centered within its column.
    private double ApplyAspectBox(FrameworkElement box, double columnWidth, double aspectWidth, double aspectHeight)
    {
        // The row's ActualHeight is the space for the whole row, margin included — the box's
        // own top/bottom margin (14px top, in XAML) has to come out of that budget too, or the
        // box ends up exactly margin-sized-taller than what's actually left, overflowing the
        // card by that much every time.
        var maxHeight = ImageRowDefinition.ActualHeight - box.Margin.Top - box.Margin.Bottom;
        if (columnWidth <= 0 || maxHeight <= 0) return 0;

        var height = columnWidth * aspectHeight / aspectWidth;
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

    // Sized from the Results card's own fixed-share row (see MainWindow.xaml), independently of
    // Preview's box — NOT as a fraction of Preview's computed height. That was the original
    // design, and it created a feedback loop: Results' height depended on Preview's height, but
    // Preview's available height was in turn whatever the (then content-sized) Results row left
    // behind, so resizing one retriggered a re-layout of the other and the two settled on a far
    // too small value. ItemsControl doesn't expose its generated containers directly; each one is
    // a ContentPresenter wrapping the DataTemplate's root Grid, reached here via VisualTreeHelper
    // since this WinAppSDK version has no ContentTemplateRoot.
    private void UpdateResultSlotsAspect()
    {
        if (AspectComboBox?.SelectedItem is not ComboBoxItem selected) return;
        if (ResultsPanel.ItemsPanelRoot is not StackPanel panel) return;
        var (aspectWidth, aspectHeight) = GetAspectRatio(selected.Content?.ToString() ?? "Square");

        // Caption text + its row + the ItemsControl's top margin + the card's own top/bottom
        // padding — everything in the card besides the slots themselves.
        const double chromeHeight = 66;
        var slotHeightFromRowHeight = ResultsCardBorder.ActualHeight - chromeHeight;
        if (slotHeightFromRowHeight <= 0) return;

        // The height-only calculation above never checked whether Results.Count slots at that
        // height — plus the spacer column and the (always-16:9) description box sharing the same
        // row — actually fit the card's width. For wide aspect ratios (Landscape) this let every
        // slot grow wider than the window had room for, overflowing the row instead of shrinking
        // to fit; the description box and credit line, left with whatever the "*" column had
        // left over, were what visibly vanished off the right edge. Solving for the height that
        // makes slots + spacer + description box add up to exactly the card's available width
        // gives a second candidate height; taking whichever of the two is smaller guarantees the
        // row fits both dimensions at once, the same "tighter constraint wins" idea as
        // ApplyAspectBox above.
        // A few px of slack, not an exact fit: solving for the precise width that fills the row
        // with zero slack left the description box (right-aligned in its column) getting its left
        // edge — where its accent border lives — clipped off whenever DPI/sub-pixel rounding
        // pushed the real layout a hair past the theoretical exact value.
        const double widthSafetyMargin = 6;
        var availableWidth = ResultsCardBorder.ActualWidth - ResultsCardBorder.Padding.Left - ResultsCardBorder.Padding.Right - ResultsSpacerColumn.ActualWidth - widthSafetyMargin;
        var totalSpacing = panel.Spacing * (Results.Count - 1);
        var widthPerUnitHeight = Results.Count * aspectWidth / aspectHeight + 16.0 / 9.0;
        var slotHeightFromRowWidth = (availableWidth - totalSpacing) / widthPerUnitHeight;

        var slotHeight = Math.Min(slotHeightFromRowHeight, slotHeightFromRowWidth);
        if (slotHeight <= 0) return;
        var slotWidth = slotHeight * aspectWidth / aspectHeight;

        // The description box is always 16:9 regardless of the Aspect dropdown — this formula
        // already doesn't depend on aspectWidth/aspectHeight, so it stays fixed across aspect
        // changes on its own. It's driven by the same measured budget as the slots (not a
        // hardcoded guess) so it can't overflow the row the way a fixed guess did in Portrait.
        StyleDescriptionBorder.Height = slotHeight;
        StyleDescriptionBorder.Width = slotHeight * 16.0 / 9.0;

        // Otherwise fixed at whatever looked right for one box size — barely filling the box
        // once the window's maximized, and cramped once it's small. Scaling off the same
        // measured height as the box itself keeps it proportionate at any window size.
        StyleDescriptionText.FontSize = Math.Clamp(slotHeight * 0.11, 12, 22);

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

    public string Hint => _isActive
        ? "Using as reference photo — tap again to clear"
        : Bytes is null ? "Nothing generated yet" : "Use as reference photo";
}
