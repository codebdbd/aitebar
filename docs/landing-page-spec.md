# AiteBar Landing Page PRD

## 1. Анализ продукта

### Назначение программы

AiteBar - локальная desktop-утилита для Windows, которая добавляет скрываемую панель быстрого доступа у выбранного края экрана. Панель появляется по наведению курсора на активную зону, через tray или горячую клавишу и позволяет запускать пользовательские действия.

Поддержанные действия:

- открытие web-ссылок;
- запуск программ;
- открытие файлов;
- открытие папок;
- запуск скриптов `.bat`, `.cmd`, `.ps1`, `.py`;
- выполнение консольных команд после подтверждения;
- отправка горячих клавиш через Win32 `SendInput`.

### Принцип работы

Пользователь настраивает панели-контексты и добавляет на них кнопки. Каждая кнопка содержит название, тип действия, значение действия, иконку, цвет и принадлежность к панели.

Панель скрыта большую часть времени. Когда пользователь подводит курсор к выбранному краю экрана, нажимает hotkey или открывает приложение из tray, панель появляется поверх окон. После запуска действия или клика вне панели она скрывается.

### Основные сценарии использования

1. Быстрый запуск AI-сервисов, рабочих сайтов и web-инструментов.
2. Открытие сайтов в нужном браузере, профиле, app mode, incognito или fullscreen.
3. Разделение кнопок по панелям: работа, AI, личное, медиа, скрипты.
4. Запуск локальных программ, файлов, папок, скриптов и команд.
5. Быстрый доступ к системным инструментам: поиск, скриншот, запись видео, калькулятор, проводник, загрузки, пипетка цвета, Quick Note.
6. Перенос готовой панели через `.aitebarpanel`.
7. Настройка положения панели на любом краю экрана и на нужном мониторе.

### Целевая аудитория

Основная аудитория: технически подкованные пользователи Windows 16-35 лет, компьютерные энтузиасты, гики, power users, разработчики, студенты, AI-энтузиасты и пользователи автоматизации.

Дополнительная аудитория:

- дизайнеры и контент-криэйторы;
- фрилансеры с несколькими клиентскими workflow;
- пользователи с несколькими браузерными профилями;
- люди, которые часто запускают локальные скрипты и служебные команды;
- пользователи, которым нужен быстрый launcher без визуального шума.

### Конкурентные преимущества

- Панель скрыта и не занимает место, пока не нужна.
- До 8 отдельных панелей-контекстов.
- Web-действия умеют запускаться в выбранном браузере и профиле.
- Есть app mode, incognito/private и fullscreen для web-ссылок.
- Есть ротация браузерных профилей.
- Есть встроенные быстрые инструменты Windows и Quick Note.
- Пользовательские панели можно импортировать и экспортировать.
- Настройки и данные хранятся локально в `%AppData%\Codebdbd\Aite Bar`.
- Нет найденных в коде аккаунтов, серверной части, облачной синхронизации или телеметрии.

### Ключевые функции

- Скрываемая edge-панель.
- Пользовательские кнопки действий.
- Панели-контексты.
- Глобальные hotkeys.
- Tray-интеграция.
- Drag-and-drop файлов, папок, `.url` и `http/https` ссылок.
- Запуск web-ссылок в браузерах и профилях.
- Запуск скриптов и команд.
- Импорт/экспорт `.aitebarpanel`.
- Quick Note и пипетка цвета.

### Ограничения

- Продукт рассчитан на Windows.
- README указывает Windows 10 / Windows 11.
- В коде нет подтвержденной облачной синхронизации.
- В коде нет подтвержденной системы аккаунтов.
- В коде нет встроенной AI-интеграции; AI-сервисы запускаются как web-ссылки.
- Выполнение команд требует подтверждения пользователя.
- Первая версия импорта не поддерживает отмену уже выполненного импорта, согласно USER_MANUAL.

### Технические особенности

- `.NET 8`, `net8.0-windows`.
- WPF UI.
- Windows Forms `NotifyIcon`.
- Win32 interop: global hotkeys, low-level mouse hook, `SendInput`.
- Локальные JSON-файлы настроек.
- Quick Note хранится как локальный Markdown-файл `QuickNote.md`.
- Пакеты панелей `.aitebarpanel` создаются как ZIP с `manifest.json`.
- Installer собирается через Inno Setup.

---

## 2. Целевая аудитория

### Core Segment: Windows Power Users

Люди, которые много работают в браузерах, сервисах, профилях, файлах и локальных утилитах. Они ценят скорость, контроль и минимальный UI.

Основная боль: нужные инструменты разбросаны по desktop, taskbar, закладкам, проводнику и терминалу.

Мотиватор: собрать личный command center, который появляется только когда нужен.

### Segment: AI и web-tool пользователи

Пользователи ChatGPT, Claude, Gemini, Perplexity, Notion, Linear, CRM, почты и других web-сервисов.

Основная боль: разные аккаунты и профили требуют ручного переключения.

Мотиватор: открывать каждый сервис в правильном браузерном окружении.

### Segment: Developers and Automation Users

Разработчики, студенты, админы и автоматизаторы.

Основная боль: скрипты, команды и папки проектов нужно искать или запускать вручную.

