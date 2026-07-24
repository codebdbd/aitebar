# Стабилизировать выбор AI-модели без изменения остальных подсистем

Этот ExecPlan является живым документом. Разделы `Progress`, `Surprises & Discoveries`, `Decision Log` и `Outcomes & Retrospective` должны обновляться по мере выполнения работы.

План ведётся в соответствии с `PLANS.md` в корне репозитория. Он намеренно ограничен единственной задачей: изменить только внутренний порядок выбора логической модели и API-подключения в Text Processing. Публичный и пользовательский интерфейсы, настройки, список поддерживаемых моделей, классификация бесплатности, сетевой протокол, семантика streaming, тексты ошибок и обработка результата в рамках этого плана не меняются. Единственное новое внутреннее API — scoped-метод gateway, который нельзя вызвать случайно из общего пути.

## Purpose / Big Picture

После выполнения плана существующая утилита «Обработка текста» выглядит и работает для пользователя так же, как сейчас, за одним исключением: режим `Автоматически` выбирает модель и API-ключ по детерминированному, объяснимому порядку.

Каждая модель продолжает отображаться в ComboBox один раз независимо от количества ключей. Явно выбранная модель не подменяется другой. Если первый ключ выбранной модели получает ограничение, шлюз пробует следующий ключ этой же модели. В автоматическом режиме шлюз сначала перебирает все ключи одной логической модели и только после этого переходит к следующей логической модели.

Изменение считается успешным, если перестановка моделей в ответе провайдера, переименование API-подключения и повторное открытие окна не приводят к случайному выбору другой модели, а все существующие функции и тесты остаются без регрессий.

## Scope Lock

Разрешены изменения только в алгоритме упорядочивания уже полученных и уже отфильтрованных моделей и подключений.

Допустимый набор производственных файлов:

    AiteBar/AiGateway.cs
    AiteBar/AiModelSelectionPolicy.cs
    AiteBar/TextProcessingWindow.xaml.cs

`AiModelSelectionPolicy.cs` является необязательным. Его следует добавить только если выделение чистой политики действительно упрощает тестирование. Если изменение остаётся компактным, политика может быть внутренним чистым методом `AiGateway`.

В `TextProcessingWindow.xaml.cs` разрешено изменить только один call site запуска AI: заменить вызов общего `GenerateStreamingAsync` на специально выделенный внутренний метод Text Processing. Любое другое изменение этого файла запрещено. До и после реализации необходимо проверить точный diff этого файла.

Допустимый набор тестовых файлов:

    AiteBar.Tests/AiProviderTests.cs

Допустимо изменить этот ExecPlan по мере выполнения:

    AI_MODEL_ROUTING_EXECPLAN.md

Любой другой файл считается вне области задачи. Если реализация требует изменить иной файл, выполнение необходимо остановить, записать причину в `Surprises & Discoveries` и пересмотреть план до внесения такого изменения.

В частности, план запрещает:

- менять `TextProcessingWindow.xaml`;
- менять в `TextProcessingWindow.xaml.cs` что-либо, кроме одного вызова gateway, выбирающего scoped-политику Text Processing;
- менять вид, размеры, подписи, список или порядок строк ComboBox;
- менять `Models.cs`, формат `settings.json` или добавлять новые настройки;
- менять `AiModels.cs` и публичные контракты `AiChatRequest`, `AiGatewayResponse`, `AiGatewayStream`;
- менять `AiProviderClient.cs`, HTTP-запросы, тайм-ауты и streaming;
- менять `AiProviderCatalog.cs`, список провайдеров или определение бесплатности;
- менять фильтрацию платных, неизвестных, нетекстовых, image/video и deprecated-моделей;
- менять вычисление контекста или оценку токенов;
- менять тексты ошибок, ресурсы локализации и документацию пользователя;
- менять время жизни `AiGateway`, устройство кэша и сохранение runtime-состояния;
- добавлять сохранение истории, текста, квот или здоровья ключей на диск;
- вводить round-robin, балансировку нагрузки или параллельные запросы;
- менять поведение начавшегося потока после получения первого чанка.

