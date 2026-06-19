# QR Code Generator Utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

## Purpose / Big Picture

After this change, users will have a **QR Code Generator** utility in the AiteBar panel. When clicked, it opens a dark-themed window matching the visual style of IconConverter/TimerStopwatch/QuickNote. The user types or pastes any text (URL, plain text, WiFi credentials, contact info, etc.) and instantly sees a live QR code preview. The user can then:

1. **Copy PNG to clipboard** — paste the QR code image into any app (Telegram, Word, Paint, email, etc.)
2. **Save as PNG** — raster image file, suitable for printing or embedding in documents
3. **Save as SVG** — vector image file, scalable to any size without quality loss, suitable for web/print

### User scenarios

**Scenario 1 — Share a URL quickly**: User copies a URL, opens QR Generator, pastes the URL, clicks "Copy PNG", pastes into a chat/messenger. Total time: ~5 seconds.

**Scenario 2 — Generate a QR for a WiFi password**: User types `WIFI:S:MyNetwork;T:WPA;P:secret123;;`, sees the QR code preview, clicks "Save PNG" to save it for printing.

**Scenario 3 — Create a scalable QR for a poster**: User types the URL, clicks "Save SVG", gets a vector file that can be scaled to any size for print.

**Scenario 4 — Quick access via hotkey**: User presses the configured hotkey, the QR Generator window opens immediately, types text, gets QR code.

This follows the existing utility pattern established by IconConverter, ColorPicker, TimerStopwatch, and QuickNote: a `[Utility]`-decorated class, a `DarkWindow`-based XAML window, a service class for logic, localization resources, settings toggle, and panel button definition.

## Progress

- [x] (2026-06-19) Plan written and used as the implementation source of truth.
- [x] (2026-06-19) Add `QRCoder` and `QRCoder.Xaml` NuGet packages to `AiteBar.csproj`; package lock files updated.
- [x] (2026-06-19) Create `QRCodeModels.cs` — options, result models.
- [x] (2026-06-19) Create `QRCodeService.cs` — generation logic for PNG and SVG plus shared QR data creation for XAML preview.
- [x] (2026-06-19) Create `QRCodeGeneratorWindow.xaml` + `.xaml.cs` — dark utility window with debounced live preview, PNG clipboard copy, PNG save, and SVG save.
- [x] (2026-06-19) Create `QRCodeGeneratorUtility.cs` — `UtilityBase<QRCodeGeneratorWindow>` registration.
- [x] (2026-06-19) Register in `UnifiedButtonService.cs` — panel button definition.
- [x] (2026-06-19) Register in `MainWindow.xaml.cs` — panel click dispatch and hotkey dispatch.
- [x] (2026-06-19) Add `AppSettings.ShowPresetQRCodeGenerator` — visibility toggle.
- [x] (2026-06-19) Add localization keys to all `.resx` files (en, ru, uk, de).
- [x] (2026-06-19) Add `HotkeyCommand.QRCodeGenerator` and hotkey binding.
- [x] (2026-06-19) Add settings checkbox and hotkey row in `AppSettingsWindow`.
- [x] (2026-06-19) Write unit tests for `QRCodeService` and update hotkey tests for the new command.
- [x] (2026-06-19) Build Release successfully using an escalated build after sandboxed WPF temp-file writes failed.
- [x] (2026-06-19) Run focused verification: `QRCodeServiceTests` passed 14/14 and `HotkeyServiceTests` passed 11/11 through `dotnet vstest` filters.
- [x] (2026-06-19) Fix full-suite localization failure by moving QR ECC combo item text into `.resx` resources.
- [x] (2026-06-19) Run full `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`; 503/503 tests passed.
- [x] (2026-06-19) Run `dotnet build .\AiteBar.sln -c Release`; build passed with 0 warnings and 0 errors.
- [x] (2026-06-19) Add accessibility names/help text to panel buttons so QR and other unified buttons are discoverable through UI Automation by their localized tooltip text.
- [x] (2026-06-19) Fix QR window layout issues found in manual review: pixel-size label/value no longer collide, status text has a dedicated row, and the three command buttons are aligned in a stable three-column footer.
- [x] (2026-06-19) Run isolated QR window UI smoke without touching the user's working `settings.json`: live preview became visible, empty hint collapsed, status reported `QR версия 6, 41x41 модулей`, and Copy/Save PNG/Save SVG buttons were enabled.
- [x] (2026-06-19) Verify Copy PNG through the real QR window path; clipboard contained an image after invoking the command.
- [x] (2026-06-19) Verify Save PNG and Save SVG through the QR window save path into `%TEMP%`; PNG had the standard `89 50 4E 47 0D 0A 1A 0A` signature and SVG started with `<svg ... viewBox="0 0 41 41" ...>`.
- [x] (2026-06-19) Re-run automated verification after the final layout fix: isolated Release build of `AiteBar.csproj` passed with 0 warnings/0 errors, and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build` passed 503/503 tests.

## Surprises & Discoveries

- Observation: The bundled Fluent icon map does not contain the originally planned `\uE944` QR icon, but it does contain `ic_fluent_qr_code_24_regular` at decimal 63029, which is `\uF635`.
  Evidence: `Select-String` against `AiteBar/Resources/FluentSystemIcons.json` returned `ic_fluent_qr_code_24_regular: 63029`; the implementation uses `\uF635`.

- Observation: QRCoder 1.8.0 API differs from the initial plan in two important places. `XamlQRCode` is in namespace `QRCoder.Xaml`, and `SvgQRCode.GetGraphic` uses `drawQuietZones: bool` rather than a numeric margin overload.
  Evidence: The installed package XML at `%USERPROFILE%\.nuget\packages\qrcoder.xaml\1.8.0\lib\net6.0-windows7.0\QRCoder.Xaml.xml` documents `T:QRCoder.Xaml.XamlQRCode`; `QRCoder.xml` documents `SvgQRCode.GetGraphic(..., System.Boolean, ...)`.

- Observation: Sandboxed WPF builds failed on generated `obj`/`wpftmp` files with access denied, while the same build outside the sandbox succeeded.
  Evidence: Sandboxed `dotnet build` failed on `App.g.cs` and `AiteBar_*_wpftmp.csproj`; escalated `dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` completed with 0 warnings and 0 errors.

- Observation: Full `dotnet test` and full `dotnet vstest` timed out in this environment, but the new and modified focused test sets completed successfully.
  Evidence: Filtered `dotnet vstest ... --TestCaseFilter:FullyQualifiedName~QRCodeServiceTests` passed 14/14; filtered `HotkeyServiceTests` passed 11/11.

- Observation: After stale `testhost.exe` processes from the timed-out runs were stopped, the full suite ran and found a real localization failure in `QRCodeGeneratorWindow.xaml`.
  Evidence: `LocalizationServiceTests.XamlTextProperties_DoNotContainTranslatableLiteralText` reported hardcoded ECC combo item text: `L - 7%`, `M - 15%`, `Q - 25%`, and `H - 30%`.

- Observation: Moving the ECC combo item text into resource keys fixed the localization gate and the full suite now passes.
  Evidence: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` passed 503/503 tests.

- Observation: Runtime startup smoke was skipped because an existing `AiteBar` process was already running.
  Evidence: Safe smoke command returned `SKIPPED_EXISTING:45504` and did not stop the existing process.

- Observation: Manual UI automation against the real desktop must not modify the user's working `%APPDATA%\Codebdbd\Aite Bar\settings.json`; doing so temporarily hid the user's normal buttons until the preserved backup was restored.
  Evidence: The original settings were restored from `C:\Users\ostee\AppData\Local\Temp\aitebar_settings_qr_backup_516c64940c68421ca8dc245607c9887d.json`, returning `ActiveContextId=context-2`, 86 user elements, and the previous utility order. Final QR verification used an isolated TEMP build and direct window harness instead of editing working settings.

