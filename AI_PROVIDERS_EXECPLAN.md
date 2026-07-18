# Реализовать безопасный шлюз бесплатных AI-провайдеров

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

Этот документ ведётся в соответствии с `PLANS.md` в корне репозитория. Он должен оставаться самодостаточным: разработчику достаточно текущего рабочего дерева и этого файла, чтобы продолжить реализацию без истории переписки.

## Purpose / Big Picture

После изменения пользователь сможет открыть раздел «Нейросети» в настройках AiteBar, добавить несколько API-подключений OpenRouter, Cerebras, Google Gemini, Groq, GitHub Models или Mistral, безопасно сохранить ключи средствами Windows и проверить каждое подключение. Подключения одного провайдера будут иметь приоритет и обозначение общей квоты, чтобы будущие утилиты могли переключиться на следующий независимый аккаунт или проект после исчерпания лимита, а затем перейти к следующему провайдеру. Платный fallback не включается: первый выпуск предназначен только для бесплатных тарифов и никогда не должен незаметно создавать расходы.

Работу можно увидеть без создания отдельной AI-утилиты: в настройках появляется новый раздел со списком подключений, кнопками добавления, удаления и проверки; ключ отсутствует в `settings.json`; проверка загружает каталог моделей и показывает количество доступных моделей либо понятную ошибку. Автоматический шлюз покрывается тестами с поддельными HTTP-ответами: при `429` он помечает общую квоту временно недоступной и выбирает следующее подключение.

## Progress

- [x] (2026-07-18 04:04Z) Изучены `PLANS.md`, текущее хранение настроек, устройство `AppSettingsWindow`, реестр утилит и локализация.
- [x] (2026-07-18 04:04Z) Проверены официальные API и правила квот OpenRouter, Cerebras, Gemini, Groq, GitHub Models и Mistral; DeepInfra исключён, потому что постоянная бесплатная квота не подтверждена.
- [x] (2026-07-18 04:04Z) Создан этот ExecPlan в корне проекта.
- [x] (2026-07-18 04:34Z) Реализованы модели данных, каталог провайдеров и сохранение несекретной конфигурации в `AppSettings`.
- [x] (2026-07-18 04:34Z) Реализованы Windows Credential Manager и тестовая абстракция хранилища секретов.
- [x] (2026-07-18 04:34Z) Реализованы получение моделей, проверка ключей, общий transport и маршрутизация с quota scope и cooldown.
- [x] (2026-07-18 04:34Z) Добавлены диалог подключения и раздел «Нейросети» в `AppSettingsWindow`.
- [x] (2026-07-18 04:34Z) Добавлена английская, русская, украинская и немецкая локализация.
- [x] (2026-07-18 04:34Z) Создан `docs/AI_PROVIDERS.md` и обновлены руководство, privacy, архитектура, карта функций и README.
- [x] (2026-07-18 07:30Z) Добавлены focused unit/integration tests; полный прогон завершён результатом 789/789, production Release опубликован, smoke-запуск успешен и создан `artifacts/installer/AiteBar-Setup.exe`.
- [x] (2026-07-18 08:00Z) Диалог подключения дополнен локализованной ссылкой на официальную страницу получения ключа выбранного сервиса; кнопки приоритета сделаны квадратными 32×32 и корректно отключаются на границах списка. Полный прогон: 790/790, installer пересобран.

## Surprises & Discoveries

- Observation: несколько ключей обычно не означают несколько квот.
  Evidence: Gemini применяет квоту к Google project, Cerebras проверяет project и organization, Groq имеет organization ceiling, OpenRouter прямо сообщает, что дополнительные ключи или аккаунты не увеличивают глобальные free-model limits. Поэтому конфигурация должна хранить `QuotaScopeId`, а cooldown должен применяться к группе, не только к ключу.

- Observation: существующий `AppSettingsService` сериализует весь `AppSettings` в обычный JSON.
  Evidence: `AiteBar/AppSettingsService.cs` использует `System.Text.Json`; следовательно, API-ключ нельзя добавлять в `Models.cs` как строковое свойство настроек.

