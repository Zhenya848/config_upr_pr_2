# Практическая работа №2 — вариант №4

**Дисциплина:** Конфигурационное управление  
**Тема:** инструмент построения и визуализации графа зависимостей пакетов Alpine Linux (`apk`)  
**Язык:** C# / .NET 9  
**Репозиторий для реальных примеров:** [Alpine v3.24 / main / x86_64](https://dl-cdn.alpinelinux.org/alpine/v3.24/main/x86_64)

## 1. Назначение

Приложение читает сведения о пакетах из репозитория Alpine Linux либо из локального JSON-файла для тестирования, строит ориентированный граф транзитивных зависимостей и сохраняет визуализацию в D2 и SVG.

Зависимость изображается направленным ребром `пакет -> зависимость`. Для построения графа используется **рекурсивный DFS**. Повторное посещение активного узла фиксирует цикл и останавливает дальнейший спуск по этому ребру.

## 2. Соответствие варианту №4

| Требование | Реализация |
|---|---|
| Параметры задаются JSON-файлом | `AppConfig` и `ConfigLoader` |
| Имя пакета, репозиторий/тестовый файл, режим теста, версия | `examples/config-*.json` |
| Имя SVG, максимальная глубина, фильтр | `outputFileName`, `maxDepth`, `filterSubstring` |
| Вывод параметров ключ–значение | Выполняется при запуске |
| Формат пакетов Alpine Linux | Разбор `APKINDEX.tar.gz` и записей `APKINDEX` |
| Запрет на менеджеры пакетов и сторонние библиотеки при получении данных | Индекс скачивается через `HttpClient`, распаковывается средствами .NET и разбирается собственным парсером |
| Транзитивные зависимости, рекурсивный DFS | `DependencyGraphBuilder` |
| Ограничение глубины и фильтрация | `maxDepth` и `filterSubstring` |
| Циклические зависимости | Активный стек DFS и список `CycleEdges` |
| Тестовый репозиторий с именами A–Z | `JsonTestRepository` и `examples/test-repository.json` |
| Обратные зависимости | `ReverseDependencyFinder`, включается параметром `showReverseDependencies` |
| Диаграмма D2 и изображение SVG | `D2Renderer` и `SvgRenderer` |

Для исключения конфликта имён все C# пространства имён начинаются с `DependencyGraphApp` (например, `DependencyGraphApp.Models`). Имя класса модели графа — `DependencyGraph`.

## 3. Структура проекта

```text
config_upr_pr_2/
├── Variant4DependencyGraph.sln
├── README.md
├── .gitignore
├── run.sh / run.bat
├── test.sh / test.bat
├── artifacts/
├── src/
│   └── DependencyGraph/
│       ├── DependencyGraph.csproj
│       ├── Program.cs
│       ├── Configuration/
│       ├── Models/
│       ├── Repositories/
│       ├── Services/
│       └── Visualization/
├── tests/
│   └── DependencyGraph.Tests/
└── examples/
    ├── test-repository.json
    ├── config-a.json
    ├── config-c.json
    ├── config-h.json
    ├── config-filter.json
    ├── config-libcurl.json
    ├── config-curl.json
    └── config-libpsl.json
```

`bin/`, `obj/`, IDE-настройки и автоматически сгенерированные диаграммы не хранятся в Git. Диаграммы создаются при запуске.

## 4. Конфигурация

Пример `examples/config-a.json` для тестового режима:

```json
{
  "packageName": "A",
  "repositoryUrl": "",
  "useTestRepository": true,
  "testRepositoryPath": "test-repository.json",
  "version": "1.0",
  "outputFileName": "artifacts/graph-a.svg",
  "maxDepth": 10,
  "filterSubstring": "",
  "showReverseDependencies": true
}
```

| Поле | Тип | Описание / проверка |
|---|---|---|
| `packageName` | string | Обязательное имя анализируемого пакета |
| `repositoryUrl` | string | Абсолютный HTTP(S) URL каталога репозитория Alpine. Не требуется в тестовом режиме. Можно указать URL непосредственно до `APKINDEX.tar.gz` |
| `useTestRepository` | boolean | `true` — читать JSON-файл, `false` — скачивать APKINDEX |
| `testRepositoryPath` | string | Путь до JSON тестового репозитория. Относительный путь считается от каталога файла конфигурации |
| `version` | string | Обязательная точная версия корневого пакета |
| `outputFileName` | string | Путь результирующего SVG; расширение должно быть `.svg` |
| `maxDepth` | integer | Максимальная глубина, целое число от 0. Корень имеет глубину 0. Ограничение применяется к прямому и обратному обходам |
| `filterSubstring` | string | Подстрока имени пакета, чувствительность к регистру не учитывается; пустая строка отключает фильтр |
| `showReverseDependencies` | boolean | `true` — вывести пакеты, прямо или транзитивно зависящие от выбранного пакета |

Путь `outputFileName` разрешается относительно текущего рабочего каталога. Каталог для результата создаётся автоматически. Для запуска с другим JSON передайте путь через `--config` или `-c`.

## 5. Формат тестового репозитория

Тестовый файл содержит массив `packages`. Каждый пакет имеет имя из заглавных латинских букв, версию и массив прямых зависимостей:

```json
{
  "packages": [
    { "name": "A", "version": "1.0", "dependencies": ["B", "C"] },
    { "name": "B", "version": "1.0", "dependencies": ["D"] },
    { "name": "C", "version": "1.0", "dependencies": [] }
  ]
}
```

Встроенный пример дополнительно содержит ветвление, общую зависимость и цикл `A -> B -> D -> A`. Это позволяет проверить дедупликацию узлов и обработку циклов.

## 6. Получение данных Alpine APK

В реальном режиме приложение формирует адрес `repositoryUrl/APKINDEX.tar.gz` (если в URL уже есть `APKINDEX.tar.gz`, повторно имя не добавляется), скачивает индекс стандартным `HttpClient`, распаковывает gzip/tar средствами .NET и разбирает текстовые поля `P`, `V`, `D` и `p`.

- `P` — имя пакета;
- `V` — версия;
- `D` — зависимости;
- `p` — предоставляемые capabilities (виртуальные возможности пакета).

Для зависимостей с префиксами `so:`, `cmd:` и `pc:` каталог пытается найти пакет-провайдер по полю `p`. Версия корневого пакета выбирается строго по `version`; для транзитивной зависимости выбирается самая новая доступная запись, удовлетворяющая простому ограничению версии в индексе.

**Ограничения упрощённой модели:** это анализатор индекса, а не полный решатель APK. Он не воспроизводит все правила выбора провайдера, приоритеты репозиториев/архитектур и разрешение системных путей вроде `/bin/sh`. Неизвестная зависимость сохраняется в графе как отдельный узел без раскрытия. Для корректного анализа задавайте URL конкретного каталога репозитория и архитектуры. Если нужна обратная зависимость для пакета из `community`, одного индекса `main` недостаточно: необходимо анализировать индекс соответствующего репозитория.

## 7. Сборка и запуск

Требуется установленный **.NET 9 SDK**. В решении нет внешних NuGet-зависимостей.

Из корня проекта:

```bash
dotnet restore Variant4DependencyGraph.sln
dotnet build Variant4DependencyGraph.sln --configuration Release

# Тестовый граф A
./run.sh examples/config-a.json

# Реальные пакеты из Alpine v3.24 main/x86_64
./run.sh examples/config-libcurl.json
./run.sh examples/config-curl.json
./run.sh examples/config-libpsl.json
```

Windows PowerShell / CMD:

```bat
run.bat examples\config-libcurl.json
run.bat examples\config-curl.json
run.bat examples\config-libpsl.json
```

То же самое напрямую через `dotnet run`:

```bash
dotnet run --project src/DependencyGraph/DependencyGraph.csproj -- --config examples/config-libcurl.json
# другие реальные примеры: examples/config-curl.json и examples/config-libpsl.json
```

При запуске без аргументов используется `examples/config-a.json`. Для реального режима необходим доступ к сети, чтобы скачать `APKINDEX.tar.gz`.

## 8. Тестирование

Тесты реализованы как автономный .NET console harness без xUnit/NUnit и без внешних пакетов:

```bash
./test.sh
# или
 dotnet run --project tests/DependencyGraph.Tests/DependencyGraph.Tests.csproj
```

Проверяются: транзитивный DFS и цикл, максимальная глубина, фильтр, обратные зависимости, разбор APKINDEX и генерация D2/SVG. Код возврата `0` означает успешное прохождение всех проверок.

## 9. Примеры для тестового репозитория

Запустите три конфигурации из корня проекта:

```bash
./run.sh examples/config-a.json
./run.sh examples/config-c.json
./run.sh examples/config-h.json
```

Ожидаемые графы по тестовым данным:

- **A:** прямые зависимости `B`, `C`; далее `D`, `E`, `F`, `G`; цикл `D -> A`.
- **C:** прямые зависимости `E`, `F`; транзитивная зависимость `G`.
- **H:** цепочка `H -> B -> D/E`, а далее `D -> A` и `E -> G`; узлы `A`, `B`, `D`, `E`, `G`.

Для проверки фильтра и ограничения глубины выполните:

```bash
./run.sh examples/config-filter.json
```

## 10. Результаты консольного запуска для пакетов Alpine v3.24

Оба запуска выполнены для репозитория `main/x86_64`:

```text
https://dl-cdn.alpinelinux.org/alpine/v3.24/main/x86_64
```

Ниже приведён фактический консольный вывод программы для двух пакетов — `libcurl` и `curl`: перечень узлов с зависимостями, путь к сохранённым D2/SVG-файлам и обратные зависимости.

### 10.1. Пакет `libcurl`

Версия: `8.23.0-r0`. Конфигурация: [`examples/config-libcurl.json`](examples/config-libcurl.json).

```text
Граф зависимостей пакета libcurl:
  brotli-libs -> musl
  c-ares -> musl
  ca-certificates-bundle -> (нет зависимостей)
  libcrypto3 -> musl
  libcurl -> brotli-libs, c-ares, ca-certificates-bundle, libcrypto3, libidn2, libpsl, libssl3, musl, nghttp2-libs, zlib, zstd-libs
  libidn2 -> libunistring, musl
  libpsl -> libidn2, libunistring, musl
  libssl3 -> libcrypto3, musl
  libunistring -> musl
  musl -> (нет зависимостей)
  nghttp2-libs -> musl
  zlib -> musl
  zstd-libs -> musl

D2-файл сохранён: C:\Users\HONOR\Desktop\RTU_MIREA\CONFIG_UPR_practics\config_upr_pr_2\artifacts\graph-curl.d2
SVG-граф сохранён: C:\Users\HONOR\Desktop\RTU_MIREA\CONFIG_UPR_practics\config_upr_pr_2\artifacts\graph-curl.svg
Обратные зависимости пакета libcurl:
  asterisk
  acf-asterisk
  asterisk-dev
  asterisk-curl
  cargo
  cargo-auditable
  cargo-nextest
  py3-setuptools-rust
  rust-clippy
  rustfmt
  curl
  dns-root-hints
  bind
  bind-dev
  bind-plugins
  bind-dnssec-tools
  dpaste
  ix
  libmaxminddb
  spamassassin
  tpaste
  curl-dev
  xen-dev
  edbrowse
  elinks
  freeradius-rest
  freeswitch
  acf-freeswitch
  acf-freeswitch-vmail
  freeswitch-dev
  freeswitch-flite
  freeswitch-mariadb
  freeswitch-perl
  freeswitch-pgsql
  freeswitch-python3
  freeswitch-sangoma
  freeswitch-snmp
  freetdm
  freetdm-dev
  git
  aaudit-server
  alpine-git-mirror-syncd
  alpine-sdk
  aports-build
  etckeeper
  git-credential-libsecret
  git-daemon
  git-daemon-openrc
  git-diff-highlight
  git-fast-import
  git-p4
  git-gitk
  git-gitweb
  git-gui
  git-perl
  git-prompt
  git-scalar
  git-subtree
  gitolite
  perl-git
  git-cvs
  git-email
  perl-git-svn
  git-svn
  tig
  icecast
  kamailio-http_async
  kamailio-presence
  kamailio-utils
  llvm16
  llvm16-dev
  llvm20
  llvm20-dev
  postgresql17-dev
  postgresql18-dev
  llvm21
  llvm
  llvm21-dev
  llvm-dev
  spirv-llvm-translator-dev
  lua5.1-curl
  lua5.2-curl
  lua5.3-curl
  lua5.4-curl
  mariadb
  acf-mariadb
  mariadb-plugin-rocksdb
  mysql
  muon-wrap
  rng-tools-extra
  rsyslog-clickhouse
  rsyslog-elasticsearch
  rsyslog-http
  rsyslog-imdocker
  skytraq-datalogger
  strongswan
  dmvpn
  strongswan-logfile
  syslog-ng
  syslog-ng-add-contextual-data
  syslog-ng-amqp
  syslog-ng-dev
  syslog-ng-examples
  syslog-ng-geoip
  syslog-ng-graphite
  syslog-ng-http
  syslog-ng-json
  syslog-ng-map-value-pairs
  syslog-ng-python
  syslog-ng-redis
  syslog-ng-scl
  syslog-ng-sql
  syslog-ng-stardate
  syslog-ng-stomp
  syslog-ng-tags-parser
  syslog-ng-xml
  uwsgi-alarm_curl
  uwsgi-curl_cron
  xen
  xen-qemu
  xmlrpc-c-client
  xmlrpc-c-client++
  xmlrpc-c-dev
  xmlrpc-c-tools
```

Каталог: [libcurl — Alpine v3.24/main/x86_64](https://pkgs.alpinelinux.org/package/v3.24/main/x86_64/libcurl).

### 10.2. Пакет `curl`

Версия: `8.23.0-r0`. Конфигурация: [`examples/config-curl.json`](examples/config-curl.json).

```text
Граф зависимостей пакета curl:
  brotli-libs -> musl
  c-ares -> musl
  ca-certificates-bundle -> (нет зависимостей)
  curl -> libcurl, musl, zlib
  libcrypto3 -> musl
  libcurl -> brotli-libs, c-ares, ca-certificates-bundle, libcrypto3, libidn2, libpsl, libssl3, musl, nghttp2-libs, zlib, zstd-libs
  libidn2 -> libunistring, musl
  libpsl -> libidn2, libunistring, musl
  libssl3 -> libcrypto3, musl
  libunistring -> musl
  musl -> (нет зависимостей)
  nghttp2-libs -> musl
  zlib -> musl
  zstd-libs -> musl

D2-файл сохранён: C:\Users\HONOR\Desktop\RTU_MIREA\CONFIG_UPR_practics\config_upr_pr_2\artifacts\graph-curl.d2
SVG-граф сохранён: C:\Users\HONOR\Desktop\RTU_MIREA\CONFIG_UPR_practics\config_upr_pr_2\artifacts\graph-curl.svg
Обратные зависимости пакета curl:
  dns-root-hints
  bind
  bind-dev
  bind-plugins
  bind-dnssec-tools
  dpaste
  ix
  libmaxminddb
  spamassassin
  tpaste
```

Каталог: [curl — Alpine v3.24/main/x86_64](https://pkgs.alpinelinux.org/package/v3.24/main/x86_64/curl).

### Дополнительная конфигурация `libpsl`

Для запуска анализа пакета `libpsl` подготовлена конфигурация [`examples/config-libpsl.json`](examples/config-libpsl.json). Запустить её можно той же командой, что и остальные конфигурации:

```bash
./run.sh examples/config-libpsl.json
```

### Повторный запуск

Запускайте из корня проекта:

```bash
./run.sh examples/config-libcurl.json
./run.sh examples/config-curl.json
./run.sh examples/config-libpsl.json
```

В Windows PowerShell / CMD:

```bat
run.bat examples\config-libcurl.json
run.bat examples\config-curl.json
run.bat examples\config-libpsl.json
```

В конфигурациях заданы параметры глубины обхода и включён вывод обратных зависимостей (`showReverseDependencies = true`).

## 11. Сравнение со штатным инструментом Alpine

Штатная команда `apk dot` формирует граф зависимостей в формате Graphviz DOT. Для сравнения в контейнере Alpine той же ветки и архитектуры можно выполнить:

```sh
apk update
apk add graphviz
apk dot libcurl > /work/libcurl-apk.dot
dot -Tsvg /work/libcurl-apk.dot -o /work/libcurl-apk.svg
```

Сравнивайте состав вершин и рёбер, прямые и транзитивные зависимости, обработку виртуальных зависимостей. D2/SVG, созданные проектом, и DOT/SVG, созданные `apk dot`, могут отличаться расположением блоков; это не означает, что сами связи различаются. При расхождении списков сначала проверьте, что используются одна ветка, одна архитектура и одна версия индекса. Проект читает только указанный APKINDEX и использует упрощённое разрешение providers, а штатный APK применяет собственные правила выбора провайдеров.

## 12. История коммитов по пяти этапам

История Git в этом проекте разделена на пять последовательных коммитов. Каждый коммит добавляет очередной этап, а пятый коммит содержит итоговый вариант программы — тот же исходный C#-код, который был в предоставленном проекте; дополнительные изменения внесены в README и примеры конфигураций.

```text
feat(config): add JSON configuration and validation
feat(repository): parse Alpine APKINDEX metadata
feat(graph): build transitive graph with recursive DFS
feat(reverse): calculate reverse dependencies
feat(visualization): export D2 and SVG diagrams
```

Проверить историю можно командой:

```bash
git log --oneline --reverse
```

Для сдачи репозиторий должен быть публичным, содержать `README.md`, `.gitignore`, `src/`, `tests/`, скрипты запуска и пять отдельных коммитов с сообщениями в стиле Conventional Commits.