- Observation: The QR window follows the same utility registration pattern as TimerStopwatch: `[Utility]`, `UtilityBase<TWindow>`, `CreateWindow`, `ShowWindow`, and `ShowNearPanel`.
  Evidence: `QRCodeGeneratorUtility.cs` mirrors `TimerStopwatchUtility.cs`; both delegate positioning to the utility window's `ShowNearPanel(AppSettingsService)` method.

- Observation: A QR XAML change that attempted to use the same `Icon="pack://application:,,,/Resources/app.ico"` URI as TimerStopwatch made isolated QR window construction fail with `IOException: Не удается найти ресурс "resources/app.ico"`.
  Evidence: The isolated constructor smoke failed until the QR window icon URI was restored to `pack://application:,,,/AiteBar;component/Resources/app.ico`.

- Observation: The final QR layout matches the compact dark utility style used by TimerStopwatch and avoids the visual overlap seen in the first manual screenshot.
  Evidence: Isolated render screenshot `C:\Users\ostee\AppData\Local\Temp\aitebar_qr_window_smoke.png` showed a 540x520 dark window with aligned field rows, separated pixel-size label/value, centered QR preview, a dedicated status line, and three evenly sized footer buttons.

## Decision Log

- Decision: Use **QRCoder** library (v1.8.0) for QR generation.
  Rationale: Zero external dependencies for PNG/SVG renderers, supports .NET 5+/net10.0, widely used (100M+ downloads), MIT license, includes payload generators (WiFi, vCard, URL, etc.) that could be exposed in the UI later. Already compatible with the project's net10.0-windows target.

- Decision: Use `SvgQRCode` for SVG export, `PngByteQRCode` for PNG export, and `XamlQRCode` for live WPF preview.
  Rationale: XamlQRCode produces a `DrawingImage` natively usable in WPF `Image` controls without bitmap conversion. PNG and SVG renderers produce byte[] and string respectively, suitable for clipboard/save operations.

- Decision: Follow `UtilityBase<TWindow>` pattern (like IconConverter, TimerStopwatch) rather than raw `IUtility` (like ColorPicker).
  Rationale: UtilityBase handles singleton window lifecycle, activation, error handling, and localization integration automatically.

- Decision: Default visibility `ShowPresetQRCodeGenerator = false` (same as ColorPicker and QuickNote).
  Rationale: New utilities should not clutter the panel by default; users opt in via settings or right-click context menu.

- Decision: Use Fluent icon glyph `\uF635` instead of the originally planned `\uE944`.
  Rationale: `\uF635` is the actual bundled `ic_fluent_qr_code_24_regular` glyph; using an existing glyph avoids a missing-icon panel button.
  Date/Author: 2026-06-19 / Codex.

- Decision: Treat `QRCodeGenerationOptions.Margin` as an on/off quiet-zone setting for QRCoder 1.8.0 renderers.
  Rationale: `PngByteQRCode`, `SvgQRCode`, and `XamlQRCode` expose `drawQuietZones: bool` for the relevant overloads. The numeric margin remains in the model for future expansion, but the current library API cannot render arbitrary quiet-zone module counts through these overloads.
  Date/Author: 2026-06-19 / Codex.

- Decision: Add the settings hotkey row using the existing `ToggleButton` modifier style instead of the plan's simple `CheckBox` snippet.
  Rationale: `AppSettingsWindow` already uses styled modifier toggle buttons for every hotkey row, and matching that pattern preserves the compact settings UI contract.
  Date/Author: 2026-06-19 / Codex.

## Outcomes & Retrospective

The QR Code Generator feature is implemented end to end in code: the utility is registered, can be enabled in settings, supports a configurable hotkey, opens a dark WPF window near the panel, renders a live QR preview, and can copy PNG, save PNG, or save SVG. The pure service behavior is covered by focused unit tests, and the explicit hotkey command list tests now include `QRCodeGenerator`.

Automated and isolated runtime verification are complete. Release build verification passed in a TEMP output without touching the user's running application, `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build` passed 503/503 tests, the isolated QR window smoke rendered a valid preview with enabled actions, Copy PNG placed an image on the clipboard, and Save PNG/SVG produced valid files in `%TEMP%`. The QR window layout was adjusted after manual screenshot review so labels, slider values, status text, and command buttons do not overlap.

## Context and Orientation

### Existing utility architecture

Every utility in AiteBar follows this pattern:

1. **Utility class** (`*Utility.cs`): Inherits `UtilityBase<TWindow>`, decorated with `[Utility]` and `[SupportedOSPlatform("windows6.1")]`. Defines `Id`, `DisplayNameKey`, `IconGlyph`, `IconColor`. Override `CreateWindow()` and `ShowWindow()`. Auto-discovered at startup via `UtilityRegistry.RegisterAllFromAssembly()`.

2. **Window class** (`*Window.xaml` + `.xaml.cs`): Inherits `DarkWindow`. XAML defines the UI. Code-behind has `ShowNearPanel(AppSettingsService)` for positioning near the panel edge.

3. **Service class** (`*Service.cs`): Contains pure logic, no UI dependencies. Takes options, returns results.

4. **Models class** (`*Models.cs` or in `Models.cs`): Options records, result records, enums.

5. **Registration**: Added to `UnifiedButtonService.UtilityButtons` list. Added to `MainWindow.ExecuteUnifiedButtonActionAsync()` switch. Added to `AppSettings` as `ShowPreset*` bool. Added to `HotkeyCommand` enum if hotkey desired. Added to localization `.resx` files with `Tool_*` and `Main_*Tooltip` keys. Added to `AppSettingsWindow` checkbox.

### Key files to reference

- `AiteBar/UtilityRegistry.cs` — `IUtility`, `UtilityBase<TWindow>`, `[Utility]` attribute, `UtilityRegistry`
- `AiteBar/UnifiedButtonService.cs` — `UtilityButtonDef` list (lines 11-24)
- `AiteBar/MainWindow.xaml.cs` — `ExecuteUnifiedButtonActionAsync()` switch (lines 1539-1576)
- `AiteBar/Models.cs` — `AppSettings` class (lines 122-173)
- `AiteBar/HotkeyService.cs` — `HotkeyCommand` enum (lines 9-19)
- `AiteBar/IconConverterUtility.cs` — exemplary `UtilityBase<TWindow>` implementation
- `AiteBar/IconConverterService.cs` — exemplary service with async patterns
- `AiteBar/IconConverterModels.cs` — exemplary options/result models
- `AiteBar/IconConverterWindow.xaml` — exemplary DarkWindow XAML with header buttons, styles
- `AiteBar/DarkWindow.cs` — base window class with localization support
- `AiteBar/LocalizationService.cs` — `Get()`, `Format()`, `LocExtension` markup extension
- `AiteBar/Resources/Strings.resx` (and `.de.resx`, `.ru.resx`, `.uk.resx`) — localization resources
- `AiteBar/AppSettingsWindow.xaml.cs` — settings checkboxes for utility visibility (lines 367-378)

### QRCoder library API (v1.8.0)

The library provides these key types we will use:

    // Generate QR data from text
    using var qrData = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.Q);

    // Render to PNG byte array
    var pngRenderer = new PngByteQRCode(qrData);
    byte[] pngBytes = pngRenderer.GetGraphic(pixelSize); // pixelSize = pixels per module

    // Render to SVG string
    var svgRenderer = new SvgQRCode(qrData);
    string svg = svgRenderer.GetGraphic(pixelSize);

    // Render to WPF DrawingImage (for live preview)
    var xamlRenderer = new QRCoder.Xaml.XamlQRCode(qrData);
    var drawingImage = xamlRenderer.GetGraphic(pixelSize);

Error correction levels: L (7%), M (15%), Q (25%), H (30%). Default Q is recommended for general use.

Maximum data capacity (QR version 40, ECC Q): 1,663 bytes / 1,273 alphanumeric / 967 kanji.

