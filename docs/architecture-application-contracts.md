# Реестр контрактов будущего Guard.Application

Актуальное дополнение: [Guard.Application физически выделен](architecture-application-assembly.md). 35 интерфейсов, все DTO, три независимых сервиса доступа и необходимые правила теперь принадлежат библиотеке со ссылками только на Domain/BCL. Семь legacy-интерфейсов и EF/Identity-реализации остаются в Guard. Проверяются скомпилированные зависимости, сигнатуры DTO и производственный DI; полный runner — 20 797 проверок. Следующий участок — состав Infrastructure и зависимости от Web. Ниже сохранена история.

Актуальное дополнение: [Guard.Domain уже выделен](architecture-domain-assembly.md). Перечисления теперь принадлежат независимой сборке, полные имена сохранены; проверка 35 прикладных контрактов прошла после переноса. Следующий участок — состав и выделение Guard.Application: контракты, DTO и независимые правила, с сохранением EF/Identity-реализаций в Guard до Infrastructure. Ниже сохранена история.

Актуальное дополнение: [IDataAccessScopeReader и независимая область подразделений](architecture-data-access-scope-port.md). Автоматически проверяются 35 независимых интерфейсов. DataAccessScopeService и SubdivisionAccessPath не зависят от EF, конкретное чтение принадлежит Infrastructure. Далее — обратная навигация Personal.User перед выделением Domain. Ниже сохранена история.

Актуальное дополнение: [IPermissionReader и PermissionAccessData](architecture-permission-port.md) отделяют EF-чтение от проверки сессии и ролевых полномочий. Теперь автоматически проверяются 34 независимых интерфейса. PermissionService, каталог и UserAccessSnapshot сохраняют прикладные правила; конкретный reader относится к Infrastructure. Проверки: сборка без ошибок, runner с PostgreSQL — 20 402. Далее — данные для DataAccessScopeService. Ниже сохранена история.

Актуальное дополнение: [IIpAccessReader и IpAccessSubject](architecture-ip-access-port.md) отделяют данные хранения от правила допуска. Теперь проверяются 33 независимых интерфейса: прежние 32 и порт IP-чтения. IpAccessService не зависит от EF; конкретный reader относится к Infrastructure. IpAddressRule независим от ASP.NET Identity, несмотря на namespace. Проверки: сборка успешна, runner с PostgreSQL — 20 364. Следующий участок — данные для PermissionService.

Актуальная [сверка этапов 1–2](architecture-stage-readiness.md): 32 независимых контракта проверяются автоматически, четыре интерфейса синхронизации/API и два enum вынесены из файлов реализаций/сущностей. Прежние пункты о фильтрах и EntityWriteDTOs ниже описывают исторический аудит: эти зависимости уже устранены. Публичные сигнатуры подготовлены; следующая работа — конкретные возможности хранения, начиная с IP-допуска. Этапы ещё открыты. Проверки: сборка успешна, runner с PostgreSQL — 20 318.

Актуальное дополнение: [OwnPasswordChangeStatus.SignInRequired](architecture-own-password-completion.md) описывает подтверждённую смену пароля с ошибкой обновления cookie; прежние значения статусов сохранены. Ошибка хранилища не считается подтверждённым сохранением. Следующий участок — независимая текущая сессия и автор аудита. История ниже сохранена.

Актуальное дополнение: [аудит исполнителя, контекстов и завершения записи](architecture-write-operation-audit.md) зафиксировал остаток этапа 2 и порядок проверок. Следующий контракт — подтверждённая смена своего пароля при ошибке cookie/аудита после записи; затем независимая текущая сессия и отделение display от автора. Поставка документальная, новых запусков тестов нет. История ниже сохранена.

Актуальное дополнение: [маппинг сущностей вынесен из EntityWriteDTOs в серверный EntityWriteMapping](architecture-write-mapping.md). Три DTO записи больше не зависят от Entities; Web не использует маппинг. Сборка успешна, runner: 18 699 проверок. Следующая поставка — аудит доверенного исполнителя, контекстов и частичных результатов составных записей. Перед переносом сборок остаются также размещение независимых enum/контрактов и Personal.User. Этапы 1–2 ещё не закрыты. Следующие абзацы сохраняют историю.

