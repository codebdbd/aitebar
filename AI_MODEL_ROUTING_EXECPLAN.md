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
2. Создать временный каталог вне репозитория и скопировать туда каждый уже существующий разрешённый файл, потому что рабочее дерево содержит пользовательские изменения. Копии являются точным baseline этой задачи и не добавляются в Git.
3. Вычислить SHA-256 всех защищённых исходных файлов, перечисленных в `Concrete Steps`.
4. Зафиксировать точные разрешённые hunks: scoped-метод gateway, чистая политика, одна строка call site и тесты.

После каждого патча агент обязан выполнить `git diff --name-only` и проверить, что не появился новый исходный файл вне allowlist. Если появился, работа немедленно останавливается. Агент не продолжает реализацию и не «исправляет заодно» обнаруженный посторонний код.

В конце SHA-256 защищённых файлов повторно вычисляется и сравнивается с исходными значениями. Разрешённые существующие файлы сравниваются со своими временными baseline-копиями через `git diff --no-index`. Для `TextProcessingWindow.xaml.cs` результирующий diff обязан содержать ровно одну замену имени вызываемого gateway-метода без форматирования соседнего кода.

Запрещены sub-agent delegation, автоматическое форматирование всего проекта, обновление пакетов, сетевой поиск, изменение версии, commit, push, publish, installer и запуск приложения. Разрешены только локальная сборка и тесты. Генерируемые `bin`, `obj` и отдельный проверочный каталог внутри `artifacts` не считаются изменением исходного кода.

Если реализация не помещается в allowlist или требует нового контракта, агент обязан остановиться и запросить отдельное разрешение пользователя. Самостоятельно расширять scope нельзя.

## Progress

- [x] (2026-07-25) Повторно проверен первоначальный план и обнаружено, что он выходил за разрешённую область задачи.
- [x] (2026-07-25) Зафиксирован строгий scope lock: только порядок выбора уже доступных моделей и ключей.
- [x] (2026-07-25) Зафиксированы существующие инварианты, которые нельзя менять.
- [x] (2026-07-25) Определён детерминированный порядок моделей и маршрутов на основе уже существующих настроек.
- [x] (2026-07-25) Устранены неоднозначности ревизии: выбран единый трёхполевой `AiRouteCandidate`, проверен фактический контракт automatic/exact запросов, добавлены жёсткие baseline-gates и построчная ревизия `BuildRoutesAsync`.
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

В текущем Text Processing автоматический запрос создаётся через `TextProcessingService.BuildRequest` и не содержит `PreferredProviderId` или `PreferredModelId`. Эти два поля устанавливаются только `TextProcessingWindow.CopyRequestWithModel`, одновременно с `RequireExactModel=true`. Scoped entry point принимает это как строгий контракт: automatic означает `RequireExactModel=false`, `PreferredProviderId=null` и `PreferredModelId=null`; exact означает `RequireExactModel=true` и оба непустых идентификатора.

Обычная и потоковая генерация используют один и тот же упорядоченный список маршрутов до начала выполнения запроса.

Существующие общие методы `GenerateAsync` и `GenerateStreamingAsync` сохраняют прежний порядок маршрутов. Новый порядок применяется только через opt-in метод Text Processing. Это намеренное уточнение границы: инвариант общего gateway важнее унификации всех его entry points.

## Deterministic Selection Contract

Новая политика применяется только scoped-путём Text Processing и должна принимать:

    AiSettings settings
    AiChatRequest request
    IReadOnlyList<AiRouteCandidate> eligibleRoutes

`eligibleRoutes` уже прошли существующую фильтрацию. Политика не имеет права добавлять, удалять, дедуплицировать или повторно классифицировать модель. Она только возвращает перестановку того же множества маршрутов. Общие entry points gateway продолжают использовать существующий порядок и не вызывают эту политику.

Формальный инвариант политики:

    output.Count == input.Count
    multiset(output route identities) == multiset(input route identities)

Идентичность маршрута для этой проверки:

    Connection.Id + ProviderId + ModelId