Мотиватор: запускать локальные автоматизации и dev-пути из одной панели.

### Segment: Creators and Designers

Дизайнеры, контент-криэйторы, авторы и стримеры.

Основная боль: часто нужны скриншот, запись, пипетка, папки, заметка и web-инструменты.

Мотиватор: держать рабочие инструменты рядом без перегруза рабочего стола.

---

## 3. УТП

### Основное УТП

AiteBar превращает край экрана Windows в персональную панель действий: сайты, AI-сервисы, браузерные профили, файлы, скрипты, команды и быстрые инструменты всегда рядом, но не занимают место на экране.

### Короткая формула

Скрываемый launcher для Windows power users, которым нужны web-инструменты, браузерные профили, скрипты и быстрые действия в одном месте.

### Обещание продукта

Меньше поиска. Меньше хаоса. Быстрее запуск того, что ты используешь каждый день.

---

## 4. Карта лендинга

| Экран | Цель | Сообщение | Эмоциональный эффект | Ключевое действие |
|---|---|---|---|---|
| 1. Hero | Мгновенно объяснить продукт | Край экрана становится командной панелью | "Это сделано для моего workflow" | Скачать или посмотреть возможности |
| 2. Проблема | Показать знакомый хаос | Инструменты разбросаны по Windows | Узнавание боли | Скролл к решению |
| 3. Решение | Показать центральную идею | Все действия собраны в скрываемой панели | Облегчение и интерес | Смотреть возможности |
| 4. Возможности | Доказать функциональность | Панели, web-профили, скрипты, инструменты | Уверенность | Выбрать релевантные фичи |
| 5. Как работает | Снять неопределенность | Настройка простая и понятная | "Я справлюсь" | Перейти к download |
| 6. Для кого | Помочь самоидентификации | Продукт для power users, AI, dev, creators | Принадлежность | Найти свой сценарий |
| 7. Технические преимущества | Убедить гиков | Локально, WPF, Win32, JSON, Markdown | Доверие | Проверить факты |
| 8. Сравнение | Показать отличие | Лучше ярлыков, закладок и Start menu | Рациональное подтверждение | Скачать |
| 9. FAQ | Снять возражения | Windows, локально, профили, скрипты | Спокойствие | Вернуться к CTA |
| 10. Финальный CTA | Закрыть страницу действием | Собери свой workflow | Готовность попробовать | Скачать AiteBar |

---

## 5. Полное описание экранов

## Экран 1 - Hero

### Цель блока

За 5-7 секунд объяснить, что такое AiteBar, для кого он нужен и почему стоит скачать.

### Контент

**Badge:** Windows launcher for power users

**H1:** Твой край экрана стал командной панелью

**Subtitle:** AiteBar - скрываемая панель быстрого доступа для Windows. Запускай AI-сервисы, сайты, браузерные профили, приложения, папки, скрипты и системные инструменты из одного компактного места.

**Microcopy:** Наведи курсор на край экрана. Выбери действие. Продолжай работать без поиска по desktop, taskbar и закладкам.

**Primary CTA:** Скачать AiteBar

**Secondary CTA:** Посмотреть возможности

**Trust row:**

- Windows 10 / 11
- Локальное хранение
- Hotkeys и tray
- Импорт панелей

### Элементы интерфейса

- Badge над заголовком.
- H1.
- Подзаголовок.
- Два CTA.
- Ряд коротких фактов.
- Главный визуал продукта.
- Небольшие floating labels: "AI", "Work", "Scripts", "Quick tools".

### Визуальная композиция

Desktop:

- Высота первого экрана: 88-92vh, чтобы был виден намек на следующий блок.
- Слева текстовая зона шириной 45%.
- Справа визуал панели AiteBar на краю условного desktop-экрана.
- Фон темный `#1A1A1C`, панели `#252526`, акцент `#007ACC`.
- CTA на одной линии; primary - синий, secondary - outline/ghost.

Mobile:

- Текст сверху, визуал ниже.
- H1 в 36-42px.
- CTA в колонку или две кнопки по ширине контейнера.
- Визуал не должен быть мелким; показать только фрагмент экрана с панелью.

### Иллюстрация

Главный визуал: стилизованный скриншот Windows desktop в темной теме. У верхнего или правого края экрана видна компактная панель AiteBar с иконками: плюс, поиск, скриншот, калькулятор, папка, AI-сервис, скрипт.

AI prompt:

```text
Dark modern Windows desktop interface mockup, a slim hidden edge launcher panel appearing from the right side of the screen, compact square icon buttons, blue accent color #007ACC, minimal WPF-style dark UI, no brand logos, no readable copyrighted app names, clean tech product landing page hero visual, sharp UI screenshot style, high contrast but muted, 16:10 composition
```

---

## Экран 2 - Проблема

### Цель блока

Показать пользователю, что AiteBar решает знакомый ежедневный хаос: слишком много мест, где приходится искать инструменты.

### Контент

**H2:** Твои инструменты разбросаны по всей Windows

**Intro:** Закладки в браузере. Ярлыки на desktop. Скрипты в папках. Рабочие сайты в разных профилях. Калькулятор, скриншот и загрузки где-то в системе.

**Problem cards:**