Renderer method signatures (from QRCoder 1.8.0 docs):

    // PngByteQRCode — returns byte[]
    byte[] GetGraphic(int pixelsPerModule)
    byte[] GetGraphic(int pixelsPerModule, byte[] darkColorRgba, byte[] lightColorRgba, bool drawQuietZones = true)

    // SvgQRCode — returns string
    string GetGraphic(int pixelsPerModule)
    string GetGraphic(int pixelsPerModule, string darkColorHtmlHex, string lightColorHtmlHex, bool drawQuietZones = true)

    // XamlQRCode — returns System.Windows.Media.DrawingImage; class is QRCoder.Xaml.XamlQRCode
    DrawingImage GetGraphic(int pixelsPerModule)
    DrawingImage GetGraphic(int pixelsPerModule, string darkColorHtmlHex, string lightColorHtmlHex, bool drawQuietZones = true)

Important: In the installed QRCoder 1.8.0 packages used by this repository, `SvgQRCode.GetGraphic` with color parameters takes `bool drawQuietZones`, not a numeric margin. `PngByteQRCode.GetGraphic` also takes `bool drawQuietZones` and accepts color byte arrays for custom colors. `XamlQRCode` is in the `QRCoder.Xaml` namespace.

## Plan of Work

### Step 1: Add QRCoder NuGet packages

Edit `AiteBar/AiteBar.csproj` and add two packages:

    <PackageReference Include="QRCoder" Version="1.8.0" />
    <PackageReference Include="QRCoder.Xaml" Version="1.8.0" />

The base `QRCoder` package provides `PngByteQRCode` and `SvgQRCode`. The `QRCoder.Xaml` package adds `XamlQRCode` which produces a WPF `DrawingImage` for live preview. Both are needed.

Run `dotnet restore` to verify compatibility with net10.0-windows.

### Step 2: Create QRCodeModels.cs

Create `AiteBar/QRCodeModels.cs` with:

    namespace AiteBar;

    public enum QRCodeEccLevel
    {
        L,  // 7% recovery
        M,  // 15% recovery
        Q,  // 25% recovery (default)
        H   // 30% recovery
    }

    public sealed class QRCodeGenerationOptions
    {
        public string Text { get; init; } = string.Empty;
        public int PixelSize { get; init; } = 20;     // pixels per QR module
        public int Margin { get; init; } = 4;          // quiet zone in modules
        public QRCodeEccLevel EccLevel { get; init; } = QRCodeEccLevel.Q;
        public string DarkColor { get; init; } = "#000000";
        public string LightColor { get; init; } = "#FFFFFF";
    }

    public sealed class QRCodeGenerationResult
    {
        public byte[] PngBytes { get; init; } = [];
        public string SvgContent { get; init; } = string.Empty;
        public int ModuleCount { get; init; }
        public int Version { get; init; }
    }

### Step 3: Create QRCodeService.cs

Create `AiteBar/QRCodeService.cs` with:

    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using QRCoder;

    namespace AiteBar;

    public sealed class QRCodeService
    {
        public Task<QRCodeGenerationResult> GenerateAsync(
            QRCodeGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                ValidateOptions(options);

                var eccLevel = options.EccLevel switch
                {
                    QRCodeEccLevel.L => QRCodeGenerator.ECCLevel.L,
                    QRCodeEccLevel.M => QRCodeGenerator.ECCLevel.M,
                    QRCodeEccLevel.Q => QRCodeGenerator.ECCLevel.Q,
                    QRCodeEccLevel.H => QRCodeGenerator.ECCLevel.H,
                    _ => QRCodeGenerator.ECCLevel.Q
                };

                using var qrData = QRCodeGenerator.GenerateQrCode(options.Text, eccLevel);

                cancellationToken.ThrowIfCancellationRequested();

                // PNG
                var pngRenderer = new PngByteQRCode(qrData);
                byte[] pngBytes = pngRenderer.GetGraphic(
                    options.PixelSize,
                    ParseColorBytes(options.DarkColor),
                    ParseColorBytes(options.LightColor),
                    drawQuietZones: options.Margin > 0);

                cancellationToken.ThrowIfCancellationRequested();

                // SVG
                var svgRenderer = new SvgQRCode(qrData);
                string svg = svgRenderer.GetGraphic(
                    options.PixelSize,
                    options.DarkColor,
                    options.LightColor,
                    drawQuietZones: options.Margin > 0);

                return new QRCodeGenerationResult
                {
                    PngBytes = pngBytes,
                    SvgContent = svg,
                    ModuleCount = qrData.ModuleMatrix.Count,
                    Version = GetVersion(qrData)
                };
            }, cancellationToken);
        }

        public QRCodeData GenerateQrData(string text, QRCodeEccLevel eccLevel = QRCodeEccLevel.Q)
        {
            var level = eccLevel switch
            {
                QRCodeEccLevel.L => QRCodeGenerator.ECCLevel.L,
                QRCodeEccLevel.M => QRCodeGenerator.ECCLevel.M,
                QRCodeEccLevel.Q => QRCodeGenerator.ECCLevel.Q,
                QRCodeEccLevel.H => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.Q
            };
            return QRCodeGenerator.GenerateQrCode(text, level);
        }

        public byte[] RenderPng(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
        {
            var renderer = new PngByteQRCode(data);
            return renderer.GetGraphic(pixelSize, darkColor, lightColor, drawQuietZones: margin > 0);
        }

        public string RenderSvg(QRCodeData data, int pixelSize, string darkColor, string lightColor, int margin)
        {
            var renderer = new SvgQRCode(data);
            return renderer.GetGraphic(pixelSize, darkColor, lightColor, drawQuietZones: margin > 0);
        }

        public static int GetVersion(QRCodeData data)
        {
            int moduleCount = data.ModuleMatrix.Count;
            return moduleCount <= 21 ? 1 : ((moduleCount - 21) / 4) + 1;
        }

        private static byte[] ParseColorBytes(string color)
        {
            string normalized = color.Trim().TrimStart('#');
            return
            [
                Convert.ToByte(normalized[..2], 16),
                Convert.ToByte(normalized.Substring(2, 2), 16),
                Convert.ToByte(normalized.Substring(4, 2), 16)
            ];
        }

        private static void ValidateOptions(QRCodeGenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.Text))
                throw new ArgumentException(LocalizationService.Get("QRCodeGenerator_ErrorEmptyText"));

            if (options.Text.Length > 4296)
                throw new ArgumentException(LocalizationService.Format("QRCodeGenerator_ErrorTextTooLong", 4296));

            if (options.PixelSize < 1 || options.PixelSize > 100)
                throw new ArgumentOutOfRangeException(nameof(options.PixelSize));

            if (options.Margin < 0 || options.Margin > 10)
                throw new ArgumentOutOfRangeException(nameof(options.Margin));
        }
    }

The service provides both batch `GenerateAsync` (for save operations) and individual `GenerateQrData`/`RenderPng`/`RenderSvg` methods (for live preview where we need the QRData for XamlQRCode). This avoids generating the QR data twice (once for preview, once for save).

Note: `XamlQRCode` is from the `QRCoder.Xaml` namespace and requires the `QRCoder.Xaml` NuGet package. The service should expose `GenerateQrData` returning `QRCodeData` so the window can call `new QRCoder.Xaml.XamlQRCode(qrData).GetGraphic(pixelSize, darkColor, lightColor, drawQuietZones: true)` directly to get a `DrawingImage` for the WPF `Image.Source`.

### Step 4: Create QRCodeGeneratorWindow.xaml

Create `AiteBar/QRCodeGeneratorWindow.xaml`. This section specifies the exact design contract derived from analyzing all existing utility windows (IconConverter, TimerStopwatch, QuickNote). Every visual detail must match.

#### Complete XAML file

