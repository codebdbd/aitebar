# Чеклист перед релизом: миграция на .NET 10

## Статус миграции

Проектные файлы (`global.json`, `AiteBar.csproj`, `AiteBar.Tests.csproj`, `packages.lock.json`) обновлены до .NET 10 корректно. Код совместим с .NET 10, устаревших API нет. Остались косметические ошибки в UI и документации.

---

## Критично (исправить перед релизом)

### 1. AboutWindow.xaml — в UI отображается ".NET 8"

**Файл**: `AiteBar/AboutWindow.xaml`, строка 104

Текст `Text=".NET 8"` виден пользователям в окне "О программе". Нужно заменить на `.NET 10`.

### 2. LocalizationServiceTests — тест упадёт после исправления UI

**Файл**: `AiteBar.Tests/LocalizationServiceTests.cs`, строка 203

Список `allowedTechnicalText` содержит `".NET 8"`. После замены в `AboutWindow.xaml` на `.NET 10` тест начнёт падать, еслиallowlist не обновить.

---

## Средний приоритет (документация)

### 3. docs/architecture.md — 4 устаревшие ссылки

| Строка | Было | Нужно |
|--------|------|-------|
| 141 | `\| .NET 8 \| Платформа приложения \|` | `.NET 10` |
| 1181 | `.NET 8 SDK и WPF` | `.NET 10 SDK` |
| 1186 | `- .NET 8 SDK` | `- .NET 10 SDK` |
| 1197 | `bin\Release\net8.0-windows\` | `net10.0-windows` |

### 4. docs/technical-reference.md — 2 устаревшие ссылки

| Строка | Было | Нужно |
|--------|------|-------|
| 7 | `на .NET 8 и WPF` | `на .NET 10` |
| 280 | `net8.0-windows\AiteBar.Tests.dll` | `net10.0-windows` |

---

## Информационные замечания (не блокируют релиз)

### 5. .github/ — отсутствует директория

В `AGENTS.md` описаны `build-test.yml`, `release.yml`, `codeql.yml`, `dependabot.yml`, но директория `.github/` не существует. CI/CD не настроен. Это не связано с миграцией, но документация не соответствует реальности.

### 6. Файлы миграции — ссылки на .NET 8 уместны

Следующие файлы содержат ссылки на `.NET 8` **целенаправленно** (описывают миграцию "откуда"):

- `docs/MIGRATION_NET10.md` — план миграции
- `docs/AITEBAR_NET10_AGENT_INSTRUCTION.md` — инструкция агента
- `CHANGELOG.md` строки 13–14 — запись о миграции в `[Unreleased]`

Исправлять **не нужно**.

---

## Что уже работает корректно

| Область | Статус |
|---------|--------|
| `global.json` — SDK `10.0.301` | OK |
| `AiteBar.csproj` — TFM `net10.0-windows` | OK |
| `AiteBar.Tests.csproj` — TFM `net10.0-windows` | OK |
| `packages.lock.json` — пересоздан для `net10.0-windows7.0` | OK |
| `AGENTS.md` — ссылки на .NET 10 | OK |
| `README.md` — нет ссылок на версию .NET | OK |
| `AssemblyInfo.cs` — версии синхронизированы (1.8.0) | OK |
| `installer/Build-Installer.ps1` — нет hardcode TFM | OK |
| `installer/AiteBar.iss` — нет ссылок на .NET | OK |
| `Directory.Build.props` — нет TFM | OK |
| `.editorconfig` — нет конфликтов | OK |
| Устаревшие API (BinaryFormatter и т.д.) | Не используются |
| `#if` директивы с net8.0 | Отсутствуют |
| NuGet-пакеты (Sentry, SkiaSharp, Svg.Skia) | Совместимы |
| P/Invoke (Win32 API) | Совместимы |

---

## Порядок действий

1. Исправить `AiteBar/AboutWindow.xaml:104` — `.NET 8` → `.NET 10`
2. Исправить `AiteBar.Tests/LocalizationServiceTests.cs:203` — `.NET 8` → `.NET 10`
3. Исправить `docs/architecture.md` — 4 строки (141, 1181, 1186, 1197)
4. Исправить `docs/technical-reference.md` — 2 строки (7, 280)
5. Собрать `Release`: `dotnet build .\AiteBar.sln -c Release`
6. Прогнать тесты: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`
7. (Опционально) Собрать инсталлятор: `.\installer\Build-Installer.ps1`

**Итого: 8 строк к исправлению в 4 файлах.**