Если scoped-запрос нарушает контракт режима, scoped entry point должен завершиться fail-closed через `InvalidOperationException` до `BuildCandidates`, чтения каталогов и сетевых вызовов. Политика повторяет эту проверку защитно для прямых unit-тестов и дополнительно валидирует уже собранные exact-кандидаты. Нарушениями считаются: exact mode без одного из двух идентификаторов; automatic mode с непустым `PreferredProviderId` или `PreferredModelId`; exact-кандидат, не совпадающий с запрошенной provider/model. Нельзя удалять «неподходящие» маршруты или подставлять другую модель. Такое исключение означает дрейф контракта между Text Processing и gateway и должно быть покрыто отдельными unit-тестами.

Маршрут-кандидат содержит ссылку на существующие объекты:

    internal sealed record AiRouteCandidate(
        AiConnectionSettings Connection,
        AiModelDescriptor Model,
        int ConnectionOrder);

Это единственное объявление и окончательный набор полей для плана. `ProviderRank` и `PreferredModelRank` вычисляются внутри `AiModelSelectionPolicy.OrderRoutes` из `AiSettings`, `AiChatRequest` и участвующих кандидатов; они не хранятся в `AiRouteCandidate`.

### Порядок провайдеров

В automatic mode `request.PreferredProviderId` по контракту Text Processing отсутствует и не участвует в ранжировании. Сначала используются значения `settings.ProviderOrder`. Затем добавляются отсутствующие значения `AiProviderCatalog.DefaultProviderOrder`. Повторения удаляются без учёта регистра.

В exact mode провайдер уже однозначно задан `request.PreferredProviderId`; после существующей фильтрации политика видит только эту provider/model и сортирует ключи. Любое смешение полей automatic и exact считается нарушением scoped-контракта и завершается fail-closed.

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

При `RequireExactModel=true` существующий `GetEligibleModels` до вызова политики обязан оставить только совпадение:

    connection.ProviderId == request.PreferredProviderId
    model.ModelId == request.PreferredModelId

Сравнение выполняется без учёта регистра. Если `PreferredProviderId` или `PreferredModelId` отсутствует, scoped-контракт нарушен и до выполнения маршрута выбрасывается `InvalidOperationException`; молчаливый fallback запрещён. Никакие preference подключения и fallback-модели не применяются.

Политика повторно не фильтрует exact mode. Она валидирует, что каждый полученный маршрут уже соответствует provider/model запроса, и затем сортирует только ключи. При нарушении входного контракта применяется fail-closed поведение, описанное выше.

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

До любого изменения production-кода сначала проверить точное наличие трёх заявленных характеристических тестов:

    rg -n "Gateway_ExactModel_TriesNextConnectionAfterRateLimit|Gateway_ExactSelection_UsesAllRequestedProviderConnectionsAndNeverChangesModel|Gateway_AutomaticMode_ExhaustsSameModelRoutesBeforeChangingModel" .\AiteBar.Tests\AiProviderTests.cs

На ревизии 2026-07-25 они найдены соответственно на строках 50, 176 и 93. Исполнитель обязан повторить проверку в своей фактической исходной версии. Если хотя бы один тест отсутствует, переименован или находится не в ожидаемом тестовом проекте, это записывается в `Surprises & Discoveries`, реализация останавливается до обновления плана; нельзя молча объявить отсутствующий тест «существующим» или заменить его новым.

Затем, всё ещё до изменения production-кода, в `AiteBar.Tests/AiProviderTests.cs` добавить или уточнить остальные характеристические тесты, которые доказывают разрешённые инварианты.

Нужны следующие тесты:

1. `Gateway_ExactModel_TriesNextConnectionAfterRateLimit` — уже существует и должен остаться зелёным.
2. `Gateway_ExactSelection_UsesAllRequestedProviderConnectionsAndNeverChangesModel` — уже существует и должен остаться зелёным.
3. `Gateway_AutomaticMode_ExhaustsSameModelRoutesBeforeChangingModel` — уже существует и должен остаться зелёным.
4. Новый тест: платная модель не становится кандидатом после введения политики.
5. Новый тест: модель с недостаточным контекстом не становится кандидатом.
6. Новый тест: `PreferredConnectionId` по-прежнему ограничивает маршруты одним ключом.
7. Новый тест: ошибка streaming после начала выдачи не запускает второй запрос.
8. Новый тест: scoped automatic request с `PreferredProviderId` или `PreferredModelId` завершается `InvalidOperationException`.
9. Новый тест: scoped exact request без любого из двух идентификаторов завершается `InvalidOperationException`.