The full file for `AiteBar/QRCodeGeneratorWindow.xaml`:

    <local:DarkWindow x:Class="AiteBar.QRCodeGeneratorWindow"
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:local="clr-namespace:AiteBar"
            Icon="pack://application:,,,/AiteBar;component/Resources/app.ico"
            Width="480" Height="440" MinWidth="420" MinHeight="380"
            WindowStartupLocation="Manual" WindowStyle="None" AllowsTransparency="True"
            Background="Transparent" Foreground="White" ResizeMode="CanResizeWithGrip"
            ShowInTaskbar="False" Topmost="True"
            PreviewKeyDown="Window_PreviewKeyDown">

        <Window.Resources>
            <!-- Copied verbatim from IconConverterWindow.xaml lines 14-157 -->
            <Style x:Key="HeaderButtonStyle" TargetType="Button">
                <Setter Property="Width" Value="32"/>
                <Setter Property="Height" Value="32"/>
                <Setter Property="FontFamily" Value="pack://application:,,,/Resources/#FluentSystemIcons-Regular"/>
                <Setter Property="FontSize" Value="18"/>
                <Setter Property="Foreground" Value="#AEB6C1"/>
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderBrush" Value="Transparent"/>
                <Setter Property="BorderThickness" Value="1"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsKeyboardFocused" Value="True">
                                    <Setter TargetName="Bd" Property="BorderBrush" Value="#3ABEFF"/>
                                </Trigger>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Bd" Property="Background" Value="#25FFFFFF"/>
                                    <Setter Property="Foreground" Value="White"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

            <Style x:Key="CommandButtonStyle" TargetType="Button">
                <Setter Property="Height" Value="34"/>
                <Setter Property="MinWidth" Value="72"/>
                <Setter Property="Padding" Value="14,0"/>
                <Setter Property="Foreground" Value="#E8EDF2"/>
                <Setter Property="Background" Value="{StaticResource FormControlBackground}"/>
                <Setter Property="BorderBrush" Value="{StaticResource FormControlBorderBrush}"/>
                <Setter Property="BorderThickness" Value="1"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Bd"
                                    Background="{TemplateBinding Background}"
                                    BorderBrush="{TemplateBinding BorderBrush}"
                                    BorderThickness="{TemplateBinding BorderThickness}"
                                    CornerRadius="4">
                                <ContentPresenter Margin="{TemplateBinding Padding}"
                                                  HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"
                                                  RecognizesAccessKey="True"/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsKeyboardFocused" Value="True">
                                    <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource FormControlFocusBorderBrush}"/>
                                </Trigger>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Bd" Property="Background" Value="{StaticResource FormControlBackgroundHover}"/>
                                </Trigger>
                                <Trigger Property="IsEnabled" Value="False">
                                    <Setter TargetName="Bd" Property="Opacity" Value="0.55"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

            <Style x:Key="PrimaryCommandButtonStyle" TargetType="Button" BasedOn="{StaticResource CommandButtonStyle}">
                <Setter Property="Foreground" Value="White"/>
                <Setter Property="Background" Value="{StaticResource AccentColor}"/>
                <Setter Property="BorderBrush" Value="{StaticResource AccentColor}"/>
            </Style>

            <Style x:Key="FieldLabelStyle" TargetType="TextBlock">
                <Setter Property="Foreground" Value="{StaticResource MutedText}"/>
                <Setter Property="FontSize" Value="11"/>
                <Setter Property="VerticalAlignment" Value="Center"/>
            </Style>

            <Style x:Key="OptionRadioButtonStyle" TargetType="RadioButton">
                <Setter Property="Height" Value="28"/>
                <Setter Property="Foreground" Value="#D8DEE6"/>
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderBrush" Value="Transparent"/>
                <Setter Property="BorderThickness" Value="1"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="FontSize" Value="12"/>
                <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="RadioButton">
                            <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4" Padding="8,4">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <Border x:Name="RadioBorder"
                                            Width="16" Height="16"
                                            BorderBrush="#3A3A3E" BorderThickness="1"
                                            Background="#2D2D30" CornerRadius="8"
                                            VerticalAlignment="Center" Margin="0,0,6,0">
                                        <Ellipse x:Name="RadioCheck"
                                                 Width="8" Height="8"
                                                 Fill="White" Visibility="Collapsed"/>
                                    </Border>
                                    <ContentPresenter Grid.Column="1" HorizontalAlignment="Left" VerticalAlignment="Center"/>
                                </Grid>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="RadioBorder" Property="Background" Value="#343438"/>
                                    <Setter TargetName="RadioBorder" Property="BorderBrush" Value="#4A4A4E"/>
                                </Trigger>
                                <Trigger Property="IsChecked" Value="True">
                                    <Setter TargetName="RadioBorder" Property="Background" Value="{StaticResource AccentColor}"/>
                                    <Setter TargetName="RadioBorder" Property="BorderBrush" Value="{StaticResource AccentColor}"/>
                                    <Setter TargetName="RadioCheck" Property="Visibility" Value="Visible"/>
                                </Trigger>
                                <Trigger Property="IsKeyboardFocused" Value="True">
                                    <Setter TargetName="Bd" Property="BorderBrush" Value="#3ABEFF"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
        </Window.Resources>

        <Grid>
            <Border Margin="8" CornerRadius="8" Background="{StaticResource BorderColor}" BorderBrush="#332A9CFF" BorderThickness="0.7">
                <Border.Effect>
                    <DropShadowEffect Color="#000000" BlurRadius="14" ShadowDepth="2" Direction="270" Opacity="0.45"/>
                </Border.Effect>

                <Border CornerRadius="7" Background="{StaticResource PanelBackground}">
                    <Grid Margin="18">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="32"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="*"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <!-- Header -->
                        <Grid Grid.Row="0" MouseLeftButtonDown="Header_MouseLeftButtonDown">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="{local:Loc ResourceKey=Tool_QRCodeGenerator}"
                                       FontSize="14" FontWeight="SemiBold"
                                       Foreground="White" VerticalAlignment="Center"/>
                            <Button Grid.Column="1"
                                    Content="&#xF369;"
                                    Style="{StaticResource HeaderButtonStyle}"
                                    ToolTip="{local:Loc ResourceKey=Common_Close}"
                                    Click="BtnClose_Click"/>
                        </Grid>

                        <!-- Input card -->
                        <Border Grid.Row="1" Margin="0,12,0,0"
                                Background="#151922" BorderBrush="#273344" BorderThickness="1"
                                CornerRadius="6" Padding="14">
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>
                                <TextBlock Text="{local:Loc ResourceKey=QRCodeGenerator_InputLabel}"
                                           Foreground="White" FontSize="13" FontWeight="SemiBold"/>
                                <TextBox x:Name="TxtInput" Grid.Row="1"
                                         Margin="0,8,0,0"
                                         Style="{StaticResource BaseTextBoxStyle}"
                                         TextChanged="TxtInput_TextChanged"
                                         AcceptsReturn="False"
                                         MaxLength="4296"/>
                            </Grid>
                        </Border>

                        <!-- Options row -->
                        <Grid Grid.Row="2" Margin="0,10,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0" Margin="0,0,8,0">
                                <TextBlock Text="{local:Loc ResourceKey=QRCodeGenerator_EccLevel}"
                                           Style="{StaticResource FieldLabelStyle}" Margin="0,0,0,6"/>
                                <StackPanel Orientation="Horizontal">
                                    <RadioButton x:Name="RbEccL" Content="L (7%)" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="EccLevel_Changed"/>
                                    <RadioButton x:Name="RbEccM" Content="M (15%)" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="EccLevel_Changed"/>
                                    <RadioButton x:Name="RbEccQ" Content="Q (25%)" IsChecked="True" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="EccLevel_Changed"/>
                                    <RadioButton x:Name="RbEccH" Content="H (30%)"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="EccLevel_Changed"/>
                                </StackPanel>
                            </StackPanel>
                            <StackPanel Grid.Column="1">
                                <TextBlock Text="{local:Loc ResourceKey=QRCodeGenerator_PixelSize}"
                                           Style="{StaticResource FieldLabelStyle}" Margin="0,0,0,6"/>
                                <StackPanel Orientation="Horizontal">
                                    <RadioButton x:Name="RbPixel10" Content="10" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="PixelSize_Changed"/>
                                    <RadioButton x:Name="RbPixel15" Content="15" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="PixelSize_Changed"/>
                                    <RadioButton x:Name="RbPixel20" Content="20" IsChecked="True" Margin="0,0,6,0"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="PixelSize_Changed"/>
                                    <RadioButton x:Name="RbPixel30" Content="30"
                                                 Style="{StaticResource OptionRadioButtonStyle}"
                                                 Checked="PixelSize_Changed"/>
                                </StackPanel>
                            </StackPanel>
                        </Grid>

                        <!-- Preview area -->
                        <Border Grid.Row="3" Margin="0,10,0,0"
                                Background="#101318" BorderBrush="#273344" BorderThickness="1"
                                CornerRadius="6" Padding="14">
                            <Grid>
                                <Image x:Name="ImgPreview"
                                       Stretch="Uniform" MaxWidth="300" MaxHeight="300"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                <TextBlock x:Name="TxtEmptyHint"
                                           Text="{local:Loc ResourceKey=QRCodeGenerator_EmptyHint}"
                                           Foreground="#7B7B80" FontSize="13"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                        </Border>

                        <!-- Status + buttons -->
                        <Grid Grid.Row="4" Margin="0,10,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock x:Name="TxtStatus"
                                       Foreground="{StaticResource MutedText}" FontSize="11"
                                       VerticalAlignment="Center"/>
                            <StackPanel Grid.Column="1" Orientation="Horizontal">
                                <Button x:Name="BtnCopyPng"
                                        Content="{local:Loc ResourceKey=QRCodeGenerator_CopyPng}"
                                        Style="{StaticResource CommandButtonStyle}" Margin="0,0,8,0"
                                        IsEnabled="False"
                                        Click="BtnCopyPng_Click"/>
                                <Button x:Name="BtnSavePng"
                                        Content="{local:Loc ResourceKey=QRCodeGenerator_SavePng}"
                                        Style="{StaticResource CommandButtonStyle}" Margin="0,0,8,0"
                                        IsEnabled="False"
                                        Click="BtnSavePng_Click"/>
                                <Button x:Name="BtnSaveSvg"
                                        Content="{local:Loc ResourceKey=QRCodeGenerator_SaveSvg}"
                                        Style="{StaticResource PrimaryCommandButtonStyle}"
                                        IsEnabled="False"
                                        Click="BtnSaveSvg_Click"/>
                            </StackPanel>
                        </Grid>
                    </Grid>
                </Border>
            </Border>
        </Grid>
    </local:DarkWindow>

