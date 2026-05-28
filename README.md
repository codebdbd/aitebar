# AiteBar

---

## English

AiteBar — Your Personal Command Center for Windows. Turn the edge of your screen into a workflow hub. Organize your AI tools, work sites, browser profiles, projects, folders, scripts and system utilities in one compact place.

### Features

#### Workflow Organization
- **Up to 8 context panels**: Separate your tools by tasks (Work, AI, Personal, Scripts, etc.)
- **Import/export panels**: Transfer ready-made button sets between computers using `.aitebarpanel`

#### Web & Browser Tools
- **Browser and profile support**: Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi, Firefox
- **Launch modes**: App Mode, Incognito/Private, Fullscreen
- **Profile rotation**: Automatic browser profile switching

#### Quick Actions
- **Hidden edge panel**: Appears when hovering over the screen edge
- **Global hotkeys**: Quick access to the panel and actions
- **Built-in tools**: Screenshot, screen recording, calculator, file explorer, downloads, color picker, Quick Note
- **Drag-and-drop**: Add files, folders, .url shortcuts and links by dragging

#### Action Types
- Open URL in selected browser
- Launch programs and shortcuts (.exe, .lnk)
- Open files and folders
- Run scripts (.bat, .cmd, .ps1, .py)
- Execute console commands (via cmd.exe with confirmation)
- Send keyboard shortcuts

#### Customization
- **Icon library**: Material Symbols, Fluent System Icons, Font Awesome Brands
- **Color settings**: Color palette and HEX color for buttons
- **Local storage**: Settings and data in `%AppData%\Codebdbd\Aite Bar`

### Installation and Launch
1. Download the release from the corresponding section.
2. Run `AiteBar-Setup.exe`.
3. Install the program like a regular Windows application.
4. After installation, launch `AiteBar` from the Start menu. The program will appear in the Windows system tray.
5. To check for updates, use the `Check for updates` item in the tray menu or the `About` window.

### Usage
- **Button settings**: Right-click on any button to edit it.
- **Add new button**: Press the `+` button at the end of any menu block.
- **Delete button**: Right-click the button and select `Delete`.
- **Import/export panel**: Use the panel context menu or tray menu to import into the current panel and export the current panel.

### Documentation
- [User Manual](USER_MANUAL.md)
- [Features Map](docs/functions.md)
- [Architecture](docs/architecture.md)
- [Pre-release Audit](docs/release-audit.md)

### Requirements
- OS: Windows 10 / Windows 11

### Data Storage
- Configuration and user data are stored in `C:\Users\user\AppData\Roaming\Codebdbd\Aite Bar`

---

## Русский

AiteBar — персональный Workflow Hub для Windows, превращающий край экрана в командный центр. Собери AI-сервисы, рабочие сайты, браузерные профили, проекты, папки, скрипты и системные инструменты в одном компактном месте.

### Особенности

#### Организация workflow
- **До 8 панелей-контекстов**: Разделяй инструменты по задачам (Работа, AI, Личное, Скрипты и т.д.)
- **Импорт/экспорт панелей**: Переноси готовые наборы кнопок между компьютерами через `.aitebarpanel`

#### Веб-инструменты и браузеры
- **Поддержка браузеров и профилей**: Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi, Firefox
- **Режимы запуска**: App Mode, Incognito/Private, Fullscreen
- **Ротация профилей**: Автоматическое переключение браузерных профилей

#### Быстрые действия
- **Скрытая edge-панель**: Появляется при наведении на край экрана
- **Глобальные горячие клавиши**: Быстрый доступ к панели и действиям
- **Встроенные инструменты**: Скриншот, запись экрана, калькулятор, проводник, загрузки, пипетка цвета, Quick Note
- **Drag-and-drop**: Добавляй файлы, папки, .url и ссылки перетаскиванием

#### Типы действий
- Открыть URL в выбранном браузере
- Запустить программы и ярлыки (.exe, .lnk)
- Открыть файлы и папки
- Запустить скрипты (.bat, .cmd, .ps1, .py)
- Выполнить консольные команды (через cmd.exe с подтверждением)
- Отправить сочетания клавиш

#### Настройка
- **Библиотека иконок**: Material Symbols, Fluent System Icons, Font Awesome Brands
- **Настройка цвета**: Палитра и HEX-цвет для кнопок
- **Локальное хранение**: Настройки и данные в `%AppData%\Codebdbd\Aite Bar`

### Установка и запуск
1. Загрузите релиз из соответствующего раздела.
2. Запустите `AiteBar-Setup.exe`.
3. Установите программу как обычное Windows-приложение.
4. После установки запустите `AiteBar` из меню Пуск. Программа появится в системном трее Windows.
5. Для проверки новых версий используйте пункт `Check for updates` в tray-меню или окне `About`.

### Управление
- **Настройка кнопок**: Кликните ПКМ (правой кнопкой мыши) по любой кнопке для её редактирования.
- **Добавление новой**: Нажмите кнопку `+` в конце любого блока меню.
- **Удаление**: Кликните ПКМ по кнопке и выберите `Удалить`.
- **Импорт/экспорт панели**: Используйте контекстное меню панели или меню в tray для импорта в текущую панель и экспорта текущей панели.

### Документация
- [Руководство пользователя](USER_MANUAL.md)
- [Карта функций](docs/functions.md)
- [Архитектура](docs/architecture.md)
- [Предрелизный аудит](docs/release-audit.md)

### Требования
- ОС: Windows 10 / Windows 11

### Хранение данных
- Конфигурация и пользовательские данные хранятся в `C:\Users\user\AppData\Roaming\Codebdbd\Aite Bar`

---

## Release Quality
- CI: GitHub Actions workflow `.github/workflows/build-test.yml` builds `Release`, runs tests, publishes coverage summary and saves coverage artifact on every push/PR to `master` and `main`.
- Static analysis: `.github/workflows/codeql.yml` runs CodeQL security-and-quality analysis for C#.
- Dependency updates: `.github/dependabot.yml` checks NuGet and GitHub Actions dependencies weekly.
- Release: `vX.Y.Z` tag must match `<Version>` in `AiteBar/AiteBar.csproj`; `.github/workflows/release.yml` builds installer and attaches `artifacts/installer/*.exe` to GitHub Release.
- Release guardrails: release workflow can be run manually as dry-run, it requires `## [X.Y.Z]` section in `CHANGELOG.md`, exactly one non-empty installer artifact and publishes `SHA256SUMS.txt`.
- Code signing: installer is signed automatically if GitHub Secrets have `WINDOWS_SIGNING_CERT_BASE64` and `WINDOWS_SIGNING_CERT_PASSWORD`. Without these secrets, build remains unsigned.
- Crash reporting: [Sentry](docs/SENTRY_SETUP.md) is used for error monitoring, disabled by default. Can be enabled via environment variables (`AITEBAR_SENTRY_DSN`/`SENTRY_DSN`) or via settings file. No PII is sent without explicit consent.
- Updates: built-in update check reads latest release from `https://github.com/codebdbd/aitebar/releases`, validates GitHub URL before opening and offers to open release page if newer installer is found. Automatic installation is not performed.
