# TXT2IMG

**2026 Jeff Molofee (NeHe)**

A Windows desktop app (WinUI 3) that generates images from a text prompt entirely on your own PC, then copies the result straight to your clipboard. No account, no API keys, no cloud calls for generation — everything runs locally on your GPU. The only time the app touches the network is the one-time model download on first run; after that your prompts and images never leave the machine.

---

<p align="center"><img src="app_image.jpg" width="500" alt="Example TXT2IMG output"></p>

---

## Download

Grab the latest build from the [Releases page](https://github.com/NeHeGL/TXT2IMG/releases) — download `TXT2IMG-vX.Y.Z-windows-x64.zip`, extract anywhere, and run `TXT2IMG.exe`.

If you don't already have it (most Windows 11 PCs do, from other WinUI apps), install the [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) first — a small, one-time Microsoft redistributable, same idea as a VC++ Redistributable.

---

## Features

- **Local generation** — prompts and images stay on your PC; the only network use is the one-time model download on first run
- **DreamShaper XL Lightning only** — one fast, high-quality model (see [Models](#models) for why SDXL Base was dropped)
- **22 hand-tuned style presets** — alphabetized in the dropdown, each with a live description of what it produces and negative-prompt terms that push it away from its nearest neighbor (see [Styles](#styles)): None, 3D Render, Anime, Black & White, Cartoon, Claymation, Colorful Vector, Comic Book, Concept Art, Cyberpunk, Dark Fantasy, Fantasy Art, Futuristic, Graphic Oil, Icon, Minimalist, New Impasto, Oil Painting, Pixel Art, Soft Oil, Vintage Photo, Watercolor
- **Aspect ratio picker** — Square, Landscape/Portrait 16:9, Landscape/Portrait 4:3 (defaults to Landscape 16:9); correctly scaled rather than stretching a square canvas
- **Max Detail** — optional checkbox that doubles inference steps for cleaner anatomy and detail (slower; does not raise resolution)
- **Generates 5 variations per prompt** so you can pick the best result in one pass
- **Copy to clipboard or save as PNG** — save suggests a filename that includes the panel number and style, e.g. `fairy-with-wings-2-icon.png`

---

## Usage

1. Type a prompt and press Enter (or click **Generate**)
2. Pick a **Style** and **Aspect Ratio** — the panel next to the results shows a description of the selected style
3. Optionally enable **Max Detail** when you want more denoising steps
4. Click any of the 5 results to preview it
5. **Copy to Clipboard** or **Save as PNG**

---

## Styles

The 22 presets are the heart of this app, and getting them right took the bulk of the development time. A style is not a single keyword bolted onto your prompt — each one is a carefully assembled recipe of positive terms, a tailored **negative** prompt, and per-style tuning (guidance scale, inference steps, and in some cases medium locks and grayscale enforcement). They were built and refined over many rounds of side-by-side generation, comparing the same subject across styles, watching for the specific ways DreamShaper Lightning drifts, and pushing each look back into its own lane.

Most of that work was **differentiation**. Left alone, a small distilled model happily collapses neighboring styles into the same image: Oil Painting slides into Graphic Oil's palette-knife slabs, Cartoon and Anime and Comic Book blur together, flat/vector styles quietly render as photos, and half the palette drifts toward the same "red-and-black poster" look. Every style therefore carries negative-prompt terms aimed squarely at its closest neighbor, so the lineup stays visually distinct:

- **Painterly & traditional media** — **Oil Painting** (visible brushwork and canvas texture), **Soft Oil** (its gentler, more blended sibling), **Graphic Oil** (hard-edged palette-knife slabs), **New Impasto** (thick tube-squeezed paint ridges), **Watercolor**, and **Claymation** for sculpted stop-motion clay.
- **Illustration & comics** — **Cartoon**, **Anime**, **Comic Book** (Ben-Day dots and inked panels), **Concept Art**, **Fantasy Art**, **Dark Fantasy** (dark medieval tone), and **Minimalist**.
- **Graphic & digital** — **Colorful Vector** (clean flat vector art), **Icon**, **Pixel Art** (locked to genuinely pixelated output, faces included), **3D Render**, **Cyberpunk** (neon dystopia), and **Futuristic** (clean sci-fi chrome, deliberately split from Cyberpunk so the two don't converge).
- **Photographic** — **Black & White** (grayscale enforced on the pixels, since the model leaks color from prompts alone) and **Vintage Photo**.
- **None** — no style suffix at all, letting the model interpret your prompt on its own.

Many of the newer recipes started from published prompt guides and were then adapted for DreamShaper XL Lightning (no LoRAs, retuned negatives, adjusted guidance). Full attribution is in [Credits](#credits) — thank you to InsiderLLM, Stable Diffusion Art, and grafmix421's Impasto Virtuoso for the starting points.

---

## Models

Runs Stable Diffusion XL locally via [OnnxStack](https://github.com/saluteai/OnnxStack) and ONNX Runtime with DirectML, using **DreamShaper XL Lightning** — distilled for low-step inference — downloaded on first use into `%LocalAppData%\TXT2IMG\Models\`.

**SDXL Base was removed** from the app to keep the download and on-disk footprint smaller. DreamShaper is also the better default for this UI: faster, generally cleaner anatomy, and stronger styled results than the stock SDXL checkpoint was giving us.

---

## Design notes

- **The side-by-side reference panel was removed in favor of a single, larger Preview.** The original layout paired the output with a reference-image panel for img2img editing — load a photo, then restyle it to claymation/pixel/watercolor, change eye color, age a face, and so on. That path never worked reliably with DreamShaper Lightning: aging a face barely worked, and most other edits either drifted off the source or failed outright. Rather than ship a half-broken feature, the reference panel, the creativity slider, and the ↻ "use as reference" badge were all removed. Generation is now text + style only, and dropping the reference column freed the whole width for one centered Preview that shows the selected result much larger. The seed readout and the experimental "keep this + 4 new" variation button were later removed for the same reason — they added UI weight without pulling their weight.
- **Max Detail** only increases inference steps (not resolution). Higher res at 1024 was previously linked to GPU TDR hangs on some machines; the working baseline stays at 768.

---

## What's new since v1.0.0

- **Big style expansion** — the lineup grew to 22 presets, adding **Colorful Vector**, **Cyberpunk**, **Futuristic**, **Graphic Oil**, **Soft Oil**, **Concept Art**, **Minimalist**, and **New Impasto**, alongside heavily retuned recipes for Anime, Oil Painting, Pixel Art, Watercolor, Comic Book, and Fantasy Art. Several recipes were adapted from published prompt guides (see [Credits](#credits)). See [Styles](#styles) for the full breakdown.
- **Styles alphabetized** in the dropdown, with the selected item no longer given a competing highlight fill (the mouse hover shows what a click will pick).
- **Black & White** (formerly Hires B&W) now enforces grayscale on the output pixels, since the model leaks color from prompts and lighting alone.
- **DreamShaper-only** — SDXL Base / model picker removed to cut size; DreamShaper is the sole engine.
- **Reference photo / img2img removed** — see [Design notes](#design-notes).
- **Seed readout and "keep this + 4 new" variation button removed** — trimmed to keep the UI focused; generation still batches 5 varied results per prompt.
- **Max Detail checkbox** — optional 2× step count for quality.
- **Layout** — single centered Preview (no side-by-side reference panel); defaults to Landscape 16:9; window height tightened so the Preview and action buttons sit without dead space.
- **Cel-Shaded removed** — too much overlap with Cartoon/Anime and inconsistent results (abstract stripes vs full scenery).
- **Style uniqueness pass** — every style given negative-prompt terms aimed at its closest neighbor so the lineup stays visually distinct (Oil vs Graphic Oil, Cartoon vs Anime vs Comic, flat/vector vs photo), plus an anti "red-and-black poster" collapse across the board.
- **Style lineup overhauled** — Illustration, Realistic, and Ultra High-Res were removed (each either overlapped another style too closely or couldn't reliably hit its intended look), replaced and expanded as above.
- **Fixed occasional GPU hang (`DXGI_ERROR_DEVICE_HUNG`) mid-batch** — a denoising/VAE step at native 1024 resolution could trip Windows' driver watchdog (TDR) on some GPUs. The model's baseline resolution was lowered to 768.
- **Fixed anatomy artifacts (fused/extra limbs, malformed hands/feet/poses)** — broadened the negative prompt to cover fused limbs and foot/toe deformities, and raised the fast model's inference steps (8 → 12, with a further 50% boost on non-square aspect ratios) to give it more denoising budget to resolve complex poses. This reduces but doesn't fully eliminate the issue on very complex or ambiguous poses — regenerating remains the reliable fallback.
- **Fixed non-square aspect ratios (Landscape/Portrait) reintroducing anatomy/GPU-hang risk** — dimensions are now computed by capping the long edge at the model's native resolution rather than stretching to match the square default's total pixel area.
- **Fixed flat/vector styles (Cartoon, Icon) occasionally rendering photorealistic instead of the requested style** — a guidance-scale increase scoped to those styles forces stronger adherence to the requested look.
- **The Style dropdown scrolls with the mouse wheel** — one style per notch, and the scrollbar can still be dragged as usual. This works around a known WinUI3 bug ([microsoft-ui-xaml#8764](https://github.com/microsoft/microsoft-ui-xaml/issues/8764)) where the framework reports its windows as "inactive," breaking the automatic wheel-to-scroll routing a dropdown normally relies on; wheel input is now applied to the list directly.
- **Fixed result thumbnails and the style description box overflowing off the right edge on Landscape/Portrait aspect ratios** — result-slot sizing now accounts for available row width, not just height.

---

## Requirements

- Windows 10/11 x64 with a DirectX 12 compatible GPU (DirectML)
- [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) 1.7+ (one-time install, see Download above)

---

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) with the Windows App SDK workload.

**To produce a distributable `.exe`** (the same self-contained build used for GitHub Releases), run **`build.bat`** from the project root — it publishes a Release, self-contained `win-x64` build to `publish\TXT2IMG.exe`.

**For development** (debugging in Visual Studio), open `TXT2IMG.csproj`, or from a terminal:

```bat
dotnet build TXT2IMG.csproj
```

It targets `net8.0-windows10.0.19041.0` with WinUI 3.

---

## Project layout

- [App.xaml](App.xaml) / [App.xaml.cs](App.xaml.cs) — application entry point
- [MainWindow.xaml](MainWindow.xaml) / [MainWindow.xaml.cs](MainWindow.xaml.cs) — main UI and generation flow
- [LocalImageGenerator.cs](LocalImageGenerator.cs) — OnnxStack pipeline wrapper
- [ModelDownloader.cs](ModelDownloader.cs) — model catalog and on-demand download
- [ExecutionProviders.cs](ExecutionProviders.cs) — DirectML/CPU execution provider setup

---

## Author

2026 Jeff Molofee (NeHe)

---

## Credits

The checkpoint and runtime are covered under [Models](#models). Several of the style presets began as prompt recipes shared by others and were then adapted for DreamShaper XL Lightning (retuned positives/negatives, adjusted guidance and steps, no LoRAs). Sincere thanks to:

- [InsiderLLM — AI Art Styles & Workflows](https://insiderllm.com/guides/ai-art-styles-workflows-guide/) — starting recipes behind **Anime**, **Oil Painting**, **Concept Art**, **Pixel Art**, **Watercolor**, **Comic Book**, **Fantasy Art**, and **Minimalist**.
- [Stable Diffusion Art — SDXL prompts](https://stable-diffusion-art.com/sdxl-prompts/) — the basis for **Colorful Vector** (effect #11 "Fish," made subject-agnostic) and **Cyberpunk**.
- [Impasto Virtuoso on Civitai](https://civitai.com/models/463852/impasto-virtuoso) by **grafmix421** — technique language behind **New Impasto**. The LoRA itself is not loaded (the OnnxStack path doesn't support it); only the descriptive prompt vocabulary was adapted.

All recipes were reworked to hit their intended look on this model without LoRAs, and every style was tuned against its neighbors so the lineup stays distinct.
