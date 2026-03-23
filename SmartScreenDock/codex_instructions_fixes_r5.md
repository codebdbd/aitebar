# Инструкция для Codex: финальные исправления r5

Проект: `SmartScreenDock` (C# / WPF / .NET 8, Windows).  
Исходники находятся в папке `SmartScreenDock/`.  
**Имя директории конфига (`"Codebdbd"`, `"Aite Deck"`) не трогать.**

---

## Задача 1 — Исправить размер структуры `INPUT`

**Файл:** `SmartScreenDock/MainWindow.xaml.cs`  
**Проблема:** Win32 `INPUT` на x64 занимает ровно **28 байт**. Текущая структура содержит `padding` поле в 8 байт, из-за чего `Marshal.SizeOf<INPUT>()` возвращает 32 — `SendInput` читает массив со сдвигом и хоткеи работают некорректно.

Найти текущее объявление `INPUT` вместе с атрибутом:

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct INPUT
{
    public uint type;
    public KEYBDINPUT ki;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] padding;
}
```

Заменить на:

```csharp
// Win32 INPUT на x64 = 28 байт (4 type + 24 KEYBDINPUT с выравниванием IntPtr).
// Size = 28 задаётся явно, чтобы Marshal.SizeOf вернул правильное значение
// независимо от разрядности и не требовал поля-заглушки.
[StructLayout(LayoutKind.Sequential, Size = 28)]
private struct INPUT
{
    public uint type;
    public KEYBDINPUT ki;
}
```

Других изменений в `MainWindow.xaml.cs` не требуется — `KEYBDINPUT`, `SendInput`, `Press` и Hotkey-блок остаются без изменений.

---

## Задача 2 — Исправить словарь `FontAwesomeNameAliases`

**Файл:** `SmartScreenDock/IconPickerWindow.xaml.cs`  
**Проблема:** В словаре 12 неверных кодпоинтов — они либо отсутствуют в шрифте, либо указывают на совершенно другой глиф. Кодпоинты верифицированы напрямую по файлу `Resources/Font Awesome 7 Brands-Regular-400.otf`.

Конкретные ошибки в текущем словаре:

| Кодпоинт в коде | Что там реально | Должно быть |
|---|---|---|
| `0xF392` → `docker` | глиф `discord` | нужен `0xF395` для docker |
| `0xF3A4` → `node` | глиф `freebsd` | нужен `0xF3D3` / `0xF419` для node |
| `0xF3B9` → `aws` | глиф `square-js` | нужен `0xF375` для aws |
| `0xF3EF` → `yandex` | глиф `slack` | нужен `0xF413` для yandex |
| `0xF3C5` → `telegram` | не существует в шрифте | нужен `0xF2C6` / `0xF3FE` |
| `0xF3F0` → `yandex-international` | не существует | нужен `0xF414` |
| `0xF1D8` → `paper-plane` | не существует | удалить (глифа нет) |
| `0xE458` → `twitch` | не существует | нужен `0xF1E8` |
| `0xE469` → `reddit` | не существует | нужен `0xF1A1` / `0xF281` |
| `0xE488` → `vk` | не существует | нужен `0xF189` |
| `0xE4C8` → `odnoklassniki` | не существует | нужен `0xF263` |
| `0xE4D7` → `spotify` | не существует | нужен `0xF1BC` |
| `0xE4E6` → `notion` | не существует | нужен `0xE7D9` ✅ |
| `0xE4F6` → `discord` | не существует | нужен `0xF392` |
| `0xE512` → `figma` | не существует | нужен `0xF799` |
| `0xE5D2` → `slack` | не существует | нужен `0xF198` / `0xF3EF` |

Найти весь словарь `FontAwesomeNameAliases` целиком (от `private static readonly Dictionary` до закрывающей `};`) и заменить на:

```csharp
private static readonly Dictionary<int, string[]> FontAwesomeNameAliases = new()
{
    // Все кодпоинты верифицированы по файлу шрифта Font Awesome 7 Brands-Regular-400.otf

    // Twitter / X
    [0xF099] = new[] { "twitter" },
    [0xE61B] = new[] { "twitter", "x", "x-twitter" },

    // Facebook / Meta
    [0xF09A] = new[] { "facebook", "meta" },

    // GitHub
    [0xF09B] = new[] { "github" },
    [0xF113] = new[] { "github-alt" },

    // GitLab
    [0xF296] = new[] { "gitlab" },

    // LinkedIn
    [0xF0E1] = new[] { "linkedin", "linkedin-in" },

    // Instagram
    [0xF16D] = new[] { "instagram" },

    // YouTube
    [0xF167] = new[] { "youtube" },

    // Google
    [0xF1A0] = new[] { "google" },

    // Apple
    [0xF179] = new[] { "apple", "ios" },

    // Windows / Microsoft
    [0xF17A] = new[] { "windows", "microsoft" },

    // WhatsApp
    [0xF232] = new[] { "whatsapp" },

    // Telegram
    [0xF2C6] = new[] { "telegram" },
    [0xF3FE] = new[] { "telegram" },

    // Discord
    [0xF392] = new[] { "discord" },

    // Docker
    [0xF395] = new[] { "docker" },

    // AWS
    [0xF375] = new[] { "aws", "amazon-web-services" },

    // Node.js
    [0xF3D3] = new[] { "node", "node-js" },
    [0xF419] = new[] { "node" },

    // Git
    [0xF841] = new[] { "git", "git-alt" },

    // Slack
    [0xF198] = new[] { "slack" },
    [0xF3EF] = new[] { "slack" },

    // Yandex
    [0xF413] = new[] { "yandex" },
    [0xF414] = new[] { "yandex-international" },

    // Twitch
    [0xF1E8] = new[] { "twitch" },

    // Reddit
    [0xF1A1] = new[] { "reddit" },
    [0xF281] = new[] { "reddit-alien" },

    // Spotify
    [0xF1BC] = new[] { "spotify" },

    // VK
    [0xF189] = new[] { "vk", "vkontakte" },

    // Odnoklassniki
    [0xF263] = new[] { "odnoklassniki", "ok" },

    // Figma
    [0xF799] = new[] { "figma" },

    // Notion
    [0xE7D9] = new[] { "notion" },

    // Bluesky
    [0xE671] = new[] { "bluesky" },

    // Threads
    [0xE618] = new[] { "threads" },

    // TikTok
    [0xE07B] = new[] { "tiktok" },
};
```

---

## Итоговый чеклист

| # | Файл | Действие |
|---|------|----------|
| 1 | `MainWindow.xaml.cs` | Убрать `padding` поле из `INPUT`, добавить `Size = 28` в `StructLayout` |
| 2 | `IconPickerWindow.xaml.cs` | Заменить `FontAwesomeNameAliases` на верифицированный словарь (36 записей) |

После изменений собрать проект в конфигурации `Debug` и убедиться, что нет ошибок компиляции.
