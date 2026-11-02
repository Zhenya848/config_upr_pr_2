# Практическая работа №2 — вариант №4

## Этапы 1–3. Конфигурация, сбор данных и обход графа

Приложение читает настройки из JSON, загружает данные из Alpine `APKINDEX.tar.gz`
или тестового JSON-репозитория, затем строит транзитивный граф рекурсивным DFS.

Реализованы ограничение глубины `maxDepth`, фильтр `filterSubstring` и обработка
циклов: ребро, возвращающееся к активному узлу DFS, сохраняется как циклическое,
но повторное раскрытие узла не выполняется.

Примеры тестового графа:

```bash
./run.sh examples/config-a.json
./run.sh examples/config-c.json
./run.sh examples/config-h.json
./run.sh examples/config-filter.json
```

Пример реального пакета:

```bash
./run.sh examples/config-curl.json
```

Источник реального индекса: Alpine v3.24 main/x86_64.