Актуальное дополнение: [независимые фильтры IP, подразделений и классификаторов](architecture-domain-search-filters.md) реализованы; Components больше не обращается к SearchQuery. Сборка: 0 ошибок, 25 предупреждений; runner: 18 525 проверок. Следующий участок — маппинг сущностей в EntityWriteDTOs, затем аудит исполнителя и атомарности. Этапы 1–2 ещё не закрыты, проекты не выделены. Ниже сохранена история предыдущих поставок.

Обновление после реализации: [фильтры Users/Roles/Logs отделены от query-построителей](architecture-search-filter-contracts.md). Web использует UserSearchFilter, RoleSearchFilter, LogSearchFilter; серверные ValidateFilter делегируют им проверку. Строки таблицы ниже для этих трёх областей и пункт 1 следующей реализации описывают исходный аудит и уже выполненную работу. Следующий участок — IP/подразделения/классификаторы. Проверки текущей поставки приведены в связанном отчёте.

Дата: 16 октября 2026 года. Ветка: `codex/architecture-ip-search`. Проверенная база: `e8a152e`.

Это актуальный срез после очистки EntityListQuery. Он уточняет размещение кода по утверждённому плану; физическое разделение проектов и закрытие этапов 1–2 ещё не выполнены. В этой поставке меняется только документация.

## Охват и результат

Проверены объявления всех 39 публичных интерфейсов в Core/6-Services, включая интерфейсы внутри файлов реализаций и Exports/Administrator. Они распределены ниже без пропусков: 24 контракта операций для UI, 7 служебных контрактов, 1 порт внешнего API и 7 legacy-контрактов. Отдельно рассмотрены DTO, статические правила фильтрации, репозитории/UoW и технический адаптер Account.

Независимая сигнатура не доказывает готовность реализации к переносу. Например, ISynchronizationService возвращает DTO, но реализация напрямую использует EF, PostgreSQL locks и runner. Аналогично интерфейс IOwnPasswordService независим, а реализация использует Identity и HTTP/cookie.

## Контракты операций для UI — 24

Целевое место перечисленных интерфейсов и их запросов/результатов — Application. Сущности, EF, Identity и Serilog не должны попадать в публичную модель этих операций. Текущие реализации разбираются отдельно на этапах 2–4, без переноса Services целиком.