Critical: Every `DarkWindow`-based utility uses this exact window attribute set. Never deviate.

### Step 5: Create QRCodeGeneratorWindow.xaml.cs

Code-behind following `IconConverterWindow.xaml.cs` pattern. This is the full implementation — not stubs.

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using QRCoder;
    using Forms = System.Windows.Forms;

    namespace AiteBar;

    public partial class QRCodeGeneratorWindow : DarkWindow
    {
        private readonly QRCodeService _service = new();
        private readonly DispatcherTimer _debounceTimer;
        private CancellationTokenSource? _previewCts;
        private QRCodeData? _currentQrData;
        private string _currentText = string.Empty;

        public QRCodeGeneratorWindow()
        {
            InitializeComponent();
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public void ShowNearPanel(AppSettingsService settingsService)
        {
            var settings = settingsService.Settings;
            var screens = Forms.Screen.AllScreens;
            var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
                ? screens[settings.MonitorIndex]
                : Forms.Screen.PrimaryScreen;
            var work = screen?.WorkingArea
                ?? Forms.Screen.PrimaryScreen?.WorkingArea
                ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

            var (_, _, shownX, shownY) = QuickNoteLayoutHelper.GetSlideCoordinates(
                settings.Edge, work, Width, Height);
            Left = shownX;
            Top = shownY;
            Show();
            Activate();
        }

        // --- Header drag ---
        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        // --- Close ---
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // --- Escape key ---
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        }

        // --- Options changed handlers ---
        private void EccLevel_Changed(object sender, RoutedEventArgs e) => _ = UpdatePreviewAsync();
        private void PixelSize_Changed(object sender, RoutedEventArgs e) => _ = UpdatePreviewAsync();

        private QRCodeEccLevel GetSelectedEccLevel()
        {
            if (RbEccL.IsChecked == true) return QRCodeEccLevel.L;
            if (RbEccM.IsChecked == true) return QRCodeEccLevel.M;
            if (RbEccH.IsChecked == true) return QRCodeEccLevel.H;
            return QRCodeEccLevel.Q;
        }

        private int GetSelectedPixelSize()
        {
            if (RbPixel10.IsChecked == true) return 10;
            if (RbPixel15.IsChecked == true) return 15;
            if (RbPixel30.IsChecked == true) return 30;
            return 20;
        }

        // --- Live preview with debounce ---
        private void TxtInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            _ = UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            string text = TxtInput.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                ImgPreview.Source = null;
                TxtEmptyHint.Visibility = Visibility.Visible;
                TxtStatus.Text = string.Empty;
                _currentQrData?.Dispose();
                _currentQrData = null;
                SetButtonsEnabled(false);
                return;
            }

            TxtEmptyHint.Visibility = Visibility.Collapsed;
            SetButtonsEnabled(false);

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var ct = _previewCts.Token;

            try
            {
                QRCodeEccLevel eccLevel = GetSelectedEccLevel();
                QRCodeData qrData = await Task.Run(() =>
                    _service.GenerateQrData(text, eccLevel), ct);

                ct.ThrowIfCancellationRequested();

                _currentQrData?.Dispose();
                _currentQrData = qrData;

                // Live preview via XamlQRCode (produces WPF DrawingImage)
                // Use small pixel size (8) for fast preview rendering
                var xamlRenderer = new QRCoder.Xaml.XamlQRCode(qrData);
                var drawingImage = xamlRenderer.GetGraphic(8);

                await Dispatcher.InvokeAsync(() =>
                {
                    ImgPreview.Source = drawingImage;
                    int moduleCount = qrData.ModuleMatrix.Count;
                    TxtStatus.Text = LocalizationService.Format(
                        "QRCodeGenerator_Status", QRCodeService.GetVersion(qrData), moduleCount);
                    SetButtonsEnabled(true);
                }, System.Windows.Threading.DispatcherPriority.DataBind, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = LocalizationService.Format(
                        "QRCodeGenerator_SaveFailed", ex.Message);
                });
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            BtnCopyPng.IsEnabled = enabled;
            BtnSavePng.IsEnabled = enabled;
            BtnSaveSvg.IsEnabled = enabled;
        }

        // --- Copy PNG to clipboard ---
        private async void BtnCopyPng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQrData == null) return;

            try
            {
                int pixelSize = GetSelectedPixelSize();
                byte[] pngBytes = await Task.Run(() =>
                    _service.RenderPng(_currentQrData, pixelSize, "#000000", "#FFFFFF", 4));

                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(pngBytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                var dataObject = new DataObject();
                dataObject.SetData(DataFormats.Bitmap, bitmap, true);
                // Also set PNG data for apps that prefer it (e.g. some chat clients)
                dataObject.SetData("PNG", pngBytes, false);
                Clipboard.SetDataObject(dataObject, true);

                TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_Copied");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                TxtStatus.Text = LocalizationService.Format("QRCodeGenerator_SaveFailed", ex.Message);
            }
        }

        // --- Save PNG ---
        private async void BtnSavePng_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQrData == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationService.Get("QRCodeGenerator_SavePngTitle"),
                Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
                DefaultExt = ".png",
                FileName = "qrcode.png"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                int pixelSize = GetSelectedPixelSize();
                byte[] pngBytes = await Task.Run(() =>
                    _service.RenderPng(_currentQrData, pixelSize, "#000000", "#FFFFFF", 4));
                await File.WriteAllBytesAsync(dialog.FileName, pngBytes);
                TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_SaveSuccess");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                TxtStatus.Text = LocalizationService.Format("QRCodeGenerator_SaveFailed", ex.Message);
            }
        }

        // --- Save SVG ---
        private async void BtnSaveSvg_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQrData == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationService.Get("QRCodeGenerator_SaveSvgTitle"),
                Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
                DefaultExt = ".svg",
                FileName = "qrcode.svg"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                int pixelSize = GetSelectedPixelSize();
                string svg = await Task.Run(() =>
                    _service.RenderSvg(_currentQrData, pixelSize, "#000000", "#FFFFFF", 4));
                await File.WriteAllTextAsync(dialog.FileName, svg);
                TxtStatus.Text = LocalizationService.Get("QRCodeGenerator_SaveSuccess");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                TxtStatus.Text = LocalizationService.Format("QRCodeGenerator_SaveFailed", ex.Message);
            }
        }
    }