На этом milestone производственный код не меняется. Если какой-либо тест выявляет отличающееся текущее поведение, результат фиксируется в `Surprises & Discoveries`; тест нельзя подгонять под желаемый результат без отдельного решения.

### Milestone 2: Реализовать чистую сортировку

Предпочтительный вариант — добавить `AiteBar/AiModelSelectionPolicy.cs`. Он содержит единственное объявление `AiRouteCandidate`, уже полностью зафиксированное в разделе `Deterministic Selection Contract`, и чистую политику:

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

- выход содержит в точности тот же multiset идентичностей маршрутов, что и вход;
- политика не теряет маршруты, не создаёт новые и не удаляет существующие дубли входа;
- одинаковый набор маршрутов в разном входном порядке даёт одинаковый выход;
- изменение `DisplayName` не меняет выход;
- перестановка JSON-моделей не меняет выход;
- `ProviderOrder` применяется первым;
- существующие `PreferredModelId` применяются в порядке `settings.Connections`;
- непреференциальные модели получают стабильный порядок по `ModelId`;
- ключи модели идут в порядке `settings.Connections`;
- exact mode содержит только точную provider/model;
- нарушение exact-контракта вызывает `InvalidOperationException`, а не скрытую фильтрацию или fallback;
- automatic mode принимает только пустые `PreferredProviderId` и `PreferredModelId`; непустое значение вызывает `InvalidOperationException`;
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

Оба streaming entry point используют существующий `BuildCandidates` без изменений. Создавать `BuildTextProcessingCandidates` запрещено: второй builder продублировал бы фильтры enabled/provider/exact и мог бы разойтись с общим путём.

Хотя `BuildCandidates` возвращает подключения в legacy-порядке с `DisplayName`, scoped-политика не доверяет входному порядку. Она восстанавливает `ConnectionOrder` через индекс `Connection.Id` в исходном `settings.Connections`, а `ProviderRank` — через существующий `ProviderOrder`. Поэтому Text Processing получает стабильный порядок без изменения или дублирования `BuildCandidates`.

`BuildRoutesAsync` может получить внутренний параметр режима сортировки. До патча исполнитель обязан найти и перечислить в `Progress` каждый call site командой `rg -n "BuildRoutesAsync\\(" .\AiteBar\AiGateway.cs`; на ревизии 2026-07-25 их два, из `GenerateAsync` и `GenerateStreamingAsync`. После патча поиск повторяется, каждый найденный вызов должен быть объяснён, а все legacy entry points обязаны передавать legacy-режим явно.

В legacy-режиме метод обязан вернуть не просто эквивалентный набор, а точно прежнюю последовательность маршрутов: одинаковые `Count` и порядок идентичностей `Connection.Id + ProviderId + ModelId`, включая повторы. Существующая сборка `routeGroups` и разворачивание групп в этой ветке сохраняются без перестановки, новой сортировки или смены LINQ-операций. В Text Processing режиме после сбора уже отфильтрованных маршрутов он вызывает:

    AiModelSelectionPolicy.OrderRoutes(settings, request, collectedRoutes)

Обе ветки `BuildRoutesAsync` должны сохранить без изменений:

- последовательное получение кэшированных каталогов;
- `IsConnectionAvailable`;
- `GetEligibleModels`;
- обработку исключений получения каталога;
- существующий `lastError`;
- существующий cache и semaphore.

Старую зависимость Text Processing от первого появления модели удалить только в scoped-ветке. Legacy-ветка сохраняет её для обратной совместимости. Набор кандидатов, фильтры и сетевые обращения обеих веток остаются одинаковыми; различается только финальная перестановка собранных маршрутов.