| Область | Интерфейсы | Контракты и условия переноса |
| --- | --- | --- |
| Сотрудники — 5 | IPersonalSearchService, IPersonalDetailsService, IPersonalEditorService, IPersonalStatusService, IPersonalIpService | PersonalSearchRequest/Result, PersonalFilter, PersonalDetailsDto, PersonalEditorDto, Create/EditPersonalDto, PersonalIpAssignmentsDto. Сохранить Read/ReadDetails/ReadArchive, область, версии и ограничения API-управления. EntityWriteDTOs пока содержит методы с сущностями |
| Справочники и IP — 3 | IIpAddressManagementService, ISubdivisionManagementService, IClassifierManagementService | SearchRequest/Result, DetailsDto, команды и варианты выбора. Сохранить версии подразделений/классификаторов; не добавлять версию IP попутно. RebuildHierarchyAndPathsAsync остаётся защищённой серверной операцией |
| Пользователи — 2 | IUserListService, IUserAdministrationService | UserSearchRequest/Result, UserDto, UserPersonalOption, UserEditData/EditUserDto, CreateUserCommand, ResetUserPasswordCommand. IdentityResult и ApplicationUser остаются внутри адаптера |
| Роли — 2 | IRoleSearchService, IRoleAccessService | RoleSearchRequest/Result, RoleListDto, RoleEditDto, UserRoleAssignmentsDto, PermissionTransitionReport. InitializeAdministratorAsync — операция инициализации, а не действие обычного UI; сохранить Root и транзакцию |
| Журналы — 2 | ILogSearchService, ILogConfigurationService | LogSearchRequest/Result, LogEntryDto, LogConfigurationDto/ApplicationLogLevel. Сохранить отдельную БД логов и смену конфигурации; LogEntry не переносить как DTO |
| Отображение пользователя — 1 | ICurrentUserDisplayService | CurrentUserDisplayDto/CurrentUserSubdivisionDto. Модель отображения не является доверенным исполнителем операции |
| Политика аккаунтов — 3 | IAccountPolicyService, IPasswordRequirementsService, IAccountPolicyAdministrationService | AccountState, требования пароля, политика входа/срока пароля, настройки и команда сохранения. Проверки входа и служебная активность используются также middleware/worker; нельзя объявить весь IAccountPolicyService Root-only |
| Локальный Account — 2 | ILocalAccountService, IOwnPasswordService | LocalSignInStatus, OwnPasswordState и результат смены пароля. Вход/выход/свой пароль; 2FA запрещает новый вход, данные 2FA сохраняются. HTTP, cookie, antiforgery и security stamp остаются в адаптерах |
| Синхронизация — 3 | ISynchronizationService, IDirectoryManagementService, IApiDiagnosticService | SynchronizationOverview, DirectoryActuality, SynchronizationSettingsDto/RunDto, ApiDiagnosticResult. Разделить интерфейсы и файлы реализаций; сохранить версии, очередь, recovery и смысл настроек. Пароль API передаётся только в операцию сохранения; не появляется в DTO чтения |
| Экспорт — 1 | IDataExportService | ExportRequest/Filter, задания, архив, preview, retention. Сохранить сериализацию старых заданий, проверку прав на выдачу и отмену. ExportOptions и внутренние модели документа не переносить автоматически вместе с публичными DTO |

## Служебные контракты — 7, и порт интеграции — 1

| Интерфейс | Цель и необходимое ограничение |
| --- | --- |
| ICurrentSession (заменил ICurrentUserService) | Независимый порт идентичности сеанса UserId/SecurityStamp; Web-адаптер читает Blazor/HTTP. Полномочия подтверждает PermissionService, а legacy-аудит читает автора при сохранении. Display больше не хранит автора. [Реализация и проверки](architecture-current-session.md) |
| IPermissionService | Application: актуальные полномочия и проверки. UserAccessSnapshot.Has зависит от PermissionCatalog, который должен перемещаться вместе с независимыми правилами permissions |
| IDataAccessScopeService | Application: серверная область. DataAccessScope содержит HashSet<Guid>; её изменяемость и единый снимок проверить в этапе 2. Пустая область не становится полным доступом |
| IIpManagementAccessService | Application: разрешение чтения/записи и область IP. IpManagementScope — независимые значения, без доверия к данным UI |
| IIpAccessService | Application: проверка допуска userId/clientIp. Реализация загрузки привязок — Infrastructure, middleware/SignInManager — адаптеры хоста |
| IAuditService | Application: AuditEventType и строки. Serilog/TLS/CEF и освобождение ресурсов — Infrastructure; аудит безопасности не объединять с техническим журналом |
| IAccessChangeNotifier | Порт уведомления об изменении доступа. Func<Task> — callback подписки, не делегат IQueryable. Реализация хранит подписчиков в памяти процесса; связанный circuit остаётся Web. При разделении зафиксировать владельца контракта по фактическим потребителям Application/Web |
| IDirectoryApiClient | Порт интеграции для Application после выделения из DirectoryApiClient.cs. Сигнатуры не содержат HttpClient/IOptions. Реализация и DirectoryApiOptions — Infrastructure; транспортные JSON-обёртки не становятся публичным API UI |

## Legacy-контракты — 7

