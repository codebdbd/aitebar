# План миграции AiteBar с .NET 8 на .NET 10

## Контекст

- **Текущая версия**: .NET 8 SDK (`8.0.421`), TFM `net8.0-windows`
- **Целевая версия**: .NET 10 SDK (LTS), TFM `net10.0-windows`
- **Причина**: .NET 8 LTS выходит из поддержки ноябрь 2026; .NET 10 — LTS до 2028
- **Риск**: Низкий. Проект небольшой (111 .cs файлов), без BinaryFormatter, без deprecated API,依赖 mainstream

---

## Этап 0: Подготовка

### 0.1 Установить .NET 10 SDK

```powershell
# Проверить текущую версию
dotnet --version

# Скачать и установить .NET 10 SDK с https://dotnet.microsoft.com/download/dotnet/10.0
# Или через winget:
winget install Microsoft.DotNet.SDK.10
```

### 0.2 Создать ветку

```powershell
git checkout -b migration/net10
```

### 0.3 Очистить артефакты сборки

Удалить stale артефакты, чтобы избежать конфликтов с новым TFM:

```powershell
Remove-Item -Recurse -Force AiteBar\obj, AiteBar.Tests\obj -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force AiteBar.Tests\bin\Release\net8.0-windows -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force artifacts\publish\win-x64 -ErrorAction SilentlyContinue
```

> В `AiteBar/obj/` есть 9 пар stale `wpftmp.*.csproj.nuget.g.props/.targets` файлов от предыдущих сборок. Очистка предотвращает путаницу при восстановлении пакетов.

### 0.4 Проверить C# версию

.NET 10 по умолчанию использует **C# 14** (вместо C# 12 в .NET 8). Проект не переопределяет `LangVersion`, поэтому новый компилятор подхватится автоматически. Возможные последствия:
- Новые warnings от анализаторов (не ошибки)
- Доступ к новым языковым фичам (collection expressions, pattern matching и др.)

Если нужно зафиксировать текущую версию языка, добавить в `Directory.Build.props`:
```xml
<LangVersion>12</LangVersion>
```

---

## Этап 1: Изменение конфигурации (3 файла)

### 1.1 `global.json`

**Файл:** `global.json`
**Строка 3**

```diff
- "version": "8.0.421",
+ "version": "10.0.100",
  "rollForward": "latestFeature"
```

> Примечание: точная версия SDK зависит от доступного релиза. `rollForward: latestFeature` подхватит любой `10.0.1xx`.

### 1.2 `AiteBar/AiteBar.csproj`

**Файл:** `AiteBar/AiteBar.csproj`
**Строка 4**

```diff
- <TargetFramework>net8.0-windows</TargetFramework>
+ <TargetFramework>net10.0-windows</TargetFramework>
```

### 1.3 `AiteBar.Tests/AiteBar.Tests.csproj`

**Файл:** `AiteBar.Tests/AiteBar.Tests.csproj`
**Строка 4**

```diff
- <TargetFramework>net8.0-windows</TargetFramework>
+ <TargetFramework>net10.0-windows</TargetFramework>
```

---

## Этап 2: Перегенерация lock-файлов

### 2.1 Удалить существующие lock-файлы

```powershell
Remove-Item "AiteBar\packages.lock.json" -Force
Remove-Item "AiteBar.Tests\packages.lock.json" -Force
```

### 2.2 Восстановить пакеты

```powershell
dotnet restore AiteBar.sln --force-evaluate
```

> Lock-файлы привязаны к TFM (`net8.0-windows7.0`). После смены TFM они должны быть перегенерированы. Флаг `RestorePackagesWithLockFile=true` в `Directory.Build.props` автоматически создаст новые lock-файлы.

---

## Этап 3: Сборка и тесты

### 3.1 Сборка Release

```powershell
dotnet build AiteBar.sln -c Release
```

**Что проверить:**
- Нет ошибок компиляции
- Нет warnings о deprecated API
- Нет warnings о несовместимости пакетов

### 3.2 Запуск тестов

```powershell
dotnet test AiteBar.Tests\AiteBar.Tests.csproj -c Release
```

**Fallback** (если WPF temp-файлы мешают):

```powershell
dotnet vstest AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll
```

---

## Этап 4: Проверка зависимостей

### 4.1 NuGet-пакеты