Эти ограничения являются частью приёмки, а не рекомендациями.

## Execution Safety Gate

План ограничивает запись, а не необходимое read-only изучение зависимостей. Агент может читать связанные типы и тесты, чтобы не сломать существующие контракты, но не может изменять их.

Перед первым патчем агент обязан:

1. Сохранить `git status --short`.
2. Сохранить текущий diff каждого разрешённого файла, потому что рабочее дерево уже содержит пользовательские изменения.
3. Вычислить SHA-256 всех защищённых исходных файлов, перечисленных в `Concrete Steps`.
4. Зафиксировать точные разрешённые hunks: scoped-метод gateway, чистая политика, одна строка call site и тесты.

После каждого патча агент обязан выполнить `git diff --name-only` и проверить, что не появился новый исходный файл вне allowlist. Если появился, работа немедленно останавливается. Агент не продолжает реализацию и не «исправляет заодно» обнаруженный посторонний код.

В конце SHA-256 защищённых файлов повторно вычисляется и сравнивается с исходными значениями. Единственное исключение — `TextProcessingWindow.xaml.cs`, для которого вместо hash требуется точная проверка diff: одна замена имени вызываемого gateway-метода без форматирования соседнего кода.

Запрещены sub-agent delegation, автоматическое форматирование всего проекта, обновление пакетов, сетевой поиск, изменение версии, commit, push, publish, installer и запуск приложения. Разрешены только локальная сборка и тесты. Генерируемые `bin`, `obj` и отдельный проверочный каталог внутри `artifacts` не считаются изменением исходного кода.

Если реализация не помещается в allowlist или требует нового контракта, агент обязан остановиться и запросить отдельное разрешение пользователя. Самостоятельно расширять scope нельзя.

## Progress

- [x] (2026-07-25) Повторно проверен первоначальный план и обнаружено, что он выходил за разрешённую область задачи.
- [x] (2026-07-25) Зафиксирован строгий scope lock: только порядок выбора уже доступных моделей и ключей.
- [x] (2026-07-25) Зафиксированы существующие инварианты, которые нельзя менять.
- [x] (2026-07-25) Определён детерминированный порядок моделей и маршрутов на основе уже существующих настроек.
- [ ] Перед реализацией добавить характеристические тесты текущего разрешённого поведения.
- [ ] Реализовать чистую детерминированную политику упорядочивания маршрутов.
- [ ] Подключить политику внутри `AiGateway.BuildRoutesAsync` без изменения остальных стадий.
- [ ] Добавить регрессионные тесты точного и автоматического выбора.
- [ ] Проверить allowlist изменённых файлов, Release-сборку и полный набор тестов.
- [ ] Записать фактические результаты в `Outcomes & Retrospective`.

## Surprises & Discoveries

- Observation: Пользовательская дедупликация моделей уже реализована и не требует дальнейшего изменения.
  Evidence: `TextProcessingWindow.BuildLogicalModelItems` объединяет записи по `ProviderId + ModelId`, а ComboBox получает готовые логические элементы.

- Observation: Базовая ротация ключей одной модели уже работает.
  Evidence: `AiGateway.BuildRoutesAsync` группирует маршруты по `ProviderId + ModelId`, после чего `GenerateAsync` и `GenerateStreamingAsync` обходят маршруты в полученном порядке.

- Observation: Явный выбор уже передаёт только `PreferredProviderId + PreferredModelId` и не закрепляет Text Processing за одним API-подключением.
  Evidence: `TextProcessingWindow.CopyRequestWithModel` устанавливает `RequireExactModel=true` без `PreferredConnectionId`.

- Observation: Недетерминированность сосредоточена в порядке маршрутов, а не в фильтрации.
  Evidence: порядок логических групп определяется первым появлением модели при обходе подключений и каталогов. Каталог внешнего провайдера может вернуть те же модели в другом порядке.

- Observation: Отображаемое имя подключения участвует в маршрутизации.
  Evidence: `AiGateway.BuildCandidates` применяет `ThenBy(connection => connection.DisplayName)`. Переименование подключения может изменить первый используемый ключ.