В `TextProcessingWindow.xaml.cs` заменить только:

    _gateway.GenerateStreamingAsync(...)

на:

    _gateway.GenerateTextProcessingStreamingAsync(...)

Никакие другие строки окна не менять.

`GenerateAsync`, публичный контракт `GenerateStreamingAsync`, `ObserveStreamAsync`, `ApplyFailure`, `MarkSuccessful`, `GetQuotaKey` и `AiProviderClient` семантически не менять.

После интеграции повторить все тесты Milestone 1 и 2.

Затем выполнить обязательную ручную ревизию `BuildRoutesAsync`: сравнить `AiGateway.cs` с сохранённой baseline-копией через `git diff --no-index`, просмотреть построчно каждый изменённый hunk метода и письменно отметить в `Progress`, почему каждая строка не меняет legacy-последовательность. При сомнении реализация останавливается; формулировки «семантически эквивалентно» недостаточно.

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

Добавить отдельный регрессионный тест с намеренно переставленным каталогом и несколькими подключениями. Для общих `GenerateAsync` и `GenerateStreamingAsync` он должен утверждать точную предрефакторинговую последовательность попыток по идентичностям `Connection.Id + ProviderId + ModelId`: то же количество, тот же порядок, те же повторы. Для `GenerateTextProcessingStreamingAsync` тот же fixture должен утверждать новый детерминированный порядок. Так граница безопасности проверяется исполняемым кодом, а не только сравнением множеств или комментариями.

Запустить focused-тесты и полный набор. После этого провести только read-only проверку окна: кроме имени вызываемого метода код не должен требовать изменения привязок, размеров, состояния или подписей.

## Concrete Steps

Рабочий каталог:

    D:\01_Codebdbd\01_projects\aitebar