1. **Слишком много точек входа**  
   Один инструмент в taskbar, другой в закладках, третий в проводнике, четвертый в терминале.

2. **Профили браузера мешают скорости**  
   Рабочие и личные аккаунты требуют ручного выбора окружения.

3. **Автоматизация спрятана слишком глубоко**  
   Скрипты и команды полезны, но их неудобно запускать каждый день.

4. **Рабочий стол быстро превращается в шум**  
   Чем больше ярлыков, тем меньше они помогают.

**Bridge text:** AiteBar собирает эти действия в одну скрываемую панель, которая появляется только тогда, когда нужна.

### Элементы интерфейса

- H2.
- Вводный текст.
- 4 карточки проблем.
- Мини-схема "desktop / bookmarks / folders / terminal" сходится в один edge.

### Визуальная композиция

Desktop:

- Секция 760-860px высотой.
- Заголовок сверху слева.
- Ниже сетка 2x2 карточки.
- Справа или под карточками тонкая инфографика хаоса.

Mobile:

- Карточки в одну колонку.
- Инфографику заменить на компактную горизонтальную схему с прокруткой или убрать.

### Иллюстрация

Схематичная инфографика: четыре источника хаоса сходятся в одну синюю линию у края экрана.

AI prompt:

```text
Minimal dark UI infographic, scattered Windows workflow elements like bookmarks, desktop shortcuts, folders, terminal commands converging into one clean blue edge panel, no text, no logos, modern SaaS landing page style, muted dark palette, blue accent #007ACC
```

---

## Экран 3 - Решение

### Цель блока

Показать AiteBar как простой ответ: один край экрана для всех быстрых действий.

### Контент

**H2:** Один край экрана. Все быстрые действия.

**Text:** AiteBar живет у выбранного края экрана и остается скрытым, пока ты работаешь. Наведи курсор, нажми hotkey или открой из tray - и получишь доступ к своим панелям действий.

**Value points:**

- Запускай сайты, приложения, папки, скрипты и команды.
- Держи AI, работу, личное и автоматизацию в отдельных панелях.
- Открывай web-сервисы в нужном браузере и профиле.
- Переноси готовые панели через `.aitebarpanel`.

**CTA:** Смотреть функции

### Элементы интерфейса

- H2.
- Короткий абзац.
- 4 benefit rows с иконками.
- CTA.
- Большой mockup панели с переключением контекстов.

### Визуальная композиция

Desktop:

- Две колонки.
- Левая колонка - mockup панели.
- Правая колонка - текст и benefits.
- Панель визуально должна быть компактной, не похожей на dashboard.

Mobile:

- Сначала H2 и текст.
- Затем mockup.
- Потом benefits.

### Иллюстрация

Mockup панели с несколькими группами кнопок и активным названием панели: "AI", "Work", "Scripts".

AI prompt:

```text
Dark compact Windows edge toolbar UI mockup with several square icon buttons and small context indicators named AI, Work, Scripts, minimal blue accent, high fidelity product UI, no real app logos, no readable copyrighted brand names, WPF-inspired dark interface
```

---

## Экран 4 - Возможности

### Цель блока

Показать функциональную глубину продукта и дать пользователю найти свой сценарий.

### Контент

**H2:** Собери панель под свой workflow

**Intro:** AiteBar не навязывает один способ работы. Ты сам выбираешь, какие действия будут на панели и как они запускаются.

**Feature cards:**

1. **Пользовательские кнопки**  
   Ссылки, программы, файлы, папки, скрипты, команды и hotkeys в одном интерфейсе.

2. **Браузеры и профили**  
   Запускай сайты в Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi или Firefox.

3. **App mode и incognito**  
   Открывай web-инструменты как отдельные app-окна или в приватном режиме.

4. **Ротация профилей**  
   Переключай браузерные профили при повторном запуске кнопки.

5. **До 8 панелей**  
   Раздели работу, AI, личное, медиа и скрипты по отдельным контекстам.

6. **Быстрые инструменты**  
   Поиск, скриншот, запись видео, калькулятор, проводник, загрузки, пипетка и Quick Note.

7. **Drag-and-drop**  
   Добавляй файлы, папки, `.url` и прямые ссылки перетаскиванием.

8. **Импорт и экспорт**  
   Сохраняй активную панель в `.aitebarpanel` и переноси наборы между задачами или ПК.

### Элементы интерфейса

- H2.
- Intro.
- 8 feature cards.
- Иконка на каждой карточке.
- Optional tabs/filter: "Web", "System", "Automation", "Panels".

### Визуальная композиция

Desktop:

- Сетка 4x2 или 3x3 с последней карточкой шире.
- Карточки темные, radius 8px, без лишних градиентов.
- Иконки синие или нейтральные.

Mobile:

- Карточки одной колонкой.
- Каждая карточка: иконка слева, текст справа.

### Иллюстрация

Для этого экрана отдельная большая иллюстрация не нужна. Использовать иконки: link, browser, incognito, rotate, panels, tools, drag, package.

---

## Экран 5 - Как работает

### Цель блока

Снять страх настройки и показать понятный путь от установки до первого workflow.

### Контент

**H2:** Настрой один раз. Запускай каждый день.

**Steps:**