- Observation: В настройках уже существует `AiConnectionSettings.PreferredModelId`.
  Evidence: текущий `GetEligibleModels` поднимает эту модель в начало каталога конкретного подключения. Новая политика должна сохранить смысл этого поля без добавления нового формата настроек.

- Observation: Список `AiSettings.Connections` уже имеет устойчивый сериализованный порядок.
  Evidence: настройки хранят подключения как `List<AiConnectionSettings>`. Этот порядок можно использовать как единственный стабильный tie-break ключей, не меняя модель данных.

- Observation: `AiGateway` является общим типом приложения, а не приватной деталью Text Processing.
  Evidence: `MainWindow` создаёт собственный `AiGateway` и возвращает его через `GetAiGateway()`. Сейчас фактический вызов генерации найден только в `TextProcessingWindow`, но изменение общего метода создало бы скрытый риск для будущих или внешних потребителей.

## Decision Log

- Decision: Не исправлять в этом плане бесплатность, кэширование каталогов, классификацию ошибок и lifetime шлюза.
  Rationale: Это отдельные архитектурные задачи. Пользователь разрешил изменить только подход к выбору модели; совместное изменение нескольких подсистем повышает риск регрессии и усложняет проверку.
  Date/Author: 2026-07-25 / Codex

- Decision: Не менять существующую идентичность логической модели `ProviderId + ModelId`.
  Rationale: Она уже используется UI и gateway, корректно скрывает повторение ключей и не смешивает модели разных провайдеров.
  Date/Author: 2026-07-25 / Codex

- Decision: Не менять фильтры кандидатов.
  Rationale: `deprecated`, capabilities, writing suitability, free-only и context capacity уже применяются до упорядочивания. Новая политика получает только прошедшие текущие фильтры маршруты.
  Date/Author: 2026-07-25 / Codex

- Decision: Для явного режима сохранить строгий `ProviderId + ModelId`.
  Rationale: Явный выбор пользователя никогда не должен переходить к другой модели или провайдеру. Меняется только ключ внутри этой же пары.
  Date/Author: 2026-07-25 / Codex

- Decision: Для автоматического режима использовать существующие предпочтения и стабильные технические идентификаторы.
  Rationale: Добавление нового рейтинга моделей, новых настроек или внешней базы качества выходит за scope. Детерминированность можно получить из уже существующих `ProviderOrder`, `Connections` и `PreferredModelId`.
  Date/Author: 2026-07-25 / Codex

- Decision: Стабильный порядок ключей определяется позицией подключения в `AiSettings.Connections`.
  Rationale: Порядок списка настроек не зависит от локализации и переименования. `DisplayName` остаётся только пользовательской подписью и не участвует в маршрутизации.
  Date/Author: 2026-07-25 / Codex

- Decision: Использовать primary-first failover, а не round-robin.
  Rationale: Текущее ожидаемое поведение — использовать очередной ключ до ограничения, затем следующий. Балансировка нагрузки является другой продуктовой политикой и не вводится скрыто.
  Date/Author: 2026-07-25 / Codex

- Decision: Не продолжать начавшийся streaming другим ключом.
  Rationale: После выдачи части текста повторный запрос может создать другой результат. Текущее поведение streaming не относится к порядку первоначального выбора и остаётся неизменным.
  Date/Author: 2026-07-25 / Codex

- Decision: Не менять поведение существующих общих `AiGateway.GenerateAsync` и `AiGateway.GenerateStreamingAsync`.
  Rationale: `AiGateway` доступен через `MainWindow` и потенциально является общей инфраструктурой. Новая детерминированная политика должна включаться только отдельным внутренним методом, вызываемым Text Processing.
  Date/Author: 2026-07-25 / Codex

- Decision: Добавить scoped-метод `GenerateTextProcessingStreamingAsync`, который делегирует тому же streaming-core, но передаёт детерминированную политику маршрутов.
  Rationale: Это создаёт явную границу безопасности без нового поля настроек, изменения `AiChatRequest` или влияния на другие callers. В окне меняется только имя вызываемого метода.
  Date/Author: 2026-07-25 / Codex