Key implementation details:

- **Debounce**: A `DispatcherTimer` with 300ms delay. Each keystroke restarts the timer. Only when the user pauses typing does `UpdatePreviewAsync` fire. This prevents generating a QR code for every character typed.

- **Live preview**: `XamlQRCode.GetGraphic(8)` produces a `DrawingImage` (low pixel size for preview — fast rendering). The actual export uses `pixelSize=20` for higher quality.

- **Clipboard PNG**: Uses `DataObject` with both `DataFormats.Bitmap` (for direct paste in image editors) and `"PNG"` format (for apps like Telegram/Discord that check for PNG data). The `BitmapImage` is loaded from the byte array into a `MemoryStream` and frozen for thread safety.

- **Save dialogs**: Use `Microsoft.Win32.SaveFileDialog` (WPF native), not `System.Windows.Forms.SaveFileDialog`.

- **Error display**: Errors are shown in the status bar `TxtStatus`, not in modal dialogs. This matches the non-intrusive pattern used in other utilities for non-critical errors.

- **QRCodeData disposal**: `_currentQrData` is disposed before each regeneration to prevent memory leaks. `QRCodeData` implements `IDisposable`.

### Step 6: Create QRCodeGeneratorUtility.cs

Create `AiteBar/QRCodeGeneratorUtility.cs`:

    using System.Windows;
    using System.Runtime.Versioning;

    namespace AiteBar;

    [SupportedOSPlatform("windows6.1")]
    [Utility]
    public sealed class QRCodeGeneratorUtility : UtilityBase<QRCodeGeneratorWindow>
    {
        public override string Id => "QRCodeGenerator";
        public override string DisplayNameKey => "Tool_QRCodeGenerator";
        public override string IconGlyph => "\uF635";  // FluentSystemIcons: QrCode glyph
        public override string IconColor => "#60A5FA";

        protected override QRCodeGeneratorWindow CreateWindow(AppSettingsService settingsService, Window? owner)
        {
            return new QRCodeGeneratorWindow() { Owner = owner };
        }

        protected override void ShowWindow(QRCodeGeneratorWindow window, AppSettingsService settingsService)
        {
            window.ShowNearPanel(settingsService);
        }
    }

The icon glyph `\uF635` is the QR code icon from FluentSystemIcons-Regular.ttf which is already bundled in the project resources. This maps to `ic_fluent_qr_code_24_regular` in `Resources/FluentSystemIcons.json`.

### Step 7: Register in UnifiedButtonService.cs

Add to the `UtilityButtons` list in `AiteBar/UnifiedButtonService.cs` after the QuickNote entry:

    new("QRCodeGenerator", "\uF635", "#60A5FA", "ShowPresetQRCodeGenerator", "Main_QRCodeGeneratorTooltip"),

### Step 8: Register in MainWindow.xaml.cs

Add to the `ExecuteUnifiedButtonActionAsync()` switch in `AiteBar/MainWindow.xaml.cs` (after the QuickNote case):

    case "QRCodeGenerator":
        await _actionService.LaunchUtilityAsync("QRCodeGenerator", HideDock);
        break;

### Step 9: Add AppSettings visibility toggle

Add to `AppSettings` class in `AiteBar/Models.cs`:

    public bool ShowPresetQRCodeGenerator { get; set; } = false;

### Step 10: Add hotkey support

Four files need changes for hotkey support.

**10a. Add to `HotkeyCommand` enum** in `AiteBar/HotkeyService.cs` (after `TimerStopwatch`):

    public enum HotkeyCommand
    {
        ShowPanel,
        NextContext,
        PreviousContext,
        AddButton,
        FileSorter,
        QuickNote,
        ColorPicker,
        TimerStopwatch,
        QRCodeGenerator
    }

**10b. Add ID and descriptor** in `AiteBar/HotkeyService.cs`:

Add constant after `TimerStopwatchId`:

    internal const int QRCodeGeneratorId = 9008;

Add to `Descriptors` list:

    new HotkeyDescriptor(HotkeyCommand.QRCodeGenerator, QRCodeGeneratorId, "Tool_QRCodeGenerator"),

**10c. Add hotkey binding to `AppSettings`** in `AiteBar/Models.cs` (after `TimerStopwatchHotkey`):

    public HotkeyBinding QRCodeGeneratorHotkey { get; set; } = new();

**10d. Add to `CreateDefinitions` bindings dictionary** in `HotkeyService.cs`:

    [HotkeyCommand.QRCodeGenerator] = settings.QRCodeGeneratorHotkey,

**10e. Add hotkey dispatch** in `MainWindow.xaml.cs` `ExecuteHotkeyCommand()` method (after `TimerStopwatch` case):

    case HotkeyCommand.QRCodeGenerator:
        _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("QRCodeGenerator", HideDock));
        break;

**10f. Add to `AllowedHotkeysWithOwnedWindows`** if the QR window is a owned window (check if it should be in this set — ColorPicker is NOT in it, so QRCodeGenerator likely should NOT be either, since it opens a new window):

The `AllowedHotkeysWithOwnedWindows` set in `MainWindow.xaml.cs` (line 855) contains `QuickNote` and `TimerStopwatch` because those windows can stay open while the panel is active. Since QRCodeGenerator is a modal-style utility (open, use, close), it does NOT need to be in this set.

### Step 11: Add localization keys

Add to `AiteBar/Resources/Strings.resx` (English):

    <data name="Tool_QRCodeGenerator" xml:space="preserve"><value>QR code generator</value></data>
    <data name="Main_QRCodeGeneratorTooltip" xml:space="preserve"><value>Generate QR code from text</value></data>
    <data name="QRCodeGenerator_InputLabel" xml:space="preserve"><value>Text or URL</value></data>
    <data name="QRCodeGenerator_InputPlaceholder" xml:space="preserve"><value>Enter text or URL...</value></data>
    <data name="QRCodeGenerator_EmptyHint" xml:space="preserve"><value>Type something to generate a QR code</value></data>
    <data name="QRCodeGenerator_EccLevel" xml:space="preserve"><value>Error correction</value></data>
    <data name="QRCodeGenerator_PixelSize" xml:space="preserve"><value>Pixel size</value></data>
    <data name="QRCodeGenerator_CopyPng" xml:space="preserve"><value>Copy PNG</value></data>
    <data name="QRCodeGenerator_SavePng" xml:space="preserve"><value>Save PNG</value></data>
    <data name="QRCodeGenerator_SaveSvg" xml:space="preserve"><value>Save SVG</value></data>
    <data name="QRCodeGenerator_Status" xml:space="preserve"><value>QR version {0}, {1}x{1} modules</value></data>
    <data name="QRCodeGenerator_ErrorEmptyText" xml:space="preserve"><value>Text cannot be empty</value></data>
    <data name="QRCodeGenerator_ErrorTextTooLong" xml:space="preserve"><value>Text exceeds maximum length ({0} characters)</value></data>
    <data name="QRCodeGenerator_Copied" xml:space="preserve"><value>QR code copied to clipboard</value></data>
    <data name="QRCodeGenerator_SavePngTitle" xml:space="preserve"><value>Save QR code as PNG</value></data>
    <data name="QRCodeGenerator_SaveSvgTitle" xml:space="preserve"><value>Save QR code as SVG</value></data>
    <data name="QRCodeGenerator_SaveSuccess" xml:space="preserve"><value>QR code saved successfully</value></data>
    <data name="QRCodeGenerator_SaveFailed" xml:space="preserve"><value>Failed to save: {0}</value></data>