1. **Установи и запусти**  
   AiteBar появляется в tray и начинает работать в фоне.

2. **Выбери край экрана**  
   Настрой сторону панели, монитор, размер зоны активации и задержку появления.

3. **Добавь свои кнопки**  
   Создай действие вручную или перетащи файл, папку, `.url` или ссылку на панель.

4. **Разложи по панелям**  
   Сделай отдельные панели для работы, AI, личного, скриптов и быстрых инструментов.

5. **Запускай без поиска**  
   Наведи курсор на край экрана, нажми hotkey или открой панель из tray.

### Элементы интерфейса

- H2.
- Vertical timeline.
- 5 шагов.
- Маленький UI-preview рядом с каждым шагом.
- CTA после steps: "Скачать и настроить".

### Визуальная композиция

Desktop:

- Timeline слева, справа sticky preview mockup.
- При скролле preview может менять состояние: tray, settings, add button, panels, launched action.

Mobile:

- Timeline в одну колонку.
- Preview под каждым шагом или только один общий preview после списка.

### Иллюстрация

Серия мини-mockup:

- tray icon menu;
- settings window tabs;
- add button dialog;
- panel contexts;
- edge panel opened.

AI prompt:

```text
Five small dark UI mockups for a Windows utility onboarding sequence: tray menu, compact settings tabs, add action dialog, panel context selector, edge toolbar opened. Minimal WPF dark style, blue accent #007ACC, no logos, no readable brand text
```

---

## Экран 6 - Для кого подходит

### Цель блока

Дать пользователю увидеть себя в продукте и понять конкретный сценарий.

### Контент

**H2:** Для тех, кто собирает Windows под себя

**Audience cards:**

1. **AI-энтузиасты**  
   Держи ChatGPT, Claude, Gemini, Perplexity и другие web-сервисы рядом. Открывай их в нужном браузере и профиле.

2. **Разработчики и автоматизаторы**  
   Запускай папки проектов, скрипты, команды и dev-инструменты без ручного поиска.

3. **Power users**  
   Раздели рабочие, личные и системные действия по панелям. Управляй запуском через hotkeys и tray.

4. **Дизайнеры и creators**  
   Скриншот, запись экрана, пипетка цвета, загрузки, Quick Note и рабочие сайты в одном месте.

### Элементы интерфейса

- H2.
- 4 persona cards.
- В каждой карточке: иконка, сегмент, сценарий, "идеальная панель".

### Визуальная композиция

Desktop:

- 4 карточки в ряд или 2x2.
- Внутри каждой карточки мини-список из 3-4 элементов панели.

Mobile:

- Карточки в одну колонку.
- Списки внутри не длиннее 4 строк.

### Иллюстрация

Использовать карточки без отдельного большого изображения.

---

## Экран 7 - Технические преимущества

### Цель блока

Убедить техническую аудиторию, что продукт понятный, локальный и не пытается выглядеть больше, чем он есть.

### Контент

**H2:** Локально, нативно, без лишней магии

**Intro:** AiteBar - desktop-приложение для Windows на `.NET 8` и WPF. Оно работает с локальными настройками, системным tray, hotkeys и Win32-интеграцией.

**Tech facts:**

- `.NET 8`, `net8.0-windows`.
- WPF UI и Windows Forms `NotifyIcon`.
- Win32 interop для hotkeys, mouse hook и `SendInput`.
- Настройки в локальном `settings.json`.
- Quick Note в локальном `QuickNote.md`.
- Пакеты панелей `.aitebarpanel` как ZIP с `manifest.json`.
- Installer через Inno Setup.
- Локализация: `en`, `de`, `uk`, `ru`, `auto`.

**Privacy note:** В изученном коде не найдено аккаунтов, серверной части, облачной синхронизации или телеметрии.

### Элементы интерфейса

- H2.
- Intro.
- Code-like fact grid.
- Privacy note box.
- Small architecture diagram.

### Визуальная композиция

Desktop:

- Слева fact grid.
- Справа схема: Edge Panel -> ActionService -> Browser/System/Script/Files -> Local AppData.
- Privacy note отдельным блоком снизу.

Mobile:

- Сначала intro и privacy note.
- Потом fact grid.
- Схему упростить до вертикального flow.

### Иллюстрация

Architecture diagram:

```text
User -> Edge Panel -> ActionService
ActionService -> Browser / Files / Scripts / Commands / Quick Tools
SettingsService -> %AppData% JSON / Icons / QuickNote.md
PanelPackageService -> .aitebarpanel
```

AI prompt:

```text
Clean dark technical architecture diagram for a local Windows desktop utility, boxes connected by thin blue lines: Edge Panel, Action Service, Browser Profiles, Scripts, Files, Quick Tools, Local AppData JSON, QuickNote.md, .aitebarpanel package. Minimal UI, no logos, dark background, blue accent
```

---

## Экран 8 - Сравнение с альтернативами

### Цель блока

Рационально показать, почему AiteBar лучше закрывает конкретный workflow, чем ярлыки, закладки и обычный Start menu.

### Контент

**H2:** Не еще одна папка с ярлыками