| Интерфейс | Причина оставить в Infrastructure на переходе |
| --- | --- |
| IPersonalService | QueryPersonalsAsync и операции с Personal/Subdivision; узкие контракты уже выделены |
| IIpAddressService | Query-методы и сущности наряду с унаследованным IIpAddressManagementService |
| ISubdivisionService | QuerySubdivisionsAsync, полные Subdivision/Classifier; наследует независимый management-контракт |
| IClassifierService | QueryClassifiersAsync и полные Classifier; наследует независимый management-контракт |
| ISecurityService | Четыре query-делегата, ApplicationUser и IdentityResult; наследует IUserAdministrationService/IUserListService |
| IApplicationRoleService | QueryRolesAsync и ApplicationRole; поиск выделен в IRoleSearchService |
| ILogService | GetLogsAsync с IQueryable<LogEntry>; поиск выделен в ILogSearchService |

Поиск по Components не нашёл потребителей этих семи интерфейсов. Это не разрешает их немедленно удалить: SecurityService зависит от IIpAddressService, реализации/DI и тесты продолжают использовать старый API. Перед удалением составить карту конкретных методов и заменить либо сохранить необходимые внутренние вызовы. Унаследованные узкие интерфейсы должны сохранять регистрацию той же реализации; отдельно учесть явную регистрацию ILogService/ILogSearchService.

IReadRepository<T>, IGenericRepository<T>, IBasicRepository<T>, IIpAddressRepository, IUnitOfWork и IUnitOfWorkFactory не входят в число 39. Текущий API раскрывает запросы/сущности/SQL и остаётся внутренним механизмом Infrastructure. Фабрика на операцию полезна, но сама по себе не делает эти контракты готовыми портами Application. Новые порты вводить по потребности конкретной операции.

## DTO и сопутствующие типы: скрытые зависимости

| Типы / файл | Фактическая зависимость и решение |
| --- | --- |
| EntityWriteDTOs.cs | PersonalFieldsDto, SubdivisionFieldsDto, ClassifierFieldsDto имеют From(entity)/ApplyTo(entity). Типы полей независимы, публичные методы связывают DTO с сущностями. Перед выделением Application вынести маппинг на сторону записи/проекций; сохранить состав полей. Особенно важна транзитивная Personal.User → ApplicationUser |
| ApiSnapshot.cs | ApiSubdivision.Fields и ApiPersonal.Fields создают DTO, а не сущности. using Guard.Core.Entities нужен для DataManagementMode в DirectoryActuality; наличие using не доказывает зависимость от Identity. ApiSnapshot — внутренний снимок интеграции, не модель страницы |
| SynchronizationDtos.cs | DataManagementMode и SynchronizationStatus объявлены в Core/1-Entities/Concrates/ApiSynchronization.cs. Выделить независимые enum вместе с состояниями синхронизации; сохранить числовые значения и JSON |
| UserAccessSnapshot.cs | Содержит поведение Has через PermissionCatalog. Permissions.cs и независимые PermissionDefinition/PermissionCatalog из Core/Identity должны входить в замыкание Application; их namespace не означает зависимости от ASP.NET Identity |
| RoleEditDto и DTO пользователей | Хранят строки permissions/ролей, ID и значения. Наличие Identity в имени IdentityDTOs.cs не делает их моделями Identity. Не переносить ApplicationUser/ApplicationRole вместе с ними |
| ExportContracts.cs | Смешаны внешний контракт сервиса, BCL JsonElement, независимые модели документа и настройки файлов/очереди. Разделить файл по владельцам; JsonElement допустим в контракте, поскольку сохраняет совместимость заданий и не зависит от Web/EF |
| Status, DataViewMode, SubdivisionHierarchyMode, ExportArea/Format/JobState, AuditEventType | Сохранить зависимости DTO от независимых enum; предметные состояния — Domain, прикладные режимы — Application. Не переносить Core/0-Enums целиком по имени папки |
| EntityRuleException, EntityConflictException, AccountPolicyConflictException, UserSecurityException | Независимые ошибки операций — Application; сохранить обработку и русские сообщения. PostgreSQL/Identity-ошибки преобразует инфраструктурный адаптер |

## Остаточные зависимости фильтров Web

