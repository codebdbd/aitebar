# Что нужно исправить

> Актуально: 2026-06-14 | Версия: 1.8.0

---

## Блокеры релиза

### 1. SECURITY.md — шаблон GitHub

**Файл:** `SECURITY.md`

Содержит версии `5.1.x`, `5.0.x`, `4.0.x` и текст "Tell them where to go..." — не относится к AiteBar.

**Исправление:** Переписать:

```markdown
# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.8.x   | :white_check_mark: |
| < 1.8   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability, report it via
[GitHub Security Advisories](https://github.com/codebdbd/aitebar/security/advisories/new).

We acknowledge receipt within 48 hours and provide a fix timeline.
```

---

## Документация

### 2. CI/CD ссылки не соответствуют реальности

**Файлы:** `README.md:125-133`, `AGENTS.md:173-175`, `docs/architecture.md:1219-1226`

Описывают `.github/workflows/build-test.yml`, `codeql.yml`, `release.yml`, `dependabot.yml` — **ни один не существует**.

**Исправление (вариант A — удалить ссылки):**

| Файл | Что сделать |
|------|-------------|
| `README.md:125-133` | Удалить секцию "Release Quality" |
| `AGENTS.md:173-175` | Удалить строки про `.github/workflows/*` |
| `docs/architecture.md:1219-1226` | Заменить §20 на "CI/CD не настроен" |
| `docs/architecture.md:1355` | Оставить "Нет CI/CD" (уже верно) |
| `docs/architecture.md:1361` | Оставить "Настроить CI/CD" как будущую задачу |

**Исправление (вариант B — создать workflow'и):**

Создать `.github/workflows/build-test.yml` и `.github/dependabot.yml` по описанию из `AGENTS.md`.

### 3. Противоречие в architecture.md §20 vs §25

**Файл:** `docs/architecture.md`

| Строка | Утверждение |
|--------|-------------|
| 1220 | "CI/CD настроен через GitHub Actions" |
| 1355 | "Нет CI/CD" |

**Исправление:** Привести §20 к реальности (см. п. 2).

### 4. Устаревшие отчёты в docs/

**Файлы:**
- `docs/ISSUES_REPORT.md` — версия 1.7.9, дата 2025-06-14
- `docs/WHAT_TO_UPDATE.md` — версия 1.7.9, дата 2025-06-14
- `docs/PLAN_UNIFIED_BUTTONS.md` — версия 1.7.9
- `docs/REMAINING_FIXES.md` — дата 2025-06-14

**Исправление:** Удалить или переместить в `docs/archive/`. Эти отчёты описывают проблемы предыдущей версии.

### 5. Дублирование AGENTS.md

**Файлы:**
- `./AGENTS.md` — 214 строк, полный Agent Handbook + UI Contract
- `AiteBar/AGENTS.md` — 14 строк, только UI Contract

UI Contract дублируется в обоих файлах.

**Исправление:** Удалить `AiteBar/AGENTS.md`. Весь контент уже в корневом `AGENTS.md`.

### 6. Дублирование описаний

Одна информация размазана по 4+ файлам:
- Архитектура: `architecture.md`, `technical-reference.md`, `README.md`, `AGENTS.md`
- Команды сборки: `AGENTS.md`, `technical-reference.md`
- Стек: `AGENTS.md`, `architecture.md`, `technical-reference.md`

**Исправление:** Не блокирует релиз. Постепенно привести к единому источнику правды.

---

## Код

### 7. Хардкод Chrome в поисковом действии

**Файл:** `AiteBar>ActionService.cs:314`

```csharp
// Сейчас:
ProcessStartInfo psi = new ProcessStartInfo(BrowserHelper.GetExecutablePath(BrowserType.Chrome))
```

Если Chrome не установлен — поиск сломается.

**Исправление:**

```csharp
// Вариант 1 — системный браузер:
ProcessStartInfo psi = new ProcessStartInfo
{
    FileName = $"https://www.google.com/search?q={Uri.EscapeDataString(text)}",
    UseShellExecute = true
};

// Вариант 2 — fallback на Edge:
var path = BrowserHelper.GetExecutablePath(BrowserType.Chrome)
    ?? BrowserHelper.GetExecutablePath(BrowserType.Edge);
ProcessStartInfo psi = new ProcessStartInfo(path) { ... };
```

### 8. Глобальная блокировка параллельности тестов

**Файл:** `AiteBar.Tests/AssemblyInfo.cs`

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Блокирует **все** тесты, включая чисто логические.

**Исправление:**

1. Удалить строку из `AssemblyInfo.cs`
2. Добавить `[Collection("WpfTestCollection")]` только на WPF-тестовые классы:
   - `IconConverterWindowLayoutTests`
   - `MainWindowIconConverterOrientationTests`
   - `QuickNoteMarkdownTests`
   - и др. (классы, использующие `Dispatcher`)

---

## Низкий приоритет

### 9. TODO-плейсхолдеры в USER_MANUAL.md

**Файл:** `docs/USER_MANUAL.md`

8 мест с TODO для скриншотов (строки ~50, ~65, ~124, ~226, ~254, ~278, ~336, ~360).

**Исправление:** Сделать скриншоты и вставить перед публикацией руководства.

### 10. Task.Delay(1100) в тестах

**Файл:** `AiteBar.Tests/QuickNoteServiceTests.cs`

**Исправление:** Заменить на polling с таймаутом (см. CODE_REVIEW_REPORT.md п. 4.2).

### 11. Side effects в TelemetryServiceTests

**Файл:** `AiteBar.Tests/TelemetryServiceTests.cs`

Хелпер `WithSettingsFile` пишет в реальный `%APPDATA%`.

**Исправление:** Использовать temp-директорию (см. CODE_REVIEW_REPORT.md п. 4.3).

---

## Итого

| Приоритет | Пунктов | Блокер релиза |
|-----------|---------|---------------|
| Критический | 1 | SECURITY.md |
| Высокий | 3 | CI/CD ссылки, противоречие architecture.md, устаревшие отчёты |
| Средний | 2 | Хардкод Chrome, параллельность тестов |
| Низкий | 3 | TODO скриншоты, flaky tests, side effects |