Add equivalent keys to `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx`. The minimum required keys for each language:

    Tool_QRCodeGenerator — display name (e.g. "Генератор QR-кодов" in Russian)
    Main_QRCodeGeneratorTooltip — panel tooltip
    QRCodeGenerator_InputLabel — input field label
    QRCodeGenerator_InputPlaceholder — input placeholder text
    QRCodeGenerator_EmptyHint — empty state hint
    QRCodeGenerator_EccLevel — options label
    QRCodeGenerator_PixelSize — options label
    QRCodeGenerator_CopyPng — button text
    QRCodeGenerator_SavePng — button text
    QRCodeGenerator_SaveSvg — button text
    QRCodeGenerator_Status — status format string with {0} and {1}
    QRCodeGenerator_ErrorEmptyText — error message
    QRCodeGenerator_ErrorTextTooLong — error format with {0}
    QRCodeGenerator_Copied — clipboard success
    QRCodeGenerator_SavePngTitle — save dialog title
    QRCodeGenerator_SaveSvgTitle — save dialog title
    QRCodeGenerator_SaveSuccess — save success
    QRCodeGenerator_SaveFailed — save error format with {0}

### Step 12: Add settings checkbox

**12a. Add XAML checkbox** in `AiteBar/AppSettingsWindow.xaml`, in the utility visibility section (after `ChkShowPresetQuickNote`):

    <CheckBox x:Name="ChkShowPresetQRCodeGenerator"
              Content="{local:Loc ResourceKey=Tool_QRCodeGenerator}"/>

**12b. Add LoadSettings binding** in `AiteBar/AppSettingsWindow.xaml.cs` `LoadSettings()` method (after `ChkShowPresetQuickNote` line):

    ChkShowPresetQRCodeGenerator.IsChecked = _settings.ShowPresetQRCodeGenerator;

**12c. Add SaveSettings binding** in `AiteBar/AppSettingsWindow.xaml.cs` `BtnSave_Click()` method (after `ShowPresetQuickNote` line):

    _settings.ShowPresetQRCodeGenerator = ChkShowPresetQRCodeGenerator.IsChecked ?? false;

**12d. Add hotkey UI controls** — follow the exact pattern of existing utilities (e.g. `TimerStopwatch`). In `AppSettingsWindow.xaml`, add hotkey binding controls (checkboxes for Ctrl/Alt/Shift/Win and a ComboBox for key) in the hotkey section. In `AppSettingsWindow.xaml.cs`, add `LoadHotkeyBinding` and `BuildHotkeyBinding` calls following the same pattern as `timerStopwatchBinding`.

For the hotkey UI, add these controls in `AppSettingsWindow.xaml`:

    <!-- QR Code Generator hotkey -->
    <DockPanel Margin="0,12,0,0">
        <TextBlock Text="{local:Loc ResourceKey=Tool_QRCodeGenerator}" Style="{StaticResource FieldLabelStyle}" Width="140" VerticalAlignment="Center"/>
        <ToggleButton x:Name="ChkQRCodeGeneratorCtrl" Content="Ctrl"/>
        <ToggleButton x:Name="ChkQRCodeGeneratorShift" Content="Shift"/>
        <ToggleButton x:Name="ChkQRCodeGeneratorAlt" Content="Alt"/>
        <ToggleButton x:Name="ChkQRCodeGeneratorWin" Content="Win"/>
        <ComboBox x:Name="CmbQRCodeGeneratorKey" Width="200" VerticalAlignment="Center"/>
    </DockPanel>

In `AppSettingsWindow.xaml.cs` `LoadSettings()`:

    LoadHotkeyBinding(
        _settings.QRCodeGeneratorHotkey,
        ChkQRCodeGeneratorCtrl, ChkQRCodeGeneratorAlt,
        ChkQRCodeGeneratorShift, ChkQRCodeGeneratorWin, CmbQRCodeGeneratorKey);

In `AppSettingsWindow.xaml.cs` `BtnSave_Click()`:

    var qrCodeGeneratorBinding = BuildHotkeyBinding(
        ChkQRCodeGeneratorCtrl, ChkQRCodeGeneratorAlt,
        ChkQRCodeGeneratorShift, ChkQRCodeGeneratorWin, CmbQRCodeGeneratorKey);
    // Add to ValidateHotkeyBindings call
    _settings.QRCodeGeneratorHotkey = qrCodeGeneratorBinding;

### Step 13: Add to GetUtilityVisibility/SetUtilityVisibility

In `AiteBar/AppSettingsService.cs`, add the QRCodeGenerator entry to both methods.

In `GetUtilityVisibility` (add before the `_ => false` default case):

    "ShowPresetQRCodeGenerator" => settings.ShowPresetQRCodeGenerator,

In `SetUtilityVisibility` (add before the closing brace):

    case "ShowPresetQRCodeGenerator": settings.ShowPresetQRCodeGenerator = visible; break;

### Step 14: Write unit tests

Create `AiteBar.Tests/QRCodeServiceTests.cs`:

    using System.Text;
    using Xunit;

    namespace AiteBar.Tests;

    public class QRCodeServiceTests
    {
        private readonly QRCodeService _service = new();

        [Fact]
        public async Task GenerateAsync_ValidText_ReturnsNonEmptyPngAndSvg()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = "https://example.com",
                PixelSize = 10,
                Margin = 2,
                EccLevel = QRCodeEccLevel.Q
            };

            var result = await _service.GenerateAsync(options);

            Assert.NotEmpty(result.PngBytes);
            Assert.NotEmpty(result.SvgContent);
            Assert.True(result.ModuleCount > 0);
            Assert.True(result.Version > 0);
        }

        [Fact]
        public async Task GenerateAsync_PngBytes_StartWithPngHeader()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = "Hello World",
                PixelSize = 10
            };

            var result = await _service.GenerateAsync(options);

            // PNG magic bytes: 0x89 0x50 0x4E 0x47
            Assert.Equal(0x89, result.PngBytes[0]);
            Assert.Equal(0x50, result.PngBytes[1]); // P
            Assert.Equal(0x4E, result.PngBytes[2]); // N
            Assert.Equal(0x47, result.PngBytes[3]); // G
        }

        [Fact]
        public async Task GenerateAsync_SvgContent_StartsWithSvgTag()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = "Hello World",
                PixelSize = 10
            };

            var result = await _service.GenerateAsync(options);

            Assert.StartsWith("<svg", result.SvgContent);
        }

        [Fact]
        public async Task GenerateAsync_EmptyText_ThrowsArgumentException()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = ""
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GenerateAsync(options));
        }

        [Fact]
        public async Task GenerateAsync_WhitespaceText_ThrowsArgumentException()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = "   "
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GenerateAsync(options));
        }

        [Fact]
        public async Task GenerateAsync_TextTooLong_ThrowsArgumentException()
        {
            var options = new QRCodeGenerationOptions
            {
                Text = new string('A', 4297)
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GenerateAsync(options));
        }

        [Fact]
        public async Task GenerateAsync_DifferentEccLevels_ProduceDifferentModuleCounts()
        {
            // Use a longer text where ECC level affects the QR version
            string text = "The quick brown fox jumps over the lazy dog. "
                        + "Pack my box with five dozen liquor jugs.";

            var optionsL = new QRCodeGenerationOptions { Text = text, EccLevel = QRCodeEccLevel.L };
            var optionsH = new QRCodeGenerationOptions { Text = text, EccLevel = QRCodeEccLevel.H };

            var resultL = await _service.GenerateAsync(optionsL);
            var resultH = await _service.GenerateAsync(optionsH);

            // Higher ECC = more redundancy = larger QR code
            Assert.True(resultH.ModuleCount >= resultL.ModuleCount);
        }

        [Fact]
        public void GenerateQrData_ValidText_ReturnsNonNullData()
        {
            using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

            Assert.NotNull(qrData);
            Assert.True(qrData.ModuleMatrix.Count > 0);
        }

        [Fact]
        public void RenderPng_ValidData_ReturnsNonEmptyBytes()
        {
            using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

            byte[] png = _service.RenderPng(qrData, 10, "#000000", "#FFFFFF", 2);

            Assert.NotEmpty(png);
            Assert.Equal(0x89, png[0]);
        }

        [Fact]
        public void RenderSvg_ValidData_ReturnsSvgString()
        {
            using var qrData = _service.GenerateQrData("test", QRCodeEccLevel.Q);

            string svg = _service.RenderSvg(qrData, 10, "#000000", "#FFFFFF", 2);

            Assert.NotEmpty(svg);
            Assert.Contains("<svg", svg);
        }

        [Theory]
        [InlineData(QRCodeEccLevel.L)]
        [InlineData(QRCodeEccLevel.M)]
        [InlineData(QRCodeEccLevel.Q)]
        [InlineData(QRCodeEccLevel.H)]
        public async Task GenerateAsync_AllEccLevels_ProduceValidQr(QRCodeEccLevel level)
        {
            var options = new QRCodeGenerationOptions
            {
                Text = "https://example.com",
                EccLevel = level
            };

            var result = await _service.GenerateAsync(options);

            Assert.NotEmpty(result.PngBytes);
            Assert.NotEmpty(result.SvgContent);
        }
    }