| Потребитель в Components | Текущий серверный тип | Решение до разнесения сборок |
| --- | --- | --- |
| Pages/IpAddresses/IpListQuery | IpSearchQuery.ValidateFilter | Вынести проверку независимого фильтра из построителя выражений; оставить IQueryable/Predicate/Sort в Infrastructure |
| Pages/Subdivisions/SubdivisionListQuery | SubdivisionSearchQuery.Fields/ValidateFilter | Независимое описание полей и проверка — Application; навигации, выражения, видимость и сортировка — Infrastructure |
| Pages/Classifiers/ClassifierListQuery | ClassifierSearchQuery.Fields/ValidateFilter | Такое же разделение; сохранить числовые, bool и UTC-условия |
| Pages/Users/UserListQuery | UserSearchQuery.Fields/ValidateFilter | Убрать зависимость проверки от RestrictedSearchQuery<ApplicationUser>; UserFilterFields остаётся в Web |
| Pages/Roles/Index.razor.cs | RoleSearchQuery.Fields/ValidateFilter | Убрать зависимость проверки от RestrictedSearchQuery<ApplicationRole> |
| Pages/Logs/Index.razor.cs | LogSearchQuery.Fields/ValidateFilter | Убрать зависимость проверки от RestrictedSearchQuery<LogEntry>; сохранить DateTime и выбор нескольких уровней |
| Pages/ExportArchive/Archive.razor.cs | ExportArchiveQuery.Fields/ValidateFilter | Уже независимая проверка через ExportFilterValidation и явный словарь типов; может идти в Application без IQueryable |
| Pages/Personals/Index.razor.cs | PersonalFilterValidation.Validate | Уже независима; допустимый вызов Application. Не заменять строковый PersonalFilter на ExportFilter с изменением сохранённых заданий |

AdministrationListQuery, EntityListQuery.Snapshot, ExportFilterCapture и адаптеры календарных дат остаются Web. Валидация на сервере обязательна даже при успешной проверке в Web. Нельзя просто удалить проверку при нажатии «Применить»: это меняет момент ошибки и принятие снимка фильтра.

У ExportArchive есть ещё вызовы ExportSchema.Title из разметки. ExportSchema не содержит EF, но совмещает названия областей, permissions и форматирование документов; при переносе определить владельцев этих функций, не переносить UI в зависимость от файлового экспорта.

Отдельно остаётся IdentityRevalidatingAuthenticationStateProvider<TUser> с UserManager<TUser>: это технический адаптер authentication state, а не прикладной контракт. Его место определить вместе с Identity/DI при переносе Web. Наличие этого адаптера не требует вернуть удалённые сценарии регистрации/2FA.

## Следующая реализация и условия готовности

1. Начать с Users/Roles/Logs: выделить независимые описания полей и валидаторы ExportFilter, переключить Web и сервер на общие правила. Сохранить текущие различия null, пустых строк, In/NotIn, UTC и сложности; не подменять их похожим валидатором без сравнительных тестов.
2. Тем же способом отделить проверки IP, подразделений и классификаторов. ExportArchiveQuery и PersonalFilterValidation использовать как уже независимые части, без переписывания ради единообразия.
3. Вынести From/ApplyTo из DTO и разложить совместно объявленные типы/интерфейсы по будущим владельцам. Не менять публичные данные, конфигурацию, схему и JSON заданий.
4. Проверить, что UI больше не обращается к построителям IQueryable и не получает Identity-граф через DTO. Для фильтров нужны сравнение старой/новой проверки, независимость снимка, UTC/календарные даты и генерация SQL; для DTO — сохранение маппинга и серверной валидации.
5. Отдельно завершить этап 2: единый доверенный исполнитель, контекст/транзакция и частичные результаты каждой составной записи. Уже работающий UnitOfWorkFactory сохраняется. Только затем этап 3 с Personal.User и выделение проектов по плану.

Проверка этого реестра: поиск объявлений интерфейсов и их потребителей, чтение сигнатур/DTO/построителей фильтров, сверка всех 39 имён с таблицами. Это статический аудит, а не доказательство атомарности, корректности SQL или производительности. Сборка, runner, браузер и PostgreSQL в этой документальной поставке не запускались; последние результаты относятся к e8a152e и описаны в architecture-filter-cleanup.md. Подключения и данные не изменялись.
