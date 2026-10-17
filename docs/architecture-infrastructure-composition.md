# Подготовка композиции Guard.Infrastructure

Дата: 17 октября 2026 года. База: `700596b`, ветка `codex/architecture-ip-search`.

Общая регистрация разделена перед выделением инфраструктурной сборки. Все реализации пока компилируются в Guard; новый проект на этом участке не создавался.

## Владельцы регистраций

| Метод | Состав |
| --- | --- |
| GuardServiceRegistration.AddGuardServices | Совместимая точка входа из Program, последовательно вызывает три регистрации |
| InfrastructureServiceRegistration.AddGuardInfrastructure | EF-фабрики, scoped-контекст и stores Identity, менеджеры и валидатор, аудит контекста, readers, UoW factory, реализации операций, уведомитель доступа, typed HTTP client, DataProtection, экспорт и workers |
| ApplicationServiceRegistration.AddGuardApplication | Три scoped-сервиса решений: IP, permissions, область подразделений |
| WebServiceRegistration.AddGuardWeb | Доверенные proxy, Blazor authentication, Identity UI, адаптер ICurrentSession, circuit, политики и обработчик авторизации |

Регистрация Application остаётся в host: независимой библиотеке не нужны дополнительные пакеты DI. Infrastructure получает сессию через ICurrentSession; реализация CurrentSessionAdapter принадлежит Web. HttpContextAccessor остаётся инфраструктурной зависимостью ASP.NET Identity. Сам метод регистрации Infrastructure больше не ссылается на Components или Blazor.

AccessCircuitHandler и AccessRefreshCoordinator перенесены из Core/Identity в Web/Identity без изменения содержимого и namespace. Scrutor использует InfrastructureServiceRegistration как маркер сборки; три реализации из Application зарегистрированы отдельно. Убрано неработавшее исключение DataAccessScopeService из сканирования Guard: тип уже находится в Application.

## Проверки

ServiceRegistrationTests проверяет общий метод и раздельную композицию с Web перед Infrastructure, в Development и Production. Для всех четырёх вариантов выполняются ValidateOnBuild/ValidateScopes и разрешение производственных интерфейсов. Добавлены проверки отсутствия Web-адаптеров в инфраструктурных descriptors, единственного scoped-адаптера сессии и состава регистрации Application.

Сохраняются проверки общего контекста обоих Identity stores внутри scope, разных контекстов между scope, scoped-интерцептора и независимой singleton-фабрики без него, единственности регистраций, 60-секундного typed client, alias журнала, настроек Identity, options и трёх workers без запуска.

- Сборка решения: 0 ошибок, 25 прежних предупреждений.
- Полный runner с PostgreSQL: **20 975 проверок**, на 178 больше предыдущего участка; код завершения 0. Изолированная временная БД удалена.
- Два circuit-файла перенесены без изменения содержимого; diff проверен.

Приложение, браузер, workers и внешние интеграции не запускались. Нагрузочные замеры не повторялись. Рабочие подключения, данные и миграции не изменены. Проверки descriptors не заменяют будущую проверку ссылок отдельной инфраструктурной сборки.

## Следующий участок

Выделить Guard.Infrastructure с проверкой полного набора зависимостей и ресурсов. Контекст, EF-конфигурации и миграции нужно переносить согласованно: ApplicationDbContext сейчас сканирует свою сборку, отдельный MigrationsAssembly не задан. Сохранить SQL-модель, доступность исторических миграций и транзакционные блокировки; перенос сам по себе не требует новой миграции схемы.

Перед переносом учесть оставшиеся адаптеры Radzen в QueryableExtensions и ExportFilters, зависимости технического логирования, startup/seeding и доступ тестов к internal-членам. Их наличие не означает разрешение переносить Core целиком без проверки. Цель остаётся прежней: четыре производственных проекта и два тестовых; этапы 1–2 ещё открыты. Неопределённый результат commit при потере соединения остаётся ранее описанным ограничением.
