# Запуск АльфаКредит

## Полное демо на Windows

Из корня backend-репозитория:

```powershell
.\scripts\start-demo.ps1
```

Скрипт закрепляет frontend на `986fc3cf2b755c7e0abb976454cb9d187c2566be`, ML на `7a293a39e54a38846326ea8cea57eaa27919e326`; backend собирается из текущего checkout. Устанавливать языковые среды на хост не нужно. Ветки и незакоммиченные файлы backend скрипт не меняет. Если `.demo/frontend` или `.demo/scoring` содержат изменения, подготовка остановится и попросит их сохранить.

Если политика PowerShell запрещает выполнение скрипта, выполни Git-команды вручную из следующего раздела. Системную политику менять не требуется.

## Ручной запуск: Linux, macOS или Windows

Из корня backend. Создай каталог `.demo` (`mkdir -p .demo` в bash или `New-Item -ItemType Directory -Force .demo` в PowerShell), затем:

```sh
git clone https://github.com/exsyyy/alfacredit-frontend.git .demo/frontend
git -C .demo/frontend checkout --detach 986fc3cf2b755c7e0abb976454cb9d187c2566be
git clone https://github.com/uandrosova07-arch/scoring.git .demo/scoring
git -C .demo/scoring checkout --detach 7a293a39e54a38846326ea8cea57eaa27919e326
docker compose -f compose.demo.yaml up -d --build --wait --wait-timeout 300
```

Не клонируй поверх существующих каталогов. Для повторного запуска достаточно последней команды; PowerShell-скрипт также можно запустить повторно.

Открыть **http://localhost:3000**. Вход: `client_good` / `Demo123!`, если `SEED_DEMO_PASSWORD` не изменён в `.env`. Swagger: http://localhost:18080/swagger; readiness: http://localhost:18080/health/ready; ML health: http://localhost:8000/health.

## Если не запускается

- Проверь `docker info` и `docker compose version`. Docker Desktop должен работать в режиме Linux containers.
- Первая сборка скачивает базовые образы и пакеты. При сетевой ошибке исправь доступ к Docker Hub, NuGet, npm или PyPI и повтори запуск. GPU не нужен.
- Состояние: `docker compose -f compose.demo.yaml ps`; журналы: `docker compose -f compose.demo.yaml logs --tail 100`.
- Если заняты порты, в PowerShell используй `-FrontendPort 13000 -ApiPort 18081 -ScoringPort 18001`. При ручном запуске выставь `DEMO_FRONTEND_PORT`, `DEMO_API_PORT`, `DEMO_SCORING_PORT` в окружении или `.env` и повтори сборку. Адрес API в браузере должен совпасть с портом, переданным в frontend при сборке.
- Дефолтный compose backend открывает только Swagger на 8080. Это другой режим, без кабинета; для полного продукта нужен `compose.demo.yaml`.
- Остановить с сохранением данных: `docker compose -f compose.demo.yaml stop`. Повторный запуск использует ту же БД. `down -v` удаляет её и для обычной остановки не нужен.

## Демо-сценарии

| Логин | Запрос | Ожидаемый результат |
|---|---|---|
| client_good | Оборотный кредит, 1,5 млн ₽, 12 мес. | Одобрение под 19,5 %, подписание и демонстрационная выдача |
| client_reduced | Оборотный кредит, 3 млн ₽, 24 мес. | Предложение 2,4 млн ₽ под 22,5 % с объяснением |
| client_bad | Экспресс-кредит, 500 тыс. ₽, 6 мес. | Отказ с причинами, без договора и выдачи |
| client_manual | Оборотный кредит, 8 млн ₽, 24 мес. | Рассмотрение менеджером, затем явно смоделированный ответ |
| client_new | Молодой бизнес | Отказ в подаче по ограничению возраста |

Пароль: `Demo123!`. Дата потребности должна быть будущей. Успешное подписание требует демо-кода из кабинета, реальные SMS не отправляются. Полный сценарий и API: [completion-flow.md](completion-flow.md), [api.md](api.md).