- Observation: `AppSettingsWindow` использует единый прокручиваемый документ и синхронизирует индекс меню с массивом секций.
  Evidence: `GetSettingsSections()` и `SettingsNavigationList` имеют одинаковый порядок. Новый пункт должен быть добавлен перед `About` одновременно в XAML, массив и `AppSettingsSection`, иначе навигация будет вести не туда.

- Observation: sandbox не разрешает MSBuild атомарно заменить файлы в существующих `obj/bin`, а полностью изолированный build сначала не имел NuGet assets.
  Evidence: обычная команда получила `MSB3491 access denied`; после разрешённого restore production-проект собрался в `AiteBar.dll`, а test project потребовал отдельной проверки выходных путей.

- Observation: полный `dotnet test` успешно компилирует и запускает тестовый проект, однако отдельный повторный `dotnet build` решения в этой сессии не может атомарно обновить служебные `Microsoft.CodeCoverage`/`CoverletSourceRootsMapping` файлы из-за ACL временных файлов Windows.
  Evidence: 789 тестов прошли из `artifacts/ai-test-output`, production `AiteBar.dll` собирается, штатный installer script успешно выполнил restore, Release publish и Inno Setup; сбой воспроизводится только в targets coverage тестового проекта и не относится к исходному коду.

## Decision Log

- Decision: первый выпуск поддерживает OpenRouter, Cerebras, Gemini, Groq, GitHub Models и Mistral; DeepInfra не включается.
  Rationale: у первых шести есть документированный бесплатный режим или включённая бесплатная квота, а у DeepInfra обнаружена только pay-as-you-go модель.
  Date/Author: 2026-07-18 / Codex.

- Decision: секреты сохраняются в Windows Credential Manager под именами `AiteBar/AI/{connection-guid}`, а `settings.json` содержит только ссылку на credential target.
  Rationale: проект Windows-only, Credential Manager предназначен для пользовательских секретов и позволяет удалить отдельный ключ. Это предотвращает попадание ключей в настройки, резервные JSON-файлы и диагностику.
  Date/Author: 2026-07-18 / Codex.

- Decision: использовать собственные небольшие REST-клиенты поверх `HttpClient`, без шести SDK.
  Rationale: OpenRouter, Cerebras, Groq, GitHub Models и Mistral имеют OpenAI-подобные интерфейсы, а Gemini требует только отдельного REST-адаптера. Это уменьшает размер publish, поверхность обновлений и риск конфликтов зависимостей.
  Date/Author: 2026-07-18 / Codex.

- Decision: подключения выбираются по приоритету, не round-robin; quota scope отключается целиком при подтверждённом исчерпании общей квоты.
  Rationale: пользователь хочет сначала расходовать основное бесплатное подключение и переходить к резервному. Round-robin затрудняет понимание остатков и способен одновременно исчерпать все резервы.
  Date/Author: 2026-07-18 / Codex.

- Decision: первая UI-версия проверяет ключ через безопасный запрос списка моделей и не отправляет тестовый prompt.
  Rationale: каталог не расходует inference-токены, не передаёт пользовательские данные и достаточен для проверки авторизации у поддерживаемых провайдеров.
  Date/Author: 2026-07-18 / Codex.

- Decision: режим первого выпуска всегда `FreeTierOnly`; автоматического перехода к платному inference нет.
  Rationale: цель функции — использование бесплатных квот. Невидимый платный fallback является финансовым риском и требует отдельного осознанного продукта с бюджетами и подтверждением пользователя.
  Date/Author: 2026-07-18 / Codex.

## Outcomes & Retrospective

Milestone завершён: существует безопасный credential store, динамические каталоги шести провайдеров, общий chat transport, quota-aware gateway, UI нескольких подключений и пользовательская документация. Полный набор из 789 тестов прошёл без падений. Штатный скрипт создал `artifacts/installer/AiteBar-Setup.exe` размером 77 521 960 байт с SHA-256 `F60F0F8BB63A82D36F23303CE992CBC2B865189A4BDF9E850E6FF0F92EFEA86C`; опубликованное приложение успешно прошло краткий startup smoke test.