## Existing Invariants

Следующие условия должны быть подтверждены тестами до изменения кода и остаться истинными после него.

Одна логическая модель соответствует одной паре `ProviderId + ModelId`. Количество API-ключей не создаёт дополнительные пользовательские модели.

Явный запрос содержит `RequireExactModel=true`, `PreferredProviderId` и `PreferredModelId`. Если такая модель отсутствует на конкретном ключе, шлюз пропускает этот ключ. Если она отсутствует на всех ключах, другая модель не выбирается.

Автоматический запрос использует текущие фильтры:

- требуемые capabilities;
- `RequireWritingModel`;
- `FreeTierOnly` или `RequireFreeModel`;
- отсутствие `IsDeprecated`;
- достаточный `RequiredContextTokens`.

При `429` или другой уже обрабатываемой ошибке до начала streaming шлюз продолжает текущий список маршрутов. Существующие правила `ApplyFailure`, cooldown и status dictionaries не меняются.

`PreferredConnectionId`, если его передал другой существующий caller, продолжает ограничивать кандидаты одним подключением. Text Processing его не использует, но обратная совместимость `AiChatRequest` сохраняется.

`ProviderOrder` продолжает задавать приоритет провайдеров.

`AiConnectionSettings.PreferredModelId` продолжает влиять только на автоматический выбор. При `RequireExactModel=true` оно игнорируется в пользу `request.PreferredModelId`.

Обычная и потоковая генерация используют один и тот же упорядоченный список маршрутов до начала выполнения запроса.

Существующие общие методы `GenerateAsync` и `GenerateStreamingAsync` сохраняют прежний порядок маршрутов. Новый порядок применяется только через opt-in метод Text Processing. Это намеренное уточнение границы: инвариант общего gateway важнее унификации всех его entry points.

## Deterministic Selection Contract

Новая политика применяется только scoped-путём Text Processing и должна принимать:

    AiSettings settings
    AiChatRequest request
    IReadOnlyList<AiRouteCandidate> eligibleRoutes

`eligibleRoutes` уже прошли существующую фильтрацию. Политика не имеет права добавлять, удалять или повторно классифицировать модель. Она только сортирует. Общие entry points gateway продолжают использовать существующий порядок и не вызывают эту политику.

Маршрут-кандидат содержит ссылку на существующие объекты:

    internal sealed record AiRouteCandidate(
        AiConnectionSettings Connection,
        AiModelDescriptor Model,
        int ConnectionOrder,
        int ProviderOrder,
        int PreferredModelOrder);

Точный набор полей можно упростить при реализации, но результат обязан соответствовать следующему контракту.

### Порядок провайдеров

Сначала используется `request.PreferredProviderId`, если он задан и запрос не является строгим выбором другого провайдера. Затем используются значения `settings.ProviderOrder`. Затем добавляются отсутствующие значения `AiProviderCatalog.DefaultProviderOrder`. Повторения удаляются без учёта регистра.

Это сохраняет текущий смысл `BuildCandidates`.

### Порядок предпочитаемых моделей

Для каждого провайдера строится список предпочитаемых моделей из уже существующих `AiConnectionSettings.PreferredModelId` только тех подключений, которые включены, принадлежат известному провайдеру и уже вошли в набор кандидатов `BuildCandidates`.

Подключения рассматриваются в порядке `settings.Connections`. Отключённое, неизвестное или исключённое через `PreferredConnectionId` подключение не влияет на rank. Пустые значения пропускаются. Повторяющиеся `ModelId` удаляются без учёта регистра. Поэтому если несколько участвующих ключей одного провайдера содержат разные предпочтения, первое предпочтение в сохранённом списке получает более высокий приоритет.

Никакой новый preference не создаётся. Значения настроек не изменяются.

### Порядок логических моделей в автоматическом режиме

