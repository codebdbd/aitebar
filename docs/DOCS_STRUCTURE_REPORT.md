# Отчёт: Проблемы документации и структуры проекта

> Дата: 2026-06-14
> Актуальная версия: 1.8.0

---

## Содержание

1. [Битые ссылки](#1-битые-ссылки)
2. [Устаревшие версии в документах](#2-устаревшие-версии-в-документах)
3. [Противоречия между документами](#3-противоречия-между-документами)
4. [Призрачные CI/CD ссылки](#4-призрачные-cicd-ссылки)
5. [Мусорные и дублирующие файлы в docs/](#5-мусорные-и-дублирующие-файлы-в-docs)
6. [Проблемы структуры проекта](#6-проблемы-структуры-проекта)
7. [Дублирование AGENTS.md](#7-дублирование-agentsmd)
8. [Итоговая таблица](#8-итоговая-таблица)

---

## 1. Битые ссылки

### В README.md

| Строка | Ссылка | Статус |
|--------|--------|--------|
| `README.md:56` | `[Pre-release Audit](docs/release-audit.md)` | **Файл не существует** |
| `README.md:117` | `[Предрелизный аудит](docs/release-audit.md)` | **Файл не существует** |

### В USER_MANUAL.md

| Строка | Ссылка | Статус |
|--------|--------|--------|
| `USER_MANUAL.md:5` | `[docs/documentation-qa-checklist.md](docs/documentation-qa-checklist.md)` | **Файл не существует** |

### В docs/README.md

| Строка | Ссылка | Статус |
|--------|--------|--------|
| `docs/README.md:10` | `[internal](internal)` | **Директория `docs/internal/` не существует** |

---

## 2. Устаревшие версии в документах

Текущая версия приложения: **1.8.0** (csproj + AssemblyInfo).

| Файл | Указанная версия | Проблема |
|------|-----------------|----------|
| `docs/ISSUES_REPORT.md:4` | `1.7.9` | Отчёт датирован 2025-06-14, описывает проблемы v1.7.9 |
| `docs/WHAT_TO_UPDATE.md:4` | `1.7.9` | Тот же отчёт, та же дата |
| `docs/PLAN_UNIFIED_BUTTONS.md:4` | `1.7.9` | План объединения кнопок |
| `docs/REMAINING_FIXES.md:4` | — | Отчёт об исправлениях, дата 2025-06-14 |
| `docs/CODE_REVIEW_REPORT.md` | `1.7.9` | Наш отчёт ревью |
| `installer/AiteBar.iss` (fallback) | `1.7.9` | Fallback версия в Inno Setup скрипте |

**Все отчёты и планы в docs/ написаны для v1.7.9 и не обновлены для v1.8.0.**

---

## 3. Противоречия между документами

### 3.1 CI/CD — жив или мёртв?

| Файл | Утверждение |
|------|-------------|
| `README.md:127-134` | "CI: GitHub Actions workflow `.github/workflows/build-test.yml` builds Release..." |
| `docs/architecture.md:1219-1226` | "CI/CD настроен через GitHub Actions" + описание 4 workflow'ов |
| `AGENTS.md` (секция "CI и качество") | Описывает 4 workflow'а как работающие |
| `docs/architecture.md:1355` | **"Нет CI/CD"** |
| `docs/architecture.md:1367` | **"Настроить CI/CD"** как будущая задача |
| `.github/` директория | **Не существует** |

Одни документы описывают CI/CD как работающий, другие пишут что его нет. Реальность: `.github/` не существует.

### 3.2 Дублирование описаний архитектуры

Одна и та же информация описана в 4+ местах:

| Тема | Где описана |
|------|-------------|
| Архитектура приложения | `architecture.md`, `technical-reference.md`, `README.md`, `AGENTS.md` |
| Команды сборки/тестов | `AGENTS.md`, `technical-reference.md:263-287` |
| Стек технологий | `AGENTS.md`, `architecture.md`, `technical-reference.md` |
| Типы действий | `README.md`, `USER_MANUAL.md`, `technical-reference.md:132-143` |
| Константы анимаций | `technical-reference.md:120-131`, `Constants.cs` (код) |
| Локальные данные | `technical-reference.md:34-49`, `README.md:61-62` |

Нет единого источника правды. При обновлении одной секции — другие остаются устаревшими.

---

## 4. Призрачные CI/CD ссылки

Ссылки на несуществующие файлы:

| Файл | Строка | Целевой файл | Статус |
|------|--------|--------------|--------|
| `README.md:128` | `.github/workflows/build-test.yml` | Не существует | Призрак |
| `README.md:129` | `.github/workflows/codeql.yml` | Не существует | Призрак |
| `README.md:130` | `.github/dependabot.yml` | Не существует | Призрак |
| `README.md:131` | `.github/workflows/release.yml` | Не существует | Призрак |
| `AGENTS.md` (CI секция) | Те же 4 файла | Не существует | Призрак |
| `docs/architecture.md:1222-1226` | Те же 4 файла | Не существует | Призрак |

---

## 5. Мусорные и дублирующие файлы в docs/

| Файл | Назначение | Актуален? | Проблема |
|------|------------|-----------|----------|
| `docs/ISSUES_REPORT.md` | Отчёт проблем v1.7.9 | **Нет** | Версия 1.7.9, дата 2025-06-14. Часть проблем исправлена в 1.8.0 (P4 async flush, P9 backup atomicity, P5 layout duplication) |
| `docs/REMAINING_FIXES.md` | Оставшиеся исправления | **Частично** | Описывает P1 (MainWindow god-class) как не исправленный — верно. Но ссылается на старые номера строк |
| `docs/WHAT_TO_UPDATE.md` | Что обновить | **Частично** | D1 (CHANGELOG) — исправлено. D2-D5 — возможно не актуальны |
| `docs/PLAN_UNIFIED_BUTTONS.md` | План объединения кнопок | **Неизвестно** | План для v1.7.9. Неясно, реализован ли |
| `docs/CODE_REVIEW_REPORT.md` | Наш отчёт ревью | **Да** | Актуален, но описывает v1.7.9 |
| `docs/README.md` | Оглавление docs/ | **Нет** | Ссылается на несуществующую `internal/` директорию |
| `docs/functions.md` | Карта функций (91KB) | **Да** | Крупный файл, актуален |
| `docs/UTILITIES.md` | Как добавлять утилиты | **Да** | Актуален |
| `docs/SENTRY_SETUP.md` | Настройка Sentry | **Да** | Актуален |

**Итого: 5 из 12 файлов в docs/ — устаревшие отчёты/планы предыдущих версий.**

---

## 6. Проблемы структуры проекта

### 6.1 Два AGENTS.md

| Файл | Содержимое |
|------|------------|
| `./AGENTS.md` (корень) | Полный Agent Handbook: стек, команды, правила, чеклист, UI Contract, CI/CD |
| `AiteBar/AGENTS.md` | Только UI Contract (Visual Style + Locked Layout Invariants, 14 строк) |

**Проблема:** Два файла с одинаковым именем, разным содержимым. Agent из корня не видит `AiteBar/AGENTS.md` автоматически (или наоборот). Неясно, какой из них является основным.

**Как исправить:** Объединить в один `./AGENTS.md`, либо переименовать `AiteBar/AGENTS.md` → `AiteBar/UI_CONTRACT.md`.

### 6.2 Дублирование версии

Версия `1.8.0` указана в 4 местах:

| Файл | Поле |
|------|------|
| `AiteBar/AiteBar.csproj:16` | `<Version>1.8.0</Version>` |
| `AiteBar/AiteBar.csproj:17` | `<AssemblyVersion>1.8.0.0</AssemblyVersion>` |
| `AiteBar/AiteBar.csproj:18` | `<FileVersion>1.8.0.0</FileVersion>` |
| `AiteBar/AssemblyInfo.cs:11` | `[assembly: AssemblyVersion("1.8.0.0")]` |
| `AiteBar/AssemblyInfo.cs:12` | `[assembly: AssemblyFileVersion("1.8.0.0")]` |
| `AiteBar/AssemblyInfo.cs:13` | `[assembly: AssemblyInformationalVersion("1.8.0")]` |
| `installer/AiteBar.iss` | `#define AppVersion "1.7.9"` (fallback) |
| `CHANGELOG.md:10` | `## [1.8.0] - 2026-06-14` |

`AiteBar.iss` содержит fallback `1.7.9` — при сборке через `Build-Installer.ps1` он перезаписывается, но при прямом запуске Inno Setup будет старая версия.

### 6.3 Отсутствующая директория docs/internal

`docs/README.md:10` ссылается на `[internal](internal)`, но `docs/internal/` не существует.

### 6.4 Мусорные артефакты

| Путь | Содержимое | Нужно ли? |
|------|------------|-----------|
| `artifacts/installer/` | Собранные инсталляторы | Да (но не應該 в git) |
| `artifacts/publish/` | Publish-артефакты | Да (но не應該 в git) |
| `AiteBar.Tests/TestResults/` | Результаты тестов | Нет (артефакт запуска) |

Проверить `.gitignore`: исключены ли `artifacts/`, `bin/`, `obj/`, `TestResults/`?

### 6.5 Тестовая директория TestResults

`AiteBar.Tests/TestResults/` — артефакт запуска тестов, не должен быть в контроле версий.

---

## 7. Дублирование AGENTS.md

### Корневой `AGENTS.md` (214 строк)

Содержит:
- Описание проекта
- Стек и архитектура
- Ключевые команды (сборка, тесты, installer)
- Правила внесения изменений
- Чеклист самопроверки
- Release, CI и документация
- UI Contract (Visual Style + Locked Layout)

### `AiteBar/AGENTS.md` (14 строк)

Содержит:
- Visual Style Guidelines (5 пунктов)
- Locked Layout Invariants (2 пункта)

**Проблема:** UI Contract дублируется — в корневом `AGENTS.md` есть секция "UI Contract", а в `AiteBar/AGENTS.md` — отдельный файл с тем же содержимым.

---

## 8. Итоговая таблица

| # | Проблема | Файл(ы) | Критичность | Время на исправление |
|---|----------|----------|-------------|---------------------|
| 1 | Битая ссылка `release-audit.md` | `README.md:56,117` | Высокий | 2 мин (удалить строки) |
| 2 | Битая ссылка `documentation-qa-checklist.md` | `USER_MANUAL.md:5` | Высокий | 1 мин (удалить часть предложения) |
| 3 | Битая ссылка `internal/` | `docs/README.md:10` | Средний | 1 мин (удалить строку) |
| 4 | Устаревшие версии 1.7.9 | `ISSUES_REPORT.md`, `WHAT_TO_UPDATE.md`, `PLAN_UNIFIED_BUTTONS.md`, `CODE_REVIEW_REPORT.md` | Средний | 30 мин (обновить или удалить) |
| 5 | Противоречие CI/CD | `README.md`, `architecture.md`, `AGENTS.md` | Высокий | 10 мин |
| 6 | Дублирование AGENTS.md | `./AGENTS.md`, `AiteBar/AGENTS.md` | Средний | 5 мин (объединить) |
| 7 | Fallback версия в .iss | `installer/AiteBar.iss` | Низкий | 1 мин |
| 8 | Мусорные файлы в docs/ | 5 устаревших отчётов | Низкий | 10 мин (архивировать/удалить) |
| 9 | TestResults в контроле | `AiteBar.Tests/TestResults/` | Низкий | 1 мин (добавить в .gitignore) |
| 10 | Дублирование описаний | 4+ файла с одним контентом | Информационный | — |

---

## Рекомендации

### Немедленно (перед релизом)

1. **Удалить битые ссылки** из `README.md` и `USER_MANUAL.md`
2. **Привести CI/CD описание к реальности** — либо создать workflow'ы, либо удалить/пометить как planned все ссылки
3. **Обновить fallback версию** в `installer/AiteBar.iss` до `1.8.0`

### В ближайшее время

4. **Объединить AGENTS.md** в один файл
5. **Удалить или архивировать** устаревшие отчёты (`ISSUES_REPORT.md`, `WHAT_TO_UPDATE.md`, `PLAN_UNIFIED_BUTTONS.md`, `REMAINING_FIXES.md`, `CODE_REVIEW_REPORT.md`)
6. **Исправить `docs/README.md`** — убрать ссылку на `internal/`

### Постепенно

7. **Ввести единый источник правды** для каждой темы (архитектура, команды, стек)
8. **Добавить `TestResults/` в `.gitignore`**
9. **Обновить `artifacts/`** — убедиться что publish/installer артефакты не в git