| Сценарий | AiteBar | Типичные альтернативы | Преимущество |
|---|---|---|---|
| Быстрый запуск | Скрытая edge-панель | Desktop shortcuts, Start menu, taskbar | Не занимает место и появляется по требованию |
| Web-инструменты | Браузер, профиль, app mode, incognito, fullscreen | Обычные закладки | Контроль над окружением запуска |
| Рабочие контексты | До 8 панелей | Одна папка ярлыков | Инструменты разделены по задачам |
| Скрипты и команды | `.bat`, `.cmd`, `.ps1`, `.py`, команды | Терминал или ручной запуск | Автоматизации доступны в один клик |
| Быстрые инструменты | Скриншот, запись, калькулятор, проводник, загрузки, пипетка, заметка | Разные системные места | Частые действия собраны рядом |
| Перенос набора | Экспорт `.aitebarpanel` | Ручное копирование | Панель переносится одним файлом |
| Хранение данных | Локально в `%AppData%` | Зависит от продукта | Понятная локальная структура |

### Элементы интерфейса

- H2.
- Таблица сравнения.
- Highlight column для AiteBar.
- CTA под таблицей: "Попробовать AiteBar".

### Визуальная композиция

Desktop:

- Полноширинная таблица в контейнере 1100-1200px.
- Колонка AiteBar подсвечена тонкой синей линией или мягким фоном.

Mobile:

- Таблицу преобразовать в accordion cards по сценариям.
- В каждой карточке показать AiteBar / Альтернатива / Преимущество.

### Иллюстрация

Отдельное изображение не требуется.

---

## Экран 9 - FAQ

### Цель блока

Снять последние сомнения перед скачиванием.

### Контент

1. **AiteBar - это launcher?**  
   Да, но не классический launcher. Это скрываемая панель у края экрана с фокусом на быстрые действия, web-инструменты, браузерные профили, скрипты и панели-контексты.

2. **Какие версии Windows поддерживаются?**  
   В README указаны Windows 10 и Windows 11.

3. **Нужно ли создавать аккаунт?**  
   В изученном коде нет аккаунтов или серверной авторизации.

4. **Где хранятся настройки?**  
   В `%AppData%\Codebdbd\Aite Bar`. Основной файл настроек - `settings.json`.

5. **Можно ли запускать AI-сервисы?**  
   Да, как обычные web-ссылки. AiteBar не содержит собственной AI-интеграции, но может быстро открывать AI-сервисы в выбранном браузере и профиле.

6. **Какие браузеры поддержаны?**  
   В коде есть Chrome, Edge, Brave, Yandex, Opera, Opera GX, Vivaldi и Firefox.

7. **Можно ли запускать сайт как отдельное приложение?**  
   Да, для web-действий есть app mode.

8. **Есть ли incognito/private mode?**  
   Да, web-действия поддерживают приватный режим с учетом разных браузеров.

9. **Можно ли использовать несколько профилей браузера?**  
   Да. AiteBar умеет находить профили браузеров и запускать ссылку с выбранным профилем.

10. **Что такое ротация профилей?**  
    Это режим, при котором web-кнопка выбирает следующий профиль из списка при очередном запуске.

11. **Можно ли запускать скрипты?**  
    Да. Поддержаны `.bat`, `.cmd`, `.ps1` и `.py`.

12. **Можно ли запускать команды?**  
    Да, команда выполняется через `cmd.exe`, но перед запуском приложение показывает подтверждение.

13. **Можно ли перенести панель на другой компьютер?**  
    Да, текущую панель можно экспортировать в `.aitebarpanel` и импортировать в активную панель.

14. **Можно ли добавить кнопку перетаскиванием?**  
    Да. Поддержаны файлы, папки, `.url` и прямые `http/https` ссылки.

15. **Есть ли заметки?**  
    Да, встроенный Quick Note сохраняет заметку в локальный Markdown-файл `QuickNote.md`.

### Элементы интерфейса

- H2 "FAQ".
- Accordion list.
- 15 вопросов.
- CTA после списка.

### Визуальная композиция

Desktop:

- Две колонки accordion или одна широкая колонка 820px.
- Открыт первый вопрос.

Mobile:

- Одна колонка.
- Только один раскрытый пункт одновременно.

### Иллюстрация

Не требуется.

---

## Экран 10 - Финальный CTA

### Цель блока

Закрыть лендинг сильным повторением ценности и скачать.

### Контент

**H2:** Собери свой Windows workflow в одну панель

**Text:** AiteBar убирает лишние клики между тобой и твоими инструментами. Сайты, AI-сервисы, профили браузера, папки, скрипты, команды и быстрые Windows-действия - рядом, у края экрана.

**Primary CTA:** Скачать AiteBar

**Secondary CTA:** Читать руководство

**Final note:** Работает локально на Windows 10 / 11.

### Элементы интерфейса

- H2.
- Короткий абзац.
- Два CTA.
- Мини-ряд facts.
- Footer links: README, USER_MANUAL, changelog, support/donate if available on site.

### Визуальная композиция

Desktop:

- Centered block, max width 760px.
- Фон темный, с тонким контуром или full-width band.
- Без декоративных орбов и heavy gradients.

Mobile:

- CTA по ширине.
- Footer links в колонку.

### Иллюстрация

Фоновый subtle visual: edge panel silhouette или crop главного hero visual.

AI prompt:

```text
Minimal dark closing CTA background for a Windows utility landing page, subtle silhouette of a compact edge toolbar, blue accent glow only on UI lines, no text, no logos, professional software product style
```

---

## 6. Финальные тексты

### Главный заголовок

Твой край экрана стал командной панелью

### Главный подзаголовок

AiteBar - скрываемая панель быстрого доступа для Windows. Запускай AI-сервисы, сайты, браузерные профили, приложения, папки, скрипты и системные инструменты из одного компактного места.

### Короткое описание

AiteBar - скрываемая панель Windows для быстрого запуска сайтов, профилей браузера, приложений, файлов, скриптов и команд.

### Среднее описание

AiteBar превращает край экрана Windows в персональную панель быстрых действий. Запускайте сайты в нужном браузере и профиле, открывайте приложения, файлы, папки, скрипты, команды и системные инструменты. Разделяйте workflow по панелям, используйте hotkeys, tray-меню, drag-and-drop и импорт/экспорт готовых наборов.

### Полное описание

AiteBar - локальная desktop-утилита для Windows, которая добавляет скрываемую панель быстрого доступа у выбранного края экрана. Она появляется по наведению мыши, через tray или горячую клавишу и позволяет запускать пользовательские действия без поиска по рабочему столу, закладкам и меню "Пуск".

Пользователь может создавать кнопки для web-ссылок, программ, файлов, папок, скриптов, команд и сочетаний клавиш. Для web-действий доступны выбор браузера, профиль, app mode, incognito, fullscreen и ротация профилей. Это удобно для AI-сервисов, рабочих кабинетов, dev-инструментов и любых сайтов, которые нужно открывать в конкретном окружении.

AiteBar поддерживает до 8 панелей, drag-and-drop, импорт и экспорт `.aitebarpanel`, встроенные быстрые инструменты Windows, пипетку цвета и Quick Note. Настройки, иконки и заметки хранятся локально в профиле пользователя Windows.

---

## 7. Промпты для изображений

### Hero visual

```text
Dark modern Windows desktop interface mockup, a slim hidden edge launcher panel appearing from the right side of the screen, compact square icon buttons, blue accent color #007ACC, minimal WPF-style dark UI, no brand logos, no readable copyrighted app names, clean tech product landing page hero visual, sharp UI screenshot style, high contrast but muted, 16:10 composition
```

### Problem visual

```text
Minimal dark UI infographic, scattered Windows workflow elements like bookmarks, desktop shortcuts, folders, terminal commands converging into one clean blue edge panel, no text, no logos, modern SaaS landing page style, muted dark palette, blue accent #007ACC
```

### Feature visual

```text
Dark compact Windows edge toolbar UI mockup with several square icon buttons and small context indicators named AI, Work, Scripts, minimal blue accent, high fidelity product UI, no real app logos, no readable copyrighted brand names, WPF-inspired dark interface
```

### How it works visual

```text
Five small dark UI mockups for a Windows utility onboarding sequence: tray menu, compact settings tabs, add action dialog, panel context selector, edge toolbar opened. Minimal WPF dark style, blue accent #007ACC, no logos, no readable brand text
```

### Technical diagram visual

```text
Clean dark technical architecture diagram for a local Windows desktop utility, boxes connected by thin blue lines: Edge Panel, Action Service, Browser Profiles, Scripts, Files, Quick Tools, Local AppData JSON, QuickNote.md, .aitebarpanel package. Minimal UI, no logos, dark background, blue accent
```

### Final CTA visual

```text
Minimal dark closing CTA background for a Windows utility landing page, subtle silhouette of a compact edge toolbar, blue accent glow only on UI lines, no text, no logos, professional software product style
```

---

## 8. Спецификация дизайна

### Общий стиль

Современный, темный, технический, чистый. Не корпоративный SaaS в стиле "платформа для всего", а персональный utility-инструмент уровня Notion/Cursor/Linear по тону: уверенно, коротко, без воды.

### Цвета

- Background: `#1A1A1C`.
- Surface: `#252526`.
- Surface elevated: `#2D2D30`.
- Border: `#3A3A3D`.
- Primary text: `#E3E3E3`.
- Secondary text: `#A6A6AD`.
- Muted text: `#73737A`.
- Accent: `#007ACC`.
- Danger only if needed: `#FF5252`.

### Типографика

- Font: Inter, Segoe UI или аналогичный modern sans.
- H1 desktop: 64-76px, line-height 1.02-1.08.
- H1 mobile: 36-42px.
- H2 desktop: 40-52px.
- H2 mobile: 28-34px.
- Body: 16-18px.
- Small labels: 12-14px.
- Letter spacing: 0.

### Layout

- Max content width: 1160-1200px.
- Section padding desktop: 96-128px vertical.
- Section padding mobile: 56-72px vertical.
- Cards radius: 8px.
- Buttons radius: 6px.
- Avoid nested cards.
- Avoid decorative gradient orbs.

### Components

- Primary button: blue filled, white text, icon optional.
- Secondary button: transparent/outline, light text.
- Feature card: dark surface, border, icon, title, text.
- FAQ accordion: dark row with subtle border.
- Table: high contrast but muted, AiteBar column highlighted.
- Badge: compact pill, blue border or muted surface.