Ручная проверка с настоящими API-ключами намеренно не выполнялась: ключи принадлежат пользователю, а автоматическая проверка не должна обращаться к внешним inference API. UI-контракты, lifecycle credential metadata, fallback и локализация проверены автоматическими тестами.

## Context and Orientation

`AiteBar` — WPF-приложение .NET 10 для Windows. `AiteBar/Models.cs` содержит сериализуемый `AppSettings`. `AiteBar/AppSettingsService.cs` клонирует настройки, загружает и атомарно сохраняет JSON. `AiteBar/AppSettingsWindow.xaml` и `.xaml.cs` образуют единое окно настроек с меню слева и прокручиваемыми секциями справа. `AiteBar/PathHelper.cs` определяет `%APPDATA%\Codebdbd\Aite Bar`. `AiteBar/UtilityRegistry.cs` запускает встроенные утилиты; будущие AI-утилиты будут получать общий шлюз, а не создавать собственные HTTP-клиенты.

В этом плане «провайдер» означает внешний AI-сервис, например Cerebras. «Подключение» означает один сохранённый API-ключ вместе с понятным названием и принадлежностью к аккаунту или проекту. «Quota scope» означает группу подключений, которые делят одну внешнюю квоту. «Cooldown» означает время, до которого подключение или quota scope временно не выбирается после `429 Too Many Requests`. «Gateway» означает единый C#-сервис, через который будущая утилита отправляет запрос, не зная деталей авторизации и fallback.

Новая конфигурация в `AppSettings` хранит список `AiConnectionSettings`, порядок провайдеров и флаг `FreeTierOnly`. `AiConnectionSettings` не содержит ключ: только GUID, provider ID, название, credential target, quota scope, приоритет, enabled и необязательную предпочитаемую модель. `AppSettingsService.CloneAppSettings` обязан глубоко копировать эти значения.

`IAiCredentialStore` скрывает Windows API и имеет операции write/read/delete. Production-реализация `WindowsCredentialStore` вызывает Unicode-функции `CredWriteW`, `CredReadW`, `CredDeleteW` и всегда освобождает буфер через `CredFree`. Тесты используют память и никогда не записывают настоящие учётные данные.

`AiProviderCatalog` содержит стабильные определения провайдеров, URL документации и стратегию каталога. Динамический список моделей получают с сервера. `AiProviderClient` нормализует разные JSON-форматы в `AiModelDescriptor`. `AiGateway` получает снимок настроек, сортирует enabled-подключения по порядку провайдеров и приоритету, исключает cooldown и отсутствующие секреты, затем выполняет запрос. Для этого выпуска важно реализовать текстовый chat request, даже если UI-утилита ещё не вызывает его: это доказывает, что слой пригоден для будущего использования.

## Plan of Work

Сначала добавить в `AiteBar/AiModels.cs` неизменяемые runtime-типы и сериализуемые настройки. В `Models.cs` добавить `AiSettings` и обеспечить глубокое клонирование в `AppSettingsService.cs`. В `PathHelper` секретные пути не добавлять: секреты не должны храниться файлами.

Затем создать `AiteBar/AiCredentialStore.cs`. Интерфейс должен быть пригоден для подмены в тестах. Production-класс ограничивает размер ключа, не логирует его и возвращает `null`, если target отсутствует. Ошибки Windows превращаются в `InvalidOperationException` с кодом Win32, но без содержимого ключа.

После этого создать `AiteBar/AiProviderCatalog.cs`, `AiteBar/AiProviderClient.cs` и `AiteBar/AiGateway.cs`. Каталог задаёт endpoint и тип авторизации. Клиент получает модели, валидирует соединение и выполняет chat completion. Gemini получает отдельные JSON request/response ветки, остальные используют OpenAI-compatible messages. Парсеры должны терпеть неизвестные поля. HTTP timeout равен 30 секундам; cancellation token проходит до `SendAsync` и чтения JSON.