Логические модели сортируются по следующему кортежу:

    ProviderRank
    PreferredModelRank
    ModelId ordinal-ignore-case

`PreferredModelRank` равен позиции модели в списке предпочтений провайдера. Для модели без preference используется `int.MaxValue`.

`ModelId` является последним стабильным tie-break. `DisplayName`, локализованная подпись и порядок модели в JSON не используются.

Это не пытается определить «лучшую нейросеть» по скрытой эвристике. Политика лишь делает существующие настройки детерминированными. Если в будущем понадобится продуктовый рейтинг качества моделей, он должен быть отдельным явно разрешённым изменением.

### Порядок логических моделей в явном режиме

При `RequireExactModel=true` допускается только совпадение:

    connection.ProviderId == request.PreferredProviderId
    model.ModelId == request.PreferredModelId

Сравнение выполняется без учёта регистра. Если `PreferredProviderId` или `PreferredModelId` отсутствует, список маршрутов пуст. Никакие preference подключения и fallback-модели не применяются.

### Порядок ключей внутри модели

Маршруты одной логической модели сортируются по позиции `Connection` в `settings.Connections`.

Если `PreferredConnectionId` задан, существующий `BuildCandidates` сначала ограничивает набор одним подключением; политика не расширяет этот набор.

`DisplayName`, `CredentialTarget`, состояние локализации и порядок ответа каталога не участвуют в сортировке ключей.

### Итоговый порядок

После сортировки результат разворачивается строго как:

    Logical model 1
        Connection 1
        Connection 2
        Connection N
    Logical model 2
        Connection 1
        Connection 2
        Connection N

Таким образом, режим `Автоматически` исчерпывает все ключи текущей модели до следующей модели. Явный режим содержит ровно одну логическую модель.

## Plan of Work

### Milestone 1: Зафиксировать текущее поведение тестами

До изменения `AiGateway` в `AiteBar.Tests/AiProviderTests.cs` добавить или уточнить тесты, которые доказывают разрешённые инварианты.

Нужны следующие тесты:

1. `Gateway_ExactModel_TriesNextConnectionAfterRateLimit` — уже существует и должен остаться зелёным.
2. `Gateway_ExactSelection_UsesAllRequestedProviderConnectionsAndNeverChangesModel` — уже существует и должен остаться зелёным.
3. `Gateway_AutomaticMode_ExhaustsSameModelRoutesBeforeChangingModel` — уже существует и должен остаться зелёным.
4. Новый тест: платная модель не становится кандидатом после введения политики.
5. Новый тест: модель с недостаточным контекстом не становится кандидатом.
6. Новый тест: `PreferredConnectionId` по-прежнему ограничивает маршруты одним ключом.
7. Новый тест: ошибка streaming после начала выдачи не запускает второй запрос.

На этом milestone производственный код не меняется. Если какой-либо тест выявляет отличающееся текущее поведение, результат фиксируется в `Surprises & Discoveries`; тест нельзя подгонять под желаемый результат без отдельного решения.

### Milestone 2: Реализовать чистую сортировку

Предпочтительный вариант — добавить `AiteBar/AiModelSelectionPolicy.cs` с внутренними типами:

    internal sealed record AiRouteCandidate(
        AiConnectionSettings Connection,
        AiModelDescriptor Model,
        int ConnectionOrder);

    internal static class AiModelSelectionPolicy
    {
        public static IReadOnlyList<AiRouteCandidate> OrderRoutes(
            AiSettings settings,
            AiChatRequest request,
            IEnumerable<AiRouteCandidate> routes);
    }

Метод должен быть чистым:

- не выполнять сетевых запросов;
- не читать настройки через `AppSettingsService`;
- не читать время;
- не менять входные объекты;
- не обращаться к WPF;
- не фильтровать бесплатность, capabilities или context;
- не обновлять здоровье подключений;
- возвращать новый массив в детерминированном порядке.

Если добавление отдельного файла потребует изменения project-файла, сначала проверить SDK-style glob. В текущем .NET SDK проекте `.cs` обычно включаются автоматически; `AiteBar.csproj` менять нельзя.