### Motion

- Hero panel visual: subtle slide-in from edge.
- Feature cards: fade-up on scroll, 80-120ms stagger.
- FAQ: smooth height transition.
- Buttons: hover lift 1-2px or background change.
- No heavy parallax.

### Mobile rules

- No horizontal overflow.
- Table becomes cards.
- Hero CTA stacks vertically if width below 420px.
- Mockups crop intelligently; do not shrink to unreadable size.
- Section order stays the same.

---

## 9. Frontend Specification

### Global HTML structure

```html
<main>
  <section id="hero"></section>
  <section id="problem"></section>
  <section id="solution"></section>
  <section id="features"></section>
  <section id="how-it-works"></section>
  <section id="audience"></section>
  <section id="technical"></section>
  <section id="comparison"></section>
  <section id="faq"></section>
  <section id="final-cta"></section>
</main>
```

### Header

Optional sticky header:

- Logo/name: AiteBar.
- Nav anchors: Возможности, Как работает, Для гиков, FAQ.
- CTA: Скачать.

Behavior:

- Desktop: sticky top, translucent dark background, blur optional.
- Mobile: compact top bar with CTA; nav can collapse.

### Hero HTML structure

```html
<section id="hero" class="hero">
  <div class="container hero__grid">
    <div class="hero__content">
      <div class="badge">Windows launcher for power users</div>
      <h1>Твой край экрана стал командной панелью</h1>
      <p class="lead">...</p>
      <p class="microcopy">...</p>
      <div class="actions">
        <a class="button button--primary" href="#download">Скачать AiteBar</a>
        <a class="button button--secondary" href="#features">Посмотреть возможности</a>
      </div>
      <ul class="trust-row">...</ul>
    </div>
    <div class="hero__visual"></div>
  </div>
</section>
```

Behavior:

- Primary CTA leads to download/release section or actual installer URL.
- Secondary CTA scrolls to `#features`.
- Visual animates slide-in on load.

### Problem section structure

Use `section-header`, `card-grid`, `problem-card`.

Desktop:

- 2x2 cards.

Mobile:

- Single column.

Hover:

- Border changes to `#4A4A50`.
- Icon color changes to accent.

### Solution section structure

Two-column layout:

- `solution__visual`
- `solution__content`
- `benefit-list`

CTA scrolls to features.

### Features section structure

```html
<section id="features">
  <div class="container">
    <header class="section-header"></header>
    <div class="feature-grid">
      <article class="feature-card"></article>
    </div>
  </div>
</section>
```

Recommended icons:

- Link.
- Browser/window.
- Shield/incognito.
- Rotate.
- Panels/layout.
- Wrench/tools.
- Mouse pointer/drag.
- Package/archive.

Hover:

- Slight background shift.
- Icon surface gets blue border.

### How it works section structure

Use ordered list:

```html
<ol class="steps">
  <li class="step">
    <span class="step__number">01</span>
    <h3>Установи и запусти</h3>
    <p>...</p>
  </li>
</ol>
```

Interactive optional:

- On desktop, highlight matching preview when a step enters viewport.
- On mobile, keep static.

### Audience section structure

Use persona cards:

- title;
- description;
- sample mini-list.

Mobile:

- Cards stacked.

### Technical section structure

Use:

- `tech-grid` for facts.
- `privacy-note`.
- `architecture-diagram`.

Code-like styling:

- Monospace for `.NET 8`, `%AppData%`, `.aitebarpanel`, `QuickNote.md`.

### Comparison section structure

Desktop:

- HTML table.

Mobile:

- Use CSS to transform rows into cards or render separate card list.

Hover:

- Row background `#2A2A2D`.

### FAQ section structure

Use accessible accordion:

```html
<button aria-expanded="false" aria-controls="faq-1">...</button>
<div id="faq-1" role="region">...</div>
```

Behavior:

- First item open by default.
- Mobile: one item open at a time.
- Desktop: multiple items may remain open if desired.

### Final CTA structure

Centered content:

- H2.
- paragraph.
- action row.
- facts row.

CTA behavior:

- Primary leads to download.
- Secondary leads to `docs/user-manual.md` or docs page if hosted.

### Button states

Primary:

- Default: background `#007ACC`, text `#FFFFFF`.
- Hover: background `#1687D9`.
- Active: background `#0069B0`, transform translateY(0).
- Focus: 2px outline `#66BFFF`.

Secondary:

- Default: border `#3A3A3D`, text `#E3E3E3`.
- Hover: border `#007ACC`, background `rgba(0, 122, 204, 0.12)`.
- Active: background `rgba(0, 122, 204, 0.18)`.

### Responsive breakpoints

- `>=1200px`: full desktop.
- `960-1199px`: narrower desktop, grids may be 3 columns.
- `720-959px`: tablet, hero becomes single column.
- `<720px`: mobile single column.
- `<420px`: CTA stacked full width.

---

## 10. SEO-материалы

### SEO Title

AiteBar - скрываемая панель быстрого доступа для Windows

### SEO Description

AiteBar - локальный Windows launcher у края экрана: сайты, браузерные профили, приложения, файлы, папки, скрипты, команды, hotkeys и быстрые инструменты.

### Meta Keywords

