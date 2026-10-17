# Сокращение переходного контракта SecurityService

Дата: 17 октября 2026 года. Ветка: `codex/architecture-ip-search`. База: `b7504bd`.

## Выполненный участок

После сверки вызовов в производственном коде и тестах из ISecurityService и SecurityService удалены десять методов без потребителей. В конструкторе осталось восемь зависимостей вместо тринадцати. UI уже использует IUserAdministrationService и IUserListService; его поведение не изменено.

| Удалённый метод | Существующий путь для действующих сценариев |
| --- | --- |
| QueryUsersAsync | IUserListService.SearchUsersAsync с DTO и серверной областью |
| QueryRolesAsync | IRoleSearchService.SearchAsync |
| QueryPersonalsAsync | GetUserPersonalOptionsAsync / GetUserEditAsync для форм пользователей; поиск сотрудников использует собственный контракт |
| GetUserRolesAsync | IRoleAccessService.GetUserRolesAsync |
| GetRoleClaimsAsync / UpdateRoleClaimsAsync | IRoleAccessService.GetAsync / SaveAsync с проверкой версии |
| GetUserClaimsAsync / UpdateUserClaimsAsync | Потребителей нет; индивидуальные permission claims остаются в БД и не участвуют в вычислении прав |
| UpdatePersonalIpAddressesAsync | IPersonalIpService.UpdateAsync |
| IsIpAllowedForUserAsync | IIpAccessService.IsAllowedAsync в действующей проверке входа и подключения |

Удалены зависимости IReadRepository<ApplicationRole>, IReadRepository<Personal>, IPersonalIpService, IIpAccessService и IRoleAccessService. IReadRepository<ApplicationUser> остаётся для защищённого поиска и переходного GetUserByIdAsync. Регистрация Scrutor сохранена; новые обёртки не введены.

## Почему ISecurityService пока остаётся

Сверка выявила шесть дополнительных методов, которые нельзя удалить простым удалением объявлений:

| Метод | Оставшиеся потребители и следующий шаг |
| --- | --- |
| GetUserByIdAsync / UpdateUserAsync | Проверка запрета изменения ролей через редактирование профиля в AuthorizationTests. Перенести проверку на действующий DTO-сценарий с контролем сохранённых назначений |
| CreateUserAsync | Вызывается DTO-операцией и тестами. Тесты дополнительно проверяют откат создания при ошибке назначения роли; при сокращении нельзя потерять проверку обязательного IdentityResult и промежуточных сохранений |
| UpdateUserDetailsAsync | Вызывается SaveDetailsAsync и тестами версии/отката. Перевести тесты на SaveDetailsAsync, затем сделать helper закрытым |
| ToggleUserLockoutAsync | Вызывается SetUserLockoutAsync и тестами политики/отката/доставки. Перевести тесты на IUserListService, затем сделать helper закрытым |
| ResetPasswordAsync | Вызывается ResetPasswordFromDtoAsync и тестами политики/отката/доставки. Перевести тесты на DTO-команду, затем сделать helper закрытым |

Этот участок сокращает переходный API, но не завершает его удаление. Узкие контракты Application не расширены. Сохранённые тела методов, WriteAsync, advisory lock 74162002, повторные проверки доступа, обязательные IdentityResult, commit и обработка ошибок доставки аудита/уведомлений не изменены. Настройки, схема, миграции и рабочие данные не менялись.

## Проверки

Добавлены проверки отсутствия всех десяти методов в интерфейсе и реализации и состава зависимостей конструктора. DI проверяет обе узкие регистрации пользователей и их scoped lifetime в четырёх вариантах композиции. Существующие проверки доступа, политики аккаунтов, версии, конкурентных изменений, отката промежуточных сохранений Identity и ошибок после commit сохранены.

Сборка: 0 ошибок, 25 существовавших предупреждений Web. Полный Test-Guard.ps1 с PostgreSQL завершился с кодом 0: **4 711 unit + 20 954 integration = 25 665**. Временная изолированная БД удалена. Прирост 27 проверок: 11 границ и 16 DI.

Браузерные и нагрузочные проверки не выполнялись. Отсутствие потребителей подтверждено внутри репозитория; удаление публичных методов и параметров конструктора нарушает совместимость неизвестных внешних CLR-потребителей, если они существуют.

Следующий участок — перенести оставшиеся интеграционные проверки на действующие DTO-контракты, отдельно решить судьбу пути создания пользователя с назначением ролей и удалить ISecurityService. Этапы 1–2 остаются открытыми; размещение HTTP/workers, неопределённый исход commit и гарантированная доставка аудита также требуют дальнейшей работы.
