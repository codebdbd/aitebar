# Настройка Monitoring через Sentry

AiteBar поддерживает мониторинг ошибок и производительности через **Sentry**. Мониторинг по умолчанию отключен для защиты конфиденциальности пользователей.

## Способы включения

### 1. Через переменные окружения (рекомендуется для production)

Установите следующие переменные окружения:

| Переменная | Описание | Значение по умолчанию |
|------------|----------|----------------------|
| `AITEBAR_SENTRY_DSN` | DSN вашего проекта Sentry | — |
| `AITEBAR_ENVIRONMENT` | Окружение (production, staging, development) | `production` |
| `AITEBAR_TRACES_SAMPLE_RATE` | Частота сбора performance traces (0.0 - 1.0) | `0.0` |
| `AITEBAR_SEND_PII` | Отправлять ли Personally Identifiable Information | `false` |

Пример (PowerShell):
```powershell
$env:AITEBAR_SENTRY_DSN = "https://examplePublicKey@o0.ingest.sentry.io/0"
$env:AITEBAR_ENVIRONMENT = "production"
```

Пример (CMD):
```cmd
set AITEBAR_SENTRY_DSN=https://examplePublicKey@o0.ingest.sentry.io/0
set AITEBAR_ENVIRONMENT=production
```

### 2. Через файл настроек (для тестирования)

Отредактируйте файл `%APPDATA%\Codebdbd\Aite Bar\settings.json` и добавьте секцию `Sentry`:

```json
{
  "Sentry": {
    "IsEnabled": true,
    "Dsn": "https://examplePublicKey@o0.ingest.sentry.io/0",
    "Environment": "production",
    "TracesSampleRate": 0.0,
    "SendDefaultPii": false
  },
  "GlobalHotkeyAlt": true,
  "GlobalHotkeyKey": "D4"
}
```

## Приоритет конфигурации

Если заданы и переменные окружения, и файл настроек:
1. Сначала проверяются переменные окружения (`AITEBAR_SENTRY_DSN`, `SENTRY_DSN`)
2. Если не найдено — проверяется файл настроек
3. Если DSN не найден — мониторинг остается отключенным

## Как получить DSN из Sentry

1. Зарегистрируйтесь на [sentry.io](https://sentry.io)
2. Создайте новый проект (Platform: .NET)
3. Скопируйте DSN из настроек проекта

## Безопасность и конфиденциальность

- По умолчанию **не отправляется** никакая личная информация пользователя
- `SendDefaultPii` всегда `false`, если явно не включено
- Только ошибки и базовые метрики (версия приложения, окружение)
- Можно настроить rate limiting в Sentry для снижения нагрузки