Маршрутизатор хранит runtime health отдельно от `settings.json`: статус, cooldown и последнее безопасное описание ошибки. `401` отключает конкретный ключ до следующей проверки, `429` уважает `Retry-After` и применяет cooldown к quota scope, `402` помечает scope как exhausted, `5xx` разрешает переход к следующему подключению. В лог не попадают prompt, response и Authorization.

Для UI создать `AiteBar/AiConnectionDialog.xaml` и `.xaml.cs` с ComboBox провайдера, названием подключения, quota scope и `PasswordBox` для ключа. В `AppSettingsWindow` добавить пункт «Нейросети» перед «О программе», section card, общий privacy-текст, кнопку добавления и динамические строки. Добавление пишет секрет временно и запоминает target; Cancel или закрытие окна удаляет новые секреты, Save фиксирует metadata и удаляет секреты удалённых старых подключений. Проверка получает модели без inference-запроса и показывает результат. Ключ после сохранения никогда не выводится обратно.

Добавить строки во все четыре `Resources/Strings*.resx`. Создать `docs/AI_PROVIDERS.md` с объяснением безопасности, free-tier ограничений, quota scope, пошаговым получением ключей для шести сервисов, добавлением нескольких подключений, диагностикой `401/403/429`, удалением и предупреждением о передаче данных внешнему сервису. Обновить `docs/USER_MANUAL.md`, `docs/architecture.md`, `docs/functions.md`, `PRIVACY.md` и README короткими ссылками; не обещать фиксированные квоты, потому что провайдеры меняют их.

Завершить focused tests для каталога, клонирования, credential abstraction, JSON parser, provider ordering, quota-scope cooldown и XAML-контракта. Все внешние ответы должны быть поддельными через `HttpMessageHandler`; тесты не требуют сети или настоящих ключей.

## Concrete Steps

Рабочая директория для всех команд: `D:\01_Codebdbd\01_projects\aitebar`.

Проверить focused tests после создания ядра:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~Ai"

Ожидается ноль падений; число тестов будет зафиксировано после добавления.

Проверить полный проект:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Если `dotnet test` падает только из-за WPF `wpftmp` или generated `obj`, использовать уже собранную DLL:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

После UI-изменения вручную открыть «Настройки программы → Нейросети», добавить тестовое подключение, закрыть через Cancel и убедиться, что временный credential удалён; затем добавить снова, сохранить, открыть настройки и увидеть metadata без ключа. Кнопка «Проверить» с настоящим ключом должна показать число моделей. Этот сетевой smoke test выполняется только пользователем с собственным ключом; автоматические тесты сети не вызывают.

Собрать поставку:

    .\installer\Build-Installer.ps1

Ожидается один актуальный installer в `artifacts\installer`.

## Validation and Acceptance

При запуске Release окно настроек содержит шесть навигационных пунктов, включая «Нейросети» перед «О программе». Раздел не выходит за `MaxWidth` правой колонки и использует существующие Windows 11-подобные cards без рамки вокруг каждой мелкой строки.

Пользователь может добавить два подключения одного провайдера с разными названиями и quota scope, изменить их приоритет кнопками или порядком строк, проверить каждое и удалить. После Save и перезапуска metadata остаётся. В `%APPDATA%\Codebdbd\Aite Bar\settings.json` нет API-ключа и его фрагментов. Удаление подключения удаляет credential.

Focused-тест маршрутизатора создаёт три подключения: первое возвращает `429`, второе имеет тот же quota scope, третье другой scope. Gateway не вызывает второе и успешно использует третье. Другой тест доказывает переход к следующему провайдеру. Тест `401` доказывает, что недействителен только ключ, а не вся независимая quota group.