В `AiteBar.Tests/AiProviderTests.cs` добавить чистые тесты политики:

- одинаковый набор маршрутов в разном входном порядке даёт одинаковый выход;
- изменение `DisplayName` не меняет выход;
- перестановка JSON-моделей не меняет выход;
- `ProviderOrder` применяется первым;
- существующие `PreferredModelId` применяются в порядке `settings.Connections`;
- непреференциальные модели получают стабильный порядок по `ModelId`;
- ключи модели идут в порядке `settings.Connections`;
- exact mode содержит только точную provider/model;
- разные провайдеры с одинаковым `ModelId` не объединяются.

Milestone завершён, когда тесты чистой политики проходят и ни один существующий production path ещё не переключён.

### Milestone 3: Подключить политику в AiGateway

Общие `GenerateAsync`, `GenerateStreamingAsync` и `BuildCandidates` должны сохранить прежнее поведение. Для Text Processing добавить отдельный внутренний entry point:

    internal Task<AiGatewayStream> GenerateTextProcessingStreamingAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);

Оба streaming entry point должны делегировать одному private core, но передавать разный режим упорядочивания:

    GenerateStreamingAsync
        -> Legacy ordering

    GenerateTextProcessingStreamingAsync
        -> DeterministicTextProcessing ordering

Для режима Text Processing добавить отдельный private `BuildTextProcessingCandidates` либо эквивалентную чистую ветку. Она должна сохранить все фильтры существующего `BuildCandidates`, но использовать порядок `settings.Connections` внутри `ProviderOrder` и не сортировать по `DisplayName`. Общий `BuildCandidates` не менять, чтобы не затронуть другие callers.

`BuildRoutesAsync` может получить внутренний параметр режима сортировки. В legacy-режиме он обязан вернуть маршруты точно в прежнем порядке. В Text Processing режиме после сбора уже отфильтрованных маршрутов он вызывает:

    AiModelSelectionPolicy.OrderRoutes(settings, request, collectedRoutes)

Обе ветки `BuildRoutesAsync` должны сохранить без изменений:

- последовательное получение кэшированных каталогов;
- `IsConnectionAvailable`;
- `GetEligibleModels`;
- обработку исключений получения каталога;
- существующий `lastError`;
- существующий cache и semaphore.

Старую зависимость Text Processing от первого появления модели удалить только в scoped-ветке. Legacy-ветка сохраняет её для обратной совместимости.

В `TextProcessingWindow.xaml.cs` заменить только:

    _gateway.GenerateStreamingAsync(...)

на:

    _gateway.GenerateTextProcessingStreamingAsync(...)

Никакие другие строки окна не менять.

`GenerateAsync`, публичный контракт `GenerateStreamingAsync`, `ObserveStreamAsync`, `ApplyFailure`, `MarkSuccessful`, `GetQuotaKey` и `AiProviderClient` семантически не менять.

После интеграции повторить все тесты Milestone 1 и 2.

### Milestone 4: Проверить отсутствие побочных изменений

Проверить allowlist:

    git diff --name-only

В результате этой задачи среди новых изменений допустимы только:

    AI_MODEL_ROUTING_EXECPLAN.md
    AiteBar/AiGateway.cs
    AiteBar/AiModelSelectionPolicy.cs
    AiteBar/TextProcessingWindow.xaml.cs
    AiteBar.Tests/AiProviderTests.cs

В репозитории уже существует грязное рабочее дерево. Поэтому проверка должна сравнивать не весь `git status`, а конкретный diff этой задачи с сохранённым перед началом списком и содержимым. Нельзя изменять или откатывать посторонние пользовательские файлы.

Проверить отдельно:

- `TextProcessingWindow.xaml` не изменён этой задачей;
- diff `TextProcessingWindow.xaml.cs` содержит ровно замену одного имени метода gateway;
- `AiProviderClient.cs` не изменён этой задачей;
- `AiModels.cs` не изменён этой задачей;
- `AiProviderCatalog.cs` не изменён этой задачей;
- `Models.cs` и `AppSettingsService.cs` не изменены этой задачей;
- resource-файлы не изменены этой задачей;
- пользовательская документация не изменена этой задачей.