| Пакет | Текущая версия | Статус совместимости |
|-------|---------------|---------------------|
| Sentry | 6.6.0 | Проверить на nuget.org — `net10.0` TFM |
| SkiaSharp | 3.119.2 | Проверить native assets для net10 |
| SkiaSharp.NativeAssets.Win32 | 3.119.2 | Проверить native assets для net10 |
| Svg.Skia | 5.0.0 | Проверить транзитивные зависимости |
| coverlet.collector | 10.0.1 | Скорее всего совместим |
| Microsoft.NET.Test.Sdk | 18.6.0 | Скорее всего совместим |
| xunit | 2.9.3 | Скорее всего совместим |
| xunit.runner.visualstudio | 3.1.5 | Скорее всего совместим |

**Действие:** Если пакет не поддерживает `net10.0`, обновить до последней версии или найти альтернативу.

### 4.2 Проверка командой

```powershell
dotnet list AiteBar\AiteBar.csproj package --outdated
dotnet list AiteBar.Tests\AiteBar.Tests.csproj package --outdated
```

---

## Этап 5: Ручная проверка P/Invoke (19 деклараций)

### 5.1 Файлы с DllImport

| Файл | Кол-во | Ключевые API |
|------|--------|-------------|
| `AiteBar/NativeMethods.cs` | 14 | `SetWindowsHookEx`, `RegisterHotKey`, `SendInput`, `SetWindowPos`, `SetForegroundWindow` |
| `AiteBar/ActionService.cs` | 1 | `GetAsyncKeyState` |
| `AiteBar/DarkWindow.cs` | 1 | `DwmSetWindowAttribute` |
| `AiteBar/FileSorterWindow.xaml.cs` | 1 | `SHGetKnownFolderPath` (с `MarshalAs`) |
| `AiteBar/QuickNoteWindow.xaml.cs` | 2 | `ReleaseCapture`, `SendMessage` |

### 5.2 Что проверять

- `NativeMethods.cs` содержит `StructLayout(LayoutKind.Explicit)` с `FieldOffset` для union-структур (`INPUTUNION:60-66`) — проверить, что размер и layout структуры не изменились
- `NativeMethods.cs` содержит 8 структур с `[StructLayout]` (`Win32Point:40`, `MSLLHOOKSTRUCT:43`, `INPUT:53`, `KEYBDINPUT:68`, `MOUSEINPUT:78`, `HARDWAREINPUT:89`) — проверить marshalling
- `NativeIntegrationService.cs:11` — delegate `LowLevelMouseProc` хранится как field; .NET 10 может иметь более агрессивный GC — проверить, что delegate не собирается до `UnhookWindowsHookEx`
- Регистрация глобальных hotkey через `RegisterHotKey`/`UnregisterHotKey`
- Mouse hook через `SetWindowsHookEx`/`UnhookWindowsHookEx`
- Позиционирование окон через `SetWindowPos`
- `Marshal.SizeOf<NativeMethods.INPUT>()` (`ActionService.cs:526`), `Marshal.PtrToStructure` (`NativeIntegrationService.cs:51`), `Marshal.PtrToStringUni`/`Marshal.FreeCoTaskMem` (`FileSorterWindow.xaml.cs:315,322`) — всё работает, но при добавлении AOT нужно будет мигрировать

### 5.3 Обратить внимание

- `DllImport` продолжают работать в .NET 10, но генерируют warning CA1401. Миграция на `LibraryImport` — отдельная задача после миграции.
- `Marshal.SizeOf<T>()`, `Marshal.PtrToStructure<T>()`, `Marshal.PtrToStringUni()`, `Marshal.FreeCoTaskMem()` — работают, миграция не требуется.

---

## Этап 6: Ручная проверка UI

### 6.1 Панель (MainWindow)

- [ ] Показ панели
- [ ] Скрытие панели
- [ ] Позиционирование на всех 4 сторонах: Top, Bottom, Left, Right
- [ ] Плавная анимация появления/скрытия
- [ ] Drag-and-drop handle для смены стороны

### 6.2 Контексты

- [ ] Переключение контекстов
- [ ] Перенос кнопок между контекстами
- [ ] Поведение на коротком и длинном контексте

### 6.3 Tray и Hotkeys

- [ ] Tray-значок отображается
- [ ] Контекстное меню tray работает
- [ ] Глобальные hotkey срабатывают
- [ ] Single-instance через Mutex

### 6.4 Quick Note

- [ ] Открытие/закрытие
- [ ] Pin toggle
- [ ] Сохранение размера и позиции

### 6.5 System.Drawing