Каталоги шести провайдеров нормализуются в непустые model IDs из fixture JSON. Бесплатный режим никогда не выбирает модель, явно помеченную `Paid`; неизвестная стоимость не превращается в `VerifiedFree`.

Полный Release build и все существующие тесты проходят. Installer создаётся. Документация позволяет новому пользователю получить ключ каждого сервиса, подключить его и понять ограничения без обращения к исходному коду.

## Idempotence and Recovery

Добавление и удаление metadata выполняется через существующую атомарную запись настроек. Credential target строится из GUID, поэтому повторное сохранение обновляет ту же запись без дубликатов. При Cancel новые targets удаляются. Если приложение аварийно завершится между записью нового credential и Save, останется недоступный из UI credential без metadata; при следующем развитии можно добавить garbage collection по префиксу, но первый выпуск не должен перечислять и удалять чужие targets автоматически.

Нельзя очищать рабочее дерево или откатывать существующие незакоммиченные изменения: они принадлежат пользователю. При конфликте редактировать только соответствующие блоки. Настоящие ключи не использовать в тестах, патчах, логах, screenshots или документации.

## Artifacts and Notes

Подтверждённые endpoint-ы, необходимые для реализации:

    OpenRouter catalog: https://openrouter.ai/api/v1/models
    OpenRouter chat:    https://openrouter.ai/api/v1/chat/completions
    Cerebras models:    https://api.cerebras.ai/v1/models
    Cerebras chat:      https://api.cerebras.ai/v1/chat/completions
    Gemini models:      https://generativelanguage.googleapis.com/v1beta/models?key=...
    Gemini generate:    https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key=...
    Groq models:        https://api.groq.com/openai/v1/models
    Groq chat:          https://api.groq.com/openai/v1/chat/completions
    GitHub catalog:     https://models.github.ai/catalog/models
    GitHub inference:   https://models.github.ai/inference/chat/completions
    Mistral models:     https://api.mistral.ai/v1/models
    Mistral chat:       https://api.mistral.ai/v1/chat/completions

Короткое доказательство текущего риска хранения ключа:

    AppSettingsService -> JsonSerializer -> settings.json

Поэтому `AiConnectionSettings` содержит `CredentialTarget`, но никогда `ApiKey`.

## Interfaces and Dependencies

В `AiteBar/AiCredentialStore.cs` должны существовать:

    internal interface IAiCredentialStore
    {
        void Write(string target, string secret);
        string? Read(string target);
        bool Delete(string target);
    }

В `AiteBar/AiProviderClient.cs` должны существовать методы:

    Task<AiConnectionCheckResult> CheckConnectionAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken);

    Task<AiProviderResponse> GenerateAsync(
        AiConnectionSettings connection,
        AiModelDescriptor model,
        AiChatRequest request,
        CancellationToken cancellationToken);

В `AiteBar/AiGateway.cs` публичный для внутренних утилит контракт:

    Task<AiGatewayResponse> GenerateAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);

Новых NuGet-зависимостей для HTTP или секретов не требуется. Используются `System.Net.Http`, `System.Text.Json` и Win32 Credential Management API, доступный на поддерживаемой Windows-платформе.

Revision note (2026-07-18 04:04Z): создан первоначальный самодостаточный план после исследования репозитория и официальных API; зафиксированы границы бесплатного первого выпуска и безопасная модель нескольких подключений.

Revision note (2026-07-18 04:34Z): отражена фактическая реализация ядра, UI, четырёх локализаций и документации; добавлена обнаруженная особенность WPF/MSBuild sandbox и уточнён оставшийся этап проверки.

Revision note (2026-07-18 07:30Z): план закрыт фактическими результатами полного тестового прогона, Release publish, installer build, checksum и startup smoke test; документировано окруженческое ограничение повторной записи coverage path maps.

Revision note (2026-07-18 08:00Z): отражена UX-доработка диалога — официальный адрес получения API-ключа зависит от выбранного провайдера; зафиксированы квадратные кнопки приоритета и повторная проверка поставки.