Добавить отдельный тест, доказывающий, что один и тот же искусственно переставленный каталог даёт прежний legacy-порядок через общий `GenerateStreamingAsync` и детерминированный порядок через `GenerateTextProcessingStreamingAsync`. Так граница безопасности проверяется исполняемым кодом, а не только комментариями.

Запустить focused-тесты и полный набор. После этого провести только read-only проверку окна: кроме имени вызываемого метода код не должен требовать изменения привязок, размеров, состояния или подписей.

## Concrete Steps

Рабочий каталог:

    D:\01_Codebdbd\01_projects\aitebar

Перед началом реализации сохранить исходное состояние:

    git status --short
    git diff -- AiteBar/AiGateway.cs AiteBar/TextProcessingWindow.xaml.cs AiteBar.Tests/AiProviderTests.cs
    Get-FileHash AiteBar/TextProcessingWindow.xaml
    Get-FileHash AiteBar/AiProviderClient.cs
    Get-FileHash AiteBar/AiModels.cs
    Get-FileHash AiteBar/AiProviderCatalog.cs
    Get-FileHash AiteBar/Models.cs
    Get-FileHash AiteBar/AppSettingsService.cs
    git diff --check

После характеристических тестов:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~Gateway_"

После чистой политики:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AiModelSelectionPolicy"

После интеграции:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~Gateway_|FullyQualifiedName~AiStreamingTests|FullyQualifiedName~TextProcessingModelEligibilityTests"

Финальная Release-сборка:

    dotnet build .\AiteBar.sln -c Release -m:1 -nr:false

Финальный полный набор:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Если обычные `bin`/`obj` заблокированы WPF/MSBuild, использовать новый каталог внутри `artifacts`:

    dotnet build .\AiteBar.sln -c Release -m:1 -nr:false -p:ReleaseVerificationRoot=<новый путь внутри artifacts>
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -p:ReleaseVerificationRoot=<новый путь внутри artifacts>

При финальной проверке выполнить:

    git diff --check
    git diff --name-only
    git diff -- AiteBar/TextProcessingWindow.xaml.cs
    Get-FileHash AiteBar/TextProcessingWindow.xaml
    Get-FileHash AiteBar/AiProviderClient.cs
    Get-FileHash AiteBar/AiModels.cs
    Get-FileHash AiteBar/AiProviderCatalog.cs
    Get-FileHash AiteBar/Models.cs
    Get-FileHash AiteBar/AppSettingsService.cs

Не запускать приложение автоматически, если уже существует запущенный экземпляр AiteBar. Данная задача не требует изменения UI и может быть полностью проверена fake-HTTP и pure unit-тестами. Ручной запуск допускается только по отдельной команде пользователя.

## Validation and Acceptance

План считается выполненным только при одновременном соблюдении всех условий.

Видимый список моделей не изменился. Одна модель остаётся одной строкой независимо от количества ключей.

Явно выбранная модель использует только точные `ProviderId + ModelId`. При недоступности первого ключа пробуется следующий ключ этой модели. Другая модель не вызывается.

Автоматический режим использует:

    ProviderOrder
    существующие PreferredModelId
    стабильный ModelId
    порядок Connections для ключей

Порядок ответа каталога и `DisplayName` не влияют на выбор.

Все ключи логической модели идут подряд до следующей модели.

Это новое поведение включается только вызовом `GenerateTextProcessingStreamingAsync` из Text Processing. Общие `GenerateAsync` и `GenerateStreamingAsync` сохраняют прежний порядок и подтверждены отдельным сравнительным тестом.

Фильтрация моделей до сортировки побитово и логически не изменена. Те же модели считаются бесплатными, текстовыми, deprecated и подходящими по контексту, что и до задачи.

Обработка `401`, `403`, `402`, `429`, `5xx`, network, timeout и cancellation не изменена.