Перед началом реализации сохранить исходное состояние:

    git status --short
    git diff -- AiteBar/AiGateway.cs AiteBar/TextProcessingWindow.xaml.cs AiteBar.Tests/AiProviderTests.cs
    $routingBaseline = Join-Path $env:TEMP ("aitebar-model-routing-baseline-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $routingBaseline
    Copy-Item -LiteralPath .\AiteBar\AiGateway.cs -Destination (Join-Path $routingBaseline "AiGateway.cs")
    Copy-Item -LiteralPath .\AiteBar\TextProcessingWindow.xaml.cs -Destination (Join-Path $routingBaseline "TextProcessingWindow.xaml.cs")
    Copy-Item -LiteralPath .\AiteBar.Tests\AiProviderTests.cs -Destination (Join-Path $routingBaseline "AiProviderTests.cs")
    Write-Output $routingBaseline
    Get-FileHash AiteBar/TextProcessingWindow.xaml
    Get-FileHash AiteBar/AiProviderClient.cs
    Get-FileHash AiteBar/AiModels.cs
    Get-FileHash AiteBar/AiProviderCatalog.cs
    Get-FileHash AiteBar/Models.cs
    Get-FileHash AiteBar/AppSettingsService.cs
    git diff --check

До первого изменения любого production-файла выполнить свежий полный baseline-прогон:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Историческое значение 897 не является критерием текущего количества. В `Progress` записать фактические `Passed`, `Failed`, `Skipped` и общее число тестов именно этого прогона. Если есть хотя бы одно падение, реализация останавливается и результат заносится в `Surprises & Discoveries`. Если падений нет, но количество отличается от 897, записать новое число и использовать его как baseline этой реализации; изменение числа само по себе не разрешает продолжить без проверки причин.

Напечатанный путь `$routingBaseline`, исходные SHA-256, найденные call sites `BuildRoutesAsync`, подтверждение наличия трёх обязательных тестов и результат полного baseline-прогона записать в `Progress` этого ExecPlan до первого изменения production-кода. Если выполнение продолжается в новой PowerShell-сессии, восстановить `$routingBaseline` из записанного абсолютного пути; не создавать новый baseline после начала правок.

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
    git diff --no-index -- (Join-Path $routingBaseline "AiGateway.cs") .\AiteBar\AiGateway.cs
    git diff --no-index -- (Join-Path $routingBaseline "TextProcessingWindow.xaml.cs") .\AiteBar\TextProcessingWindow.xaml.cs
    git diff --no-index -- (Join-Path $routingBaseline "AiProviderTests.cs") .\AiteBar.Tests\AiProviderTests.cs
    Get-FileHash AiteBar/TextProcessingWindow.xaml
    Get-FileHash AiteBar/AiProviderClient.cs
    Get-FileHash AiteBar/AiModels.cs
    Get-FileHash AiteBar/AiProviderCatalog.cs
    Get-FileHash AiteBar/Models.cs
    Get-FileHash AiteBar/AppSettingsService.cs

`git diff --no-index` возвращает exit code `1`, когда ожидаемые различия существуют; это не является ошибкой проверки. Необходимо вручную подтвердить, что diff содержит только разрешённые hunks. Итоговые SHA-256 защищённых файлов должны в точности совпасть со значениями, записанными до начала.

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

Новая политика возвращает точную перестановку входных маршрутов: количество и multiset `ConnectionId + ProviderId + ModelId` до и после совпадают. Политика не фильтрует кандидатов второй раз.

Фильтрация моделей до сортировки побитово и логически не изменена. Те же модели считаются бесплатными, текстовыми, deprecated и подходящими по контексту, что и до задачи.

Обработка `401`, `403`, `402`, `429`, `5xx`, network, timeout и cancellation не изменена.

Публичная обычная и публичная потоковая генерация сохраняют прежний порядок маршрутов. Scoped-поток Text Processing использует новую политику только до начала запроса. Начавшийся streaming не переключается незаметно на другой ключ.

Формат настроек и сохранённые значения не изменены. Новых полей JSON нет.

XAML, размеры окна, локализация, команды, кнопки, ComboBox, статусная строка, обработка текста, diff, Undo/Redo и защита технических фрагментов не изменены. В code-behind окна допустима ровно одна замена вызова общего gateway на scoped-метод.

Release-сборка завершается с нулём ошибок и предупреждений. Все существующие и новые тесты проходят. Значение 897 — только исторический снимок на момент одной из ревизий плана. При старте реализации обязателен новый полный baseline-прогон; итог сравнивается с зафиксированным фактическим результатом этого прогона и не должен иметь ни одного падения.

## Idempotence and Recovery

Чистая политика сортировки не хранит состояние, поэтому повторный вызов с тем же набором маршрутов всегда даёт тот же результат.

Интеграция выполняется одним вызовом политики после существующей фильтрации. Если новый порядок вызывает регрессию, можно временно вернуть старое разворачивание `routeGroups`, не затрагивая UI, provider client, настройки или status dictionaries.

Нельзя использовать `git reset --hard`, `git checkout --` или удалять существующие изменения грязного рабочего дерева. Откат допустим только точечным патчем файлов, изменённых этой задачей.

Новый файл политики не содержит миграции или persistent state. Его удаление вместе с возвратом прежнего вызова полностью восстанавливает старый выбор.

## Artifacts and Notes

Исторический baseline на момент пересмотра плана (не заменяет обязательный свежий прогон перед реализацией):

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

Plan revision note (2026-07-25): После экспертной проверки удалён дублирующий `BuildTextProcessingCandidates`; scoped-путь использует неизменённый общий `BuildCandidates` и восстанавливает стабильные ranks из настроек. Политика формально ограничена перестановкой того же multiset маршрутов, exact mode валидируется fail-closed, а изменения разрешённых грязных файлов сравниваются с точными baseline-копиями из временного каталога.

Plan revision note (2026-07-25): После второй экспертной проверки оставлено одно окончательное трёхполевое объявление `AiRouteCandidate`; по фактическому коду закреплено, что automatic Text Processing не передаёт provider/model preference, а exact передаёт оба идентификатора. Добавлены stop-gates при отсутствии заявленных тестов или красном свежем baseline, полный пересчёт тестов вместо доверия историческим 897 и построчная ревизия `BuildRoutesAsync` с точным сохранением последовательности legacy-маршрутов.