- [ ] Иконки отображаются корректно (SkiaSharp + System.Drawing interop)
- [ ] Screen picker работает (`ScreenColorPickerWindow` — использует `System.Drawing.Color` для GDI+ pixel access)
- [ ] FolderBrowserDialog открывается

### 6.6 WinForms в WPF

- [ ] `NotifyIcon` в трее работает (создание, disposed при shutdown)
- [ ] `Screen.AllScreens` / `Screen.PrimaryScreen` корректно возвращают мониторы
- [ ] `FolderBrowserDialog` открывается и выбирает папку

### 6.7 AppDomain

- [ ] `AppDomain.CurrentDomain.UnhandledException` handler срабатывает при необработанных исключениях (в .NET 10 поведение для async исключений может отличаться)

---

## Этап 7: Сборка инсталлятора

```powershell
.\installer\Build-Installer.ps1
```

**Проверить:**
- Инсталлятор собирается без ошибок
- `dotnet publish --self-contained` работает с `net10.0-windows`
- Артефакт лежит в `artifacts\installer\`
- Инсталлятор устанавливает и запускает приложение

---

## Этап 8: Обновление документации

### 8.1 Файлы с ссылками на `.NET 8`

| Файл | Строки | Что менять |
|------|--------|-----------|
| `AGENTS.md` | 21, 60, 132 | `.NET 8` → `.NET 10`, `net8.0-windows` → `net10.0-windows` |
| `docs/architecture.md` | 141, 1181, 1186, 1197, 1207 | Таблица технологий + `.NET 8 SDK` → `.NET 10 SDK`, пути `net8.0-windows` → `net10.0-windows` |
| `docs/technical-reference.md` | 7, 11, 12, 280 | `.NET 8` → `.NET 10`, `net8.0-windows` → `net10.0-windows` |

### 8.2 CHANGELOG.md

Добавить запись о миграции:

```markdown
## [Unreleased]

### Changed
- Migrated from .NET 8 to .NET 10 (LTS)
```

---

## Этап 9: Финальная проверка

```powershell
# Сборка
dotnet build AiteBar.sln -c Release

# Тесты
dotnet test AiteBar.Tests\AiteBar.Tests.csproj -c Release

# Инсталлятор
.\installer\Build-Installer.ps1
```

### 9.1 Тесты — на что обратить внимание

- Тесты создают STA-потоки для WPF (`IconConverterWindowLayoutTests:168`, `MainWindowIconConverterOrientationTests:151`, `QuickNoteDocumentHelperTests:55`, `QuickNoteMarkdownTests:164`, `QuickNoteServiceTests:227`, `RuntimeLocalizationInfrastructureTests:134`) — проверить, что xUnit STA thread behavior не изменился
- Тесты используют reflection для вызова приватных методов (`ActionServiceTests:552,585`, `BrowserHelperTests:236`) — работает, но может сломаться при включении trimming/AOT

---

## Откат

Если что-то пошло не так:

```powershell
git checkout global.json AiteBar/AiteBar.csproj AiteBar.Tests/AiteBar.Tests.csproj
Remove-Item "AiteBar\packages.lock.json" -Force
Remove-Item "AiteBar.Tests\packages.lock.json" -Force
dotnet restore AiteBar.sln --force-evaluate
```

---

## Чеклист

### Обязательные
- [ ] .NET 10 SDK установлен
- [ ] Артефакты сборки очищены (obj/, bin/, artifacts/)
- [ ] `global.json` обновлён
- [ ] Оба `.csproj` обновлены
- [ ] Lock-файлы перегенерированы
- [ ] `dotnet build` — 0 ошибок
- [ ] `dotnet test` — все тесты проходят
- [ ] NuGet-пакеты совместимы с net10.0
- [ ] P/Invoke работает (hotkey, mouse hook, окна)
- [ ] INPUTUNION union struct — размер и marshalling корректны
- [ ] Hook delegate не собирается GC (NativeIntegrationService)
- [ ] UI панели работает на всех 4 сторонах
- [ ] Tray и hotkeys работают
- [ ] Quick Note работает
- [ ] System.Drawing interop работает (GDI+ pixel access, icons)
- [ ] WinForms в WPF работает (NotifyIcon, Screen, FolderBrowserDialog)
- [ ] AppDomain.UnhandledException handler срабатывает
- [ ] STA-потоки в тестах работают
- [ ] Инсталлятор собирается
- [ ] Инсталлятор устанавливает и запускает приложение
- [ ] Документация обновлена (AGENTS.md, architecture.md, technical-reference.md)