Публичная обычная и публичная потоковая генерация сохраняют прежний порядок маршрутов. Scoped-поток Text Processing использует новую политику только до начала запроса. Начавшийся streaming не переключается незаметно на другой ключ.

Формат настроек и сохранённые значения не изменены. Новых полей JSON нет.

XAML, размеры окна, локализация, команды, кнопки, ComboBox, статусная строка, обработка текста, diff, Undo/Redo и защита технических фрагментов не изменены. В code-behind окна допустима ровно одна замена вызова общего gateway на scoped-метод.

Release-сборка завершается с нулём ошибок и предупреждений. Все существующие и новые тесты проходят. На момент создания пересмотренного плана baseline полного набора составляет 897 пройденных тестов; итог не должен иметь ни одного падения.

## Idempotence and Recovery

Чистая политика сортировки не хранит состояние, поэтому повторный вызов с тем же набором маршрутов всегда даёт тот же результат.

Интеграция выполняется одним вызовом политики после существующей фильтрации. Если новый порядок вызывает регрессию, можно временно вернуть старое разворачивание `routeGroups`, не затрагивая UI, provider client, настройки или status dictionaries.

Нельзя использовать `git reset --hard`, `git checkout --` или удалять существующие изменения грязного рабочего дерева. Откат допустим только точечным патчем файлов, изменённых этой задачей.

Новый файл политики не содержит миграции или persistent state. Его удаление вместе с возвратом прежнего вызова полностью восстанавливает старый выбор.

## Artifacts and Notes

Baseline на момент пересмотра плана:

    Release-сборка: 0 предупреждений, 0 ошибок.
    Полный набор: 897 пройдено, 0 не пройдено.
    UI уже дедуплицирует ProviderId + ModelId.
    Exact mode уже ротирует ключи одной модели.
    Automatic mode уже использует model-first маршрут,
    но порядок логических моделей зависит от порядка каталогов.

Ключевое отличие пересмотренного плана от первоначального: удалены изменения бесплатности, кэша, lifetime, error classifier, route health, UI, настроек, локализации, документации и сетевого клиента. Они могут быть рассмотрены только отдельными задачами с отдельным разрешением пользователя.

## Interfaces and Dependencies

Новые внешние зависимости и NuGet-пакеты не требуются.

Существующие интерфейсы остаются без изменений:

    AiGateway.GenerateAsync(AiChatRequest, CancellationToken)
    AiGateway.GenerateStreamingAsync(AiChatRequest, CancellationToken)
    AiGateway.GetModelsAsync(AiConnectionSettings, CancellationToken)
    AiChatRequest
    AiGatewayResponse
    AiGatewayStream
    AiConnectionSettings
    AiSettings

Добавляется один внутренний opt-in entry point только для Text Processing:

    AiGateway.GenerateTextProcessingStreamingAsync(
        AiChatRequest request,
        CancellationToken cancellationToken)

Он не заменяет и не меняет публичный `GenerateStreamingAsync`.

Второй новый внутренний интерфейс появляется только если будет создан отдельный файл политики:

    AiModelSelectionPolicy.OrderRoutes(
        AiSettings settings,
        AiChatRequest request,
        IEnumerable<AiRouteCandidate> routes)

Политика возвращает те же маршруты, что получила, без добавления и удаления; меняется только порядок.

Plan revision note (2026-07-25): Первоначальный расширенный план признан слишком широким для разрешённой задачи. Пересмотренная версия вводит строгий allowlist файлов и меняет исключительно детерминированный порядок уже доступных моделей и ключей. Бесплатность, UI, настройки, каталоги, HTTP, streaming, ошибки, cache, runtime health и документация явно зафиксированы как неизменяемые.

Plan revision note (2026-07-25): После проверки usages обнаружено, что `AiGateway` создаётся и экспортируется `MainWindow`. Для исключения косвенного влияния на другие функции новая политика переведена в opt-in метод `GenerateTextProcessingStreamingAsync`; публичные gateway entry points сохраняют legacy-порядок, а `TextProcessingWindow.xaml.cs` допускает ровно одну замену имени вызываемого метода.