### Step 15: Build and verify

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Manual verification:
- Launch AiteBar, open settings, enable QR Code Generator visibility.
- Click QR Code Generator button on panel — window opens near panel.
- Type a URL — live preview updates.
- Change ECC level — preview regenerates.
- Click "Copy PNG" — paste in Paint/test image viewer to verify.
- Click "Save PNG" — save dialog, file opens correctly.
- Click "Save SVG" — save dialog, file opens in browser correctly.
- Close window, reopen — window state clean.
- Test with empty text, very long text — error handling works.

## Concrete Steps

    # Working directory: D:\01_Codebdbd\01_projects\mino\aitebar

    # Step 1: Add NuGet packages
    dotnet add AiteBar\AiteBar.csproj package QRCoder --version 1.8.0
    dotnet add AiteBar\AiteBar.csproj package QRCoder.Xaml --version 1.8.0
    dotnet restore AiteBar.sln

    # Step 14: Run tests
    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

After implementation, verify these behaviors:

1. **Panel button appears**: After enabling in settings, QR Code Generator button shows on the panel with correct icon and tooltip.
2. **Window opens**: Clicking the button opens a dark-themed window positioned near the panel edge.
3. **Live preview**: Typing text immediately generates a QR code preview in the window.
4. **ECC level change**: Changing ECC level regenerates the QR code (module count may change).
5. **Copy to clipboard**: "Copy PNG" button copies QR code; paste into Paint/Word/Photoshop shows the image.
6. **Save PNG**: Save dialog opens; saved file is a valid PNG viewable in any image viewer.
7. **Save SVG**: Save dialog opens; saved file is a valid SVG viewable in a browser.
8. **Empty text**: Shows error message, no crash.
9. **Long text**: Shows error message for text > 4296 chars.
10. **Hotkey**: If configured, pressing the hotkey opens the QR Code Generator window.
11. **Settings toggle**: Checkbox in settings correctly shows/hides the utility on the panel.
12. **Localization**: Window title, labels, placeholders, errors display in the selected language.
13. **Window lifecycle**: Closing and reopening works without errors. Only one instance at a time (UtilityBase handles this).

## Idempotence and Recovery

All steps are additive. Adding a NuGet package, creating new files, and adding entries to existing files are idempotent operations — running them multiple times produces the same result. If a step fails (e.g., NuGet restore), retry after fixing the network connection. No destructive operations are involved.

## Artifacts and Notes

Implementation evidence from 2026-06-19:

    dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Build succeeded with 0 warnings and 0 errors when run outside the sandbox.

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --TestCaseFilter:FullyQualifiedName~QRCodeServiceTests
    Passed: 14, Failed: 0, Skipped: 0.

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --TestCaseFilter:FullyQualifiedName~HotkeyServiceTests
    Passed: 11, Failed: 0, Skipped: 0.

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
    Passed: 503, Failed: 0, Skipped: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed: 503, Failed: 0, Skipped: 0.

    dotnet build .\AiteBar.sln -c Release
    Build succeeded with 0 warnings and 0 errors.

Earlier full `dotnet test` and full `dotnet vstest` attempts timed out and left stale `testhost.exe` processes locking the test output. Those stale processes were confirmed to be running from `AiteBar.Tests\bin\Release\net10.0-windows`, stopped, and the full suite then ran normally. Manual UI verification still needs to be performed on a Windows desktop session.

Runtime startup smoke was skipped because `AiteBar` was already running; the safer command reported `SKIPPED_EXISTING:45504` and did not terminate that process.

## Interfaces and Dependencies

### New NuGet dependencies

    QRCoder 1.8.0 (MIT license, zero transitive dependencies for PNG/SVG)
    QRCoder.Xaml 1.8.0 (provides XamlQRCode renderer for WPF DrawingImage preview)

Note: `XamlQRCode` is NOT included in the base `QRCoder` package. It is a separate NuGet package `QRCoder.Xaml` that depends on `QRCoder`. The `XamlQRCode` class lives in the `QRCoder.Xaml` namespace and comes from the `QRCoder.Xaml` assembly. Without this package, the WPF live preview will not compile.

### New files

| File | Purpose |
|------|---------|
| `AiteBar/QRCodeModels.cs` | `QRCodeGenerationOptions`, `QRCodeGenerationResult`, `QRCodeEccLevel` |
| `AiteBar/QRCodeService.cs` | QR generation logic: `GenerateAsync`, `GenerateQrData`, `RenderPng`, `RenderSvg` |
| `AiteBar/QRCodeGeneratorWindow.xaml` | WPF window XAML with input, options, preview, buttons |
| `AiteBar/QRCodeGeneratorWindow.xaml.cs` | Code-behind: live preview, clipboard copy, file save |
| `AiteBar/QRCodeGeneratorUtility.cs` | `UtilityBase<QRCodeGeneratorWindow>` registration |
| `AiteBar.Tests/QRCodeServiceTests.cs` | Unit tests for QRCodeService |

### Modified files

| File | Change |
|------|--------|
| `AiteBar/AiteBar.csproj` | Add `QRCoder` and `QRCoder.Xaml` PackageReferences |
| `AiteBar/UnifiedButtonService.cs` | Add QRCodeGenerator to `UtilityButtons` list |
| `AiteBar/MainWindow.xaml.cs` | Add `case "QRCodeGenerator":` to switch |
| `AiteBar/Models.cs` | Add `ShowPresetQRCodeGenerator` and `QRCodeGeneratorHotkey` to AppSettings |
| `AiteBar/HotkeyService.cs` | Add `QRCodeGenerator` to `HotkeyCommand` enum |
| `AiteBar/AppSettingsService.cs` | Add visibility mapping |
| `AiteBar/AppSettingsWindow.xaml` | Add checkbox for QRCodeGenerator |
| `AiteBar/AppSettingsWindow.xaml.cs` | Wire up checkbox binding |
| `AiteBar/Resources/Strings.resx` | Add localization keys |
| `AiteBar/Resources/Strings.ru.resx` | Add Russian translations |
| `AiteBar/Resources/Strings.uk.resx` | Add Ukrainian translations |
| `AiteBar/Resources/Strings.de.resx` | Add German translations |

## Revision Notes

- 2026-06-19 / Codex: Implemented the QR Code Generator feature, updated living sections with progress and validation evidence, corrected QRCoder 1.8.0 API details discovered from installed package XML, and recorded remaining full-suite/manual verification gaps.
- 2026-06-19 / Codex: Added a localized placeholder overlay for the QR input, fixed hardcoded ECC combo item text found by the full localization suite, reran full `dotnet test` successfully, and narrowed the remaining gap to manual desktop UI verification.