AiteBar, Windows launcher, панель быстрого доступа, edge panel, hotkeys, браузерные профили, запуск скриптов, Windows automation, быстрые инструменты, app mode, incognito, локальный launcher

### Open Graph Title

AiteBar - твой край экрана стал командной панелью

### Open Graph Description

Соберите свои сайты, AI-сервисы, браузерные профили, приложения, скрипты и команды в скрываемую панель быстрого доступа для Windows.

### Open Graph Image

Рекомендуемый visual: hero mockup с Windows desktop и выдвинутой edge-панелью AiteBar.

### URL slug

`/`

### H1

Твой край экрана стал командной панелью

### Recommended schema

Use `SoftwareApplication` schema:

- name: `AiteBar`
- operatingSystem: `Windows 10, Windows 11`
- applicationCategory: `UtilitiesApplication`
- offers: fill only if pricing/download data is available.

---

## Факты, найденные в коде

- Проект называется `AiteBar`.
- Основной проект таргетит `.NET 8` и `net8.0-windows`.
- UI построен на WPF.
- В проекте включены `UseWPF` и `UseWindowsForms`.
- Tray реализован через `System.Windows.Forms.NotifyIcon`.
- Приложение использует Win32 interop для hotkeys, mouse hook и `SendInput`.
- Главное окно без рамки, прозрачное, `Topmost`, не показывается в taskbar.
- Панель поддерживает стороны `Top`, `Bottom`, `Left`, `Right`.
- Панель можно показывать через активную зону у края экрана.
- Есть drag handle для переноса панели к другому краю и сохранения монитора.
- В настройках есть монитор, сторона панели, размер панели, зона активации и задержка появления.
- Есть глобальная горячая клавиша показа панели.
- Есть hotkeys для следующей панели, предыдущей панели и добавления кнопки.
- Есть до 8 панелей-контекстов; первая панель всегда включена.
- Пользовательские элементы имеют `ContextId`.
- Кнопки можно переупорядочивать внутри панели.
- Кнопки можно перемещать между панелями.
- Поддержанные типы действий: Web, Hotkey, Program, File, Folder, ScriptFile, Command.
- Web-действия запускаются через выбранный браузер.
- Поддержанные браузеры в модели: Chrome, Edge, Brave, Yandex, Opera, OperaGX, Vivaldi, Firefox.
- Для браузеров реализован поиск executable path через registry/common paths.
- Для браузеров реализован поиск профилей.
- Для Firefox профили читаются из `profiles.ini`.
- Для Chromium-подобных браузеров читаются profile directories и `Preferences`.
- Web-действие поддерживает app mode.
- Web-действие поддерживает incognito/private flags для разных браузеров.
- Web-действие поддерживает fullscreen через отправку F11 после запуска.
- Web-действие поддерживает profile rotation через `RotationProfilePaths` и `LastUsedProfile`.
- Hotkey-действие отправляет нажатия через `SendInput`.
- Program/File/Folder запускаются через `ProcessStartInfo` с `UseShellExecute = true`.
- ScriptFile поддерживает `.bat`, `.cmd`, `.ps1`, `.py`.
- `.ps1` запускается через `pwsh.exe` или `powershell.exe`.
- `.py` запускается через найденный `python.exe`.
- Command запускается через `cmd.exe /c` после подтверждения в dialog.
- Быстрые инструменты: поиск, скриншот, запись видео, калькулятор, проводник, загрузки, пипетка цвета, Quick Note.
- Скриншот запускается через `ms-screenclip:`.
- Запись видео запускается через `ms-screenclip:?type=recording`.
- Калькулятор запускается через `calc.exe`.
- Загрузки открываются через `shell:Downloads`.
- Пипетка делает снимок virtual screen, показывает magnifier и копирует HEX в clipboard.
- Quick Note сохраняет данные в `QuickNote.md`.
- Quick Note умеет сохранять conflict copy при внешних изменениях.
- Пользовательские настройки хранятся в `%AppData%\Codebdbd\Aite Bar`.
- Основной файл настроек: `settings.json`.
- Старый/совместимый файл кнопок: `custom_buttons.json`.
- Иконки хранятся в `%AppData%\Codebdbd\Aite Bar\Icons`.
- Лог ошибок: `error.log`.
- Импорт/экспорт панелей использует расширение `.aitebarpanel`.
- `.aitebarpanel` создается как ZIP-пакет с `manifest.json`.
- Экспорт сохраняет app name/version, panel metadata и элементы панели.
- Импорт добавляет элементы в текущую активную панель.
- Импорт копирует packaged images в локальное хранилище иконок.
- Manifest import проверяет версию формата, список элементов, типы действий и безопасность путей иконок.
- Встроены ресурсы Material Icons, Fluent System Icons и Font Awesome Brands.
- Локализация поддерживает `auto`, `en`, `de`, `uk`, `ru`.
- README указывает Windows 10 / Windows 11 как требования.
- Installer построен на Inno Setup.
- Publish идет в `artifacts\publish\win-x64`.
- Installer создается в `artifacts\installer`.
- Installer поддерживает задачу автозапуска через HKCU Run.
- В изученном коде не найдено серверной части, аккаунтов, облачной синхронизации или телеметрии.
