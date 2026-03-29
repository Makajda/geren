# Handoff / Контекст для новых сессий

Этот файл — короткая “памятка” для продолжения работы в репозитории после паузы/перезагрузки или новой сессии с агентом.

## TL;DR

- Репозиторий `geren` публикует NuGet-пакеты по git-тегам `vX.Y.Z` через GitHub Actions.
- Release notes генерируются автоматически и категоризируются по лейблам PR (см. `.github\release.yml`).
- Основные проекты:
  - `src\Geren.Client.Generator` — Roslyn source generator для клиента (читает opt-in `AdditionalFiles`).
  - `src\Geren.Client` — клиентская библиотека/пакет, который тянет генератор и MSBuild интеграцию.
  - `src\Geren.OpenApi.Server` — серверная часть вокруг OpenAPI (если используется).
  - `src\Geren.Server.Exporter` — консольный экспортёр, который строит `Compilation` из `*.csproj` и пишет наш JSON-спек в указанную папку (нужен для генерации клиентов, а не для сервера).

## Окружение и стиль

- Основная ОС разработки: Windows (пути в примерах через `\`).
- PowerShell: для цепочек команд используй `;` (не `&&`/`||`).
- Старайся, чтобы генерируемые тексты были детерминированными и в стиле репозитория (`.editorconfig`), без trailing whitespace.

## Экспортёр (`Geren.Server.Exporter`)

### Назначение

Экспортёр запускается отдельно и создаёт JSON-описание Minimal API эндпоинтов (не OpenAPI) из `Compilation`.

### Запуск (пример)

```powershell
dotnet run --project src\Geren.Server.Exporter\Geren.Server.Exporter.csproj -- `
  --project C:\\path\\to\\Server\\Server.csproj `
  --output-dir C:\\path\\to\\out `
  --configuration Release
```

Поддерживаемые ключи/алиасы описаны в `src\Geren.Server.Exporter\Common\Config.cs`.

### Важное ограничение MapGroup

Сейчас префиксы `MapGroup(...)` надёжно извлекаются только из compile-time константных строк. Избегай обёрток вида `MapGroup(Func<string>)`, `MapGroup(MethodBase)` и рефлексии для route-prefix, если хочешь корректный экспорт.

## Source generator (`Geren.Client.Generator`)

### AdditionalFiles: как отличать “наши” файлы

В проекте потребителя generator читает только те `AdditionalFiles`, которые явно opted-in через метадату, доступную компилятору:

- MSBuild item: `<AdditionalFiles Include="...\\file.json" Geren="openapi" />` (пример; значение зависит от формата).
- Для чтения `build_metadata.AdditionalFiles.Geren` в генераторе нужно, чтобы метадата была compiler-visible.

Пакетная/транзитивная настройка живёт в MSBuild `.props`/`.targets` (см. `Directory.Build.props` и `buildTransitive`-интеграции в соответствующих `.csproj`).

### Build properties из AnalyzerConfigOptionsProvider

Если генератор читает `build_property.*`, свойство должно быть compiler-visible (через `CompilerVisibleProperty`). Это обычно делается на стороне пакета, чтобы пользователю не приходилось добавлять строки в свой `.csproj`.

## Релизы и лейблы

### Как формируются release notes

Файл `.github\release.yml` задаёт:

- какие PR исключать (`skip-changelog`, а также стандартные `duplicate/invalid/question/wontfix`);
- категории (Breaking/Exporter/Client/Server/Features/Fixes/Documentation/Maintenance) по лейблам.

### Какие лейблы использовать

Стандартные GitHub лейблы остаются: `bug`, `documentation`, `enhancement`, `duplicate`, `invalid`, `question`, `wontfix`, `good first issue`, `help wanted`.

Дополнительные “наши” лейблы (нужно завести в GitHub один раз):

- `area:client` — клиентский генератор/клиентская часть
- `area:server` — серверная часть/OpenAPI server
- `area:exporter` — экспортёр
- `breaking` — breaking change
- `chore` — техдолг/рефактор
- `dependencies` — обновления зависимостей
- `skip-changelog` — PR не попадает в release notes

## Рекомендованный рабочий процесс (вкратце)

1. Создать ветку от `master`.
2. Сделать изменения небольшими порциями.
3. Открыть PR в `master`.
4. Повесить лейблы (area + type; опционально `skip-changelog`).
5. Мёржить только при зелёном CI.
6. Для публикации: поставить тег `vX.Y.Z` (workflow соберёт/упакует/опубликует).

## Куда смотреть, если “сломалось”

- Генератор не видит метадату/свойства: проверь `obj\\*\\*.GeneratedMSBuildEditorConfig.editorconfig` у потребителя (ищи `build_metadata.AdditionalFiles.*` и `build_property.*`).
- Релиз-ноты странно группируются: проверь лейблы PR и правила в `.github\\release.yml`.
- Экспортёр не видит префиксы групп: проверь, что `MapGroup("const")` без рефлексии/Func.
