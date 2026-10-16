# Статический инвентарь архитектуры Guard

Создан scripts/Get-ArchitectureInventory.ps1. Поиск текстовый: совпадение не доказывает вызов или нарушение границы. Комментарии могут попадать в выдачу. Контрольные суммы фиксируют файлы, а не соответствие рабочей БД модели EF.

## Контракты операций и репозиториев (включая legacy)

```text
Guard/Core/1-Entities/Abstarct/IHasSubdivision.cs:5: Guid? SubdivisionId { get; }
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:14: IQueryable<T> Query();
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:26: Task<T?> GetByIdAsync(object[] keyValues, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:31: IReadOnlyList<T> GetAll();
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:36: Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:43: void Add(T entity);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:48: Task AddAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:53: void AddRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:58: Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:65: void Update(T entity);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:70: Task UpdateAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:75: void UpdateRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:80: Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:87: void Delete(T entity);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:92: Task DeleteAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:97: void DeleteRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:102: Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:109: bool Exists(params object[] keyValues);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:114: Task<bool> ExistsAsync(object[] keyValues, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:119: int Count();
Guard/Core/4-Repositories/Abstarct/IBasicRepository.cs:124: Task<int> CountAsync(CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:16: IQueryable<T> Query();
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:31: Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:36: IReadOnlyList<T> GetAll();
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:42: Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:50: void Add(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:57: Task AddAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:63: void AddRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:70: Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:78: void Update(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:85: Task UpdateAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:91: void UpdateRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:98: Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:106: void SoftDelete(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:113: Task SoftDeleteAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:119: void Archive(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:126: Task ArchiveAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:132: void Block(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:139: Task BlockAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:145: void Unblock(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:152: Task UnblockAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:158: void Restore(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:165: Task RestoreAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:172: void ChangeStatus(T entity, Status newStatus);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:181: Task ChangeStatusAsync(T entity, Status newStatus, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:189: void Delete(T entity);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:197: Task DeleteAsync(T entity, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:203: void DeleteRange(IEnumerable<T> entities);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:210: Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:218: bool Exists(object id);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:225: Task<bool> ExistsAsync(object id, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:230: int Count();
Guard/Core/4-Repositories/Abstarct/IGenericRepository.cs:236: Task<int> CountAsync(CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IReadRepository.cs:5: Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IReadRepository.cs:6: Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IReadRepository.cs:7: Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IReadRepository.cs:9: Task<bool> ExistsAsync(object id, CancellationToken ct = default);
Guard/Core/4-Repositories/Abstarct/IReadRepository.cs:10: Task<int> CountAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:16: IGenericRepository<T> BaseEntityRepository<T>() where T : BaseEntity;
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:24: IBasicRepository<T> BasicRepository<T>() where T : class;
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:33: Task<int> SaveChangesAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:39: int SaveChanges();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:49: Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:58: Task<int> ExecuteSqlRawAsync(string sql, IEnumerable<object> parameters, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:67: Task BeginTransactionAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:75: Task CommitTransactionAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:81: Task RollbackTransactionAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:89: void BeginTransaction();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:96: void CommitTransaction();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:101: void RollbackTransaction();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:111: Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:121: Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWorkFactory.cs:3: Task<IUnitOfWork> CreateAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/IAuditService.cs:14: void LogIdentityEvent(
Guard/Core/6-Services/Administrator/IAuditService.cs:18: string? details = null);
Guard/Core/6-Services/Administrator/ILogService.cs:7: Task<TResult> GetLogsAsync<TResult>(
Guard/Core/6-Services/Administrator/ILogService.cs:9: CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:12: Task<string?> GetOwnSubdivisionPathAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:15: Task<TResult> QuerySubdivisionsAsync<TResult>(Func<IQueryable<Subdivision>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:23: Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:31: Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:38: Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:46: Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:57: Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:64: Task UpdateAsync(Subdivision subdivision, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:74: Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:81: Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:88: Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:95: Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:102: Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:109: Task RebuildHierarchyAndPathsAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:115: Task<IReadOnlyList<Classifier>> GetOrganTypesAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:122: Task<IReadOnlyList<Classifier>> GetClassifiersByTypeAsync(ClassifierType type, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:123: Task<Guid> CreateFromDtoAsync(CreateSubdivisionDto input, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:124: Task UpdateFromDtoAsync(EditSubdivisionDto input, CancellationToken ct = default);
Guard/Core/6-Services/IAccessChangeNotifier.cs:4: IDisposable Subscribe(string userId, Func<Task> refresh);
Guard/Core/6-Services/IAccessChangeNotifier.cs:5: Task PublishAsync(IEnumerable<string> userIds);
Guard/Core/6-Services/IAccountPolicyService.cs:9: Task<AccountPolicy> GetAsync(CancellationToken ct = default);
Guard/Core/6-Services/IAccountPolicyService.cs:10: Task SaveAsync(AccountPolicy policy, CancellationToken ct = default);
Guard/Core/6-Services/IAccountPolicyService.cs:11: Task<AccountState> CheckAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IAccountPolicyService.cs:12: Task RecordActivityAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IAccountPolicyService.cs:13: Task SweepAsync(CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:15: Task<TResult> QueryRolesAsync<TResult>(
Guard/Core/6-Services/IApplicationRoleService.cs:17: CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:25: Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:33: Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:39: Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:47: Task<bool> IsRoleNameUniqueAsync(string roleName, string? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:57: Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:64: Task UpdateAsync(ApplicationRole role, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:71: Task DeleteAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:16: Task<TResult> QueryClassifiersAsync<TResult>(
Guard/Core/6-Services/IClassifierService.cs:18: CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:26: Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:34: Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:42: Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:48: Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:57: Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:67: Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:74: Task UpdateAsync(Classifier classifier, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:82: Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IClassifierService.cs:89: Task DeleteAsync(int id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IClassifierService.cs:90: Task<int> CreateFromDtoAsync(CreateClassifierDto input, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:91: Task UpdateFromDtoAsync(EditClassifierDto input, CancellationToken ct = default);
Guard/Core/6-Services/ICurrentUserService.cs:8: string? UserId { get; }
Guard/Core/6-Services/ICurrentUserService.cs:13: string? UserName { get; }
Guard/Core/6-Services/ICurrentUserService.cs:18: Task<UserContext?> GetContextAsync(CancellationToken ct = default);
Guard/Core/6-Services/IDataAccessScopeService.cs:8: Task<DataAccessScope> GetAsync(CancellationToken ct = default);
Guard/Core/6-Services/IIpAccessService.cs:5: Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:16: Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:24: Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:30: Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:37: Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:45: Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:56: Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:63: Task UpdateAsync(IpAddress ipAddress, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:73: Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:80: Task ArchiveAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:87: Task BlockAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:94: Task UnblockAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:101: Task RestoreAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpManagementAccessService.cs:7: Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default);
Guard/Core/6-Services/ILogConfigurationService.cs:5: Task SaveAsync(int maxSessions, LogEventLevel level, CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:6: Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:7: Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:8: Task<bool> HasAsync(string permission, CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:9: Task RequireAsync(string permission, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalIpService.cs:7: Task<PersonalIpAssignmentsDto> GetAsync(Guid personalId, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalIpService.cs:8: Task UpdateAsync(Guid personalId, IEnumerable<Guid> ipIds, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:12: Task<Guard.Core.Services.DTOs.PersonalSearchResult> SearchAsync(Guard.Core.Services.DTOs.PersonalSearchRequest request, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:13: Task<Guard.Core.Services.DTOs.PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:19: Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:27: Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:35: Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:42: Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:50: Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:59: Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
Guard/Core/6-Services/IPersonalService.cs:62: CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:72: Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:79: Task UpdateAsync(Personal Personal, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:88: Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IPersonalService.cs:95: Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IPersonalService.cs:102: Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IPersonalService.cs:109: Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IPersonalService.cs:116: Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
Guard/Core/6-Services/IPersonalService.cs:117: Task<Guid> CreateFromDtoAsync(CreatePersonalDto input, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:118: Task UpdateFromDtoAsync(EditPersonalDto input, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:5: Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:6: Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:7: Task DeleteAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:8: Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:9: Task SetUserRolesAsync(string userId, IEnumerable<string> roles, string version, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:10: Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:11: Task InitializeAdministratorAsync(CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:18: Task<TResult> QueryUsersAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:20: CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:25: Task<TResult> QueryRolesAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:27: CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:32: Task<TResult> QueryIpAddressesAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:34: CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:39: Task<TResult> QueryPersonalsAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:41: CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:47: Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:48: Task<Guard.Core.Services.DTOs.UserEditData> GetUserEditAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:49: Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:50: Task<IReadOnlyList<Guard.Core.Services.DTOs.UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:51: Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:52: Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:53: Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:54: Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:60: Task<List<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:61: Task<List<string>> GetRoleClaimsAsync(string roleId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:62: Task<IdentityResult> UpdateRoleClaimsAsync(string roleId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:63: Task<List<string>> GetUserClaimsAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:64: Task<IdentityResult> UpdateUserClaimsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:70: Task UpdatePersonalIpAddressesAsync(Guid personalId, IEnumerable<Guid> ipAddressIds, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:71: Task<bool> IsIpAllowedForUserAsync(string userId, string clientIp, CancellationToken ct = default);
```

## Публичные операции конкретных сервисов (включая интерфейсы в общих файлах)

```text
Guard/Core/6-Services/AccessChangeNotifier.cs:18: public Task PublishAsync(IEnumerable<string> userIds)
Guard/Core/6-Services/AccessChangeNotifier.cs:32: public Task NotifyAsync() => Volatile.Read(ref callback)?.Invoke() ?? Task.CompletedTask;
Guard/Core/6-Services/AccountPolicyService.cs:12: public async Task<AccountPolicy> GetAsync(CancellationToken ct = default)
Guard/Core/6-Services/AccountPolicyService.cs:18: public async Task SaveAsync(AccountPolicy policy, CancellationToken ct = default)
Guard/Core/6-Services/AccountPolicyService.cs:52: public async Task<AccountState> CheckAsync(string userId, CancellationToken ct = default)
Guard/Core/6-Services/AccountPolicyService.cs:91: public async Task RecordActivityAsync(string userId, CancellationToken ct = default)
Guard/Core/6-Services/AccountPolicyService.cs:104: public async Task SweepAsync(CancellationToken ct = default)
Guard/Core/6-Services/Administrator/ILogService.cs:7: Task<TResult> GetLogsAsync<TResult>(
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:12: Task<string?> GetOwnSubdivisionPathAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:15: Task<TResult> QuerySubdivisionsAsync<TResult>(Func<IQueryable<Subdivision>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:23: Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:38: Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:46: Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:57: Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:115: Task<IReadOnlyList<Classifier>> GetOrganTypesAsync(CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:122: Task<IReadOnlyList<Classifier>> GetClassifiersByTypeAsync(ClassifierType type, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:123: Task<Guid> CreateFromDtoAsync(CreateSubdivisionDto input, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/LogService.cs:23: public async Task<TResult> GetLogsAsync<TResult>(
Guard/Core/6-Services/Administrator/SubdivisionService.cs:36: public async Task<string?> GetOwnSubdivisionPathAsync(CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:46: public async Task<TResult> QuerySubdivisionsAsync<TResult>(
Guard/Core/6-Services/Administrator/SubdivisionService.cs:53: public async Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:59: public async Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:69: public async Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:80: public async Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:151: public async Task UpdateAsync(Subdivision subdivision, CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:174: public async Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:243: public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:258: public async Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:273: public async Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:288: public async Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:303: public async Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:335: public async Task RebuildHierarchyAndPathsAsync(CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:422: public async Task<IReadOnlyList<Classifier>> GetOrganTypesAsync(CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:429: public async Task<IReadOnlyList<Classifier>> GetClassifiersByTypeAsync(ClassifierType type, CancellationToken ct = default)
Guard/Core/6-Services/Administrator/SubdivisionService.cs:436: public Task<Guid> CreateFromDtoAsync(CreateSubdivisionDto input, CancellationToken ct = default) {
Guard/Core/6-Services/Administrator/SubdivisionService.cs:441: public async Task UpdateFromDtoAsync(EditSubdivisionDto input, CancellationToken ct = default) {
Guard/Core/6-Services/ApiDiagnosticService.cs:12: Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null);
Guard/Core/6-Services/ApiDiagnosticService.cs:19: public async Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null)
Guard/Core/6-Services/ApplicationRoleService.cs:23: public async Task<TResult> QueryRolesAsync<TResult>(
Guard/Core/6-Services/ApplicationRoleService.cs:30: public async Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:37: public async Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:49: public async Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:58: public async Task<bool> IsRoleNameUniqueAsync(string roleName, string? excludeId = null, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:72: public async Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:77: public async Task UpdateAsync(ApplicationRole role, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:85: public async Task DeleteAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:28: public async Task<TResult> QueryClassifiersAsync<TResult>(
Guard/Core/6-Services/ClassifierService.cs:35: public async Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:42: public async Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:52: public async Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:67: public async Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:77: public async Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:86: public async Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:112: public async Task UpdateAsync(Classifier classifier, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:134: public async Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/ClassifierService.cs:151: public async Task DeleteAsync(int id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/ClassifierService.cs:181: public Task<int> CreateFromDtoAsync(CreateClassifierDto input, CancellationToken ct = default) {
Guard/Core/6-Services/ClassifierService.cs:186: public async Task UpdateFromDtoAsync(EditClassifierDto input, CancellationToken ct = default) {
Guard/Core/6-Services/CurrentUserService.cs:15: public async Task<UserContext?> GetContextAsync(CancellationToken ct = default)
Guard/Core/6-Services/DataAccessScopeService.cs:11: public async Task<DataAccessScope> GetAsync(CancellationToken ct = default)
Guard/Core/6-Services/DirectoryApiClient.cs:20: Task<ApiSnapshot> FetchAsync(CancellationToken ct);
Guard/Core/6-Services/DirectoryApiClient.cs:21: Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null);
Guard/Core/6-Services/DirectoryApiClient.cs:22: Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null);
Guard/Core/6-Services/DirectoryApiClient.cs:23: Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
Guard/Core/6-Services/DirectoryApiClient.cs:38: public async Task<ApiSnapshot> FetchAsync(CancellationToken ct)
Guard/Core/6-Services/DirectoryApiClient.cs:41: public async Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null)
Guard/Core/6-Services/DirectoryApiClient.cs:44: public async Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null)
Guard/Core/6-Services/DirectoryApiClient.cs:78: public async Task PingAsync(string baseUrl, string username, string password, CancellationToken ct)
Guard/Core/6-Services/DirectoryApiClient.cs:96: public async Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
Guard/Core/6-Services/DirectoryManagementService.cs:9: Task<bool> IsApiManagedAsync(CancellationToken ct = default);
Guard/Core/6-Services/DirectoryManagementService.cs:14: public async Task<bool> IsApiManagedAsync(CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:36: public async Task<Guid> CreateAsync(ExportRequest request, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:55: public async Task<ExportJobDto> GetAsync(Guid id, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:62: public async Task CancelAsync(Guid id, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:71: public async Task<IReadOnlyList<ExportJobDto>> GetActiveAsync(ExportArea area, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:79: public async Task<ExportArchivePage> SearchArchiveAsync(ExportArchiveRequest request, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:97: public async Task<ExportArchiveDetails> GetArchiveDetailsAsync(Guid id, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:109: public async Task<ExportRetentionSettings> GetRetentionAsync(CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:114: public async Task SaveRetentionAsync(int years, Guid version, CancellationToken ct = default)
Guard/Core/6-Services/Exports/DataExportService.cs:124: public async Task<ExportPreview> PreviewAsync(Guid id, int part = 1, int skip = 0, CancellationToken ct = default)
Guard/Core/6-Services/Exports/ExportAccess.cs:16: public async Task<ExportAccessScope> ForUserAsync(string id, ExportRequest request, CancellationToken ct)
Guard/Core/6-Services/Exports/ExportContracts.cs:60: Task<Guid> CreateAsync(ExportRequest request, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:61: Task<ExportJobDto> GetAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:62: Task<IReadOnlyList<ExportJobDto>> GetActiveAsync(ExportArea area, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:64: Task<ExportArchivePage> SearchArchiveAsync(ExportArchiveRequest request, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:65: Task<ExportPreview> PreviewAsync(Guid id, int part = 1, int skip = 0, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:66: Task<ExportArchiveDetails> GetArchiveDetailsAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportContracts.cs:67: Task<Guard.Core.Entities.ExportRetentionSettings> GetRetentionAsync(CancellationToken ct = default);
Guard/Core/6-Services/Exports/ExportDocumentWriter.cs:17: public async Task<ExportWrittenFile> WriteAsync(ExportJob job, ExportRequest request, ExportSnapshotInfo snapshot,
Guard/Core/6-Services/Exports/ExportProcessor.cs:13: public async Task<bool> RunNextAsync(CancellationToken stopping)
Guard/Core/6-Services/Exports/ExportProcessor.cs:96: public async Task RecoverAsync(CancellationToken ct)
Guard/Core/6-Services/Exports/ExportProcessor.cs:108: public async Task CleanupAsync(CancellationToken ct, bool scanOrphans = false)
Guard/Core/6-Services/Exports/ExportSnapshot.cs:14: public async Task<ExportSnapshotInfo> CaptureAsync(ExportJob job, ExportRequest request, ExportAccessScope scope,
Guard/Core/6-Services/Exports/ExportStorage.cs:64: public async Task<(string Key, string Hash, long Size)> SaveAsync(string path, CancellationToken ct)
Guard/Core/6-Services/Exports/ExportStorage.cs:83: public async Task VerifyAsync(string key, string expectedHash, long expectedSize, CancellationToken ct)
Guard/Core/6-Services/IAccountPolicyService.cs:9: Task<AccountPolicy> GetAsync(CancellationToken ct = default);
Guard/Core/6-Services/IAccountPolicyService.cs:11: Task<AccountState> CheckAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:15: Task<TResult> QueryRolesAsync<TResult>(
Guard/Core/6-Services/IApplicationRoleService.cs:25: Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:33: Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:39: Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:47: Task<bool> IsRoleNameUniqueAsync(string roleName, string? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:57: Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:16: Task<TResult> QueryClassifiersAsync<TResult>(
Guard/Core/6-Services/IClassifierService.cs:26: Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:34: Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:42: Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:48: Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:57: Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:67: Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:90: Task<int> CreateFromDtoAsync(CreateClassifierDto input, CancellationToken ct = default);
Guard/Core/6-Services/ICurrentUserService.cs:18: Task<UserContext?> GetContextAsync(CancellationToken ct = default);
Guard/Core/6-Services/IDataAccessScopeService.cs:8: Task<DataAccessScope> GetAsync(CancellationToken ct = default);
Guard/Core/6-Services/IIpAccessService.cs:5: Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:16: Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:24: Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:30: Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:37: Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:45: Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default);
Guard/Core/6-Services/IIpAddressService.cs:56: Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default);
Guard/Core/6-Services/IIpManagementAccessService.cs:7: Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default);
Guard/Core/6-Services/IpAccessService.cs:10: public async Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default)
Guard/Core/6-Services/IpAddressService.cs:16: public async Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default)
Guard/Core/6-Services/IpAddressService.cs:23: public Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
Guard/Core/6-Services/IpAddressService.cs:26: public async Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default) =>
Guard/Core/6-Services/IpAddressService.cs:30: public async Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default) =>
Guard/Core/6-Services/IpAddressService.cs:34: public async Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default)
Guard/Core/6-Services/IpAddressService.cs:49: public Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default) =>
Guard/Core/6-Services/IpAddressService.cs:51: public async Task UpdateAsync(IpAddress ipAddress, CancellationToken ct = default)
Guard/Core/6-Services/IpAddressService.cs:56: public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "delete", ct);
Guard/Core/6-Services/IpAddressService.cs:57: public async Task ArchiveAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "archive", ct);
Guard/Core/6-Services/IpAddressService.cs:58: public async Task RestoreAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "restore", ct);
Guard/Core/6-Services/IpAddressService.cs:59: public async Task BlockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "block", ct);
Guard/Core/6-Services/IpAddressService.cs:60: public async Task UnblockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "unblock", ct);
Guard/Core/6-Services/IPermissionService.cs:6: Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:7: Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IPermissionService.cs:8: Task<bool> HasAsync(string permission, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalIpService.cs:7: Task<PersonalIpAssignmentsDto> GetAsync(Guid personalId, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:12: Task<Guard.Core.Services.DTOs.PersonalSearchResult> SearchAsync(Guard.Core.Services.DTOs.PersonalSearchRequest request, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:13: Task<Guard.Core.Services.DTOs.PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:19: Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:27: Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:35: Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:42: Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:50: Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:59: Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
Guard/Core/6-Services/IPersonalService.cs:72: Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default);
Guard/Core/6-Services/IPersonalService.cs:117: Task<Guid> CreateFromDtoAsync(CreatePersonalDto input, CancellationToken ct = default);
Guard/Core/6-Services/IpManagementAccessService.cs:5: public async Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default)
Guard/Core/6-Services/IRoleAccessService.cs:5: Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:6: Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:8: Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/IRoleAccessService.cs:10: Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:18: Task<TResult> QueryUsersAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:25: Task<TResult> QueryRolesAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:32: Task<TResult> QueryIpAddressesAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:39: Task<TResult> QueryPersonalsAsync<TResult>(
Guard/Core/6-Services/ISecurityService.cs:47: Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:48: Task<Guard.Core.Services.DTOs.UserEditData> GetUserEditAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:49: Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:50: Task<IReadOnlyList<Guard.Core.Services.DTOs.UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:51: Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:52: Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:53: Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:54: Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:60: Task<List<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:61: Task<List<string>> GetRoleClaimsAsync(string roleId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:62: Task<IdentityResult> UpdateRoleClaimsAsync(string roleId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:63: Task<List<string>> GetUserClaimsAsync(string userId, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:64: Task<IdentityResult> UpdateUserClaimsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:71: Task<bool> IsIpAllowedForUserAsync(string userId, string clientIp, CancellationToken ct = default);
Guard/Core/6-Services/LogConfigurationService.cs:10: public async Task SaveAsync(int maxSessions, LogEventLevel level, CancellationToken ct = default)
Guard/Core/6-Services/PermissionService.cs:13: public async Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default)
Guard/Core/6-Services/PermissionService.cs:28: public async Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default)
Guard/Core/6-Services/PermissionService.cs:45: public async Task<bool> HasAsync(string permission, CancellationToken ct = default)
Guard/Core/6-Services/PermissionService.cs:50: public async Task RequireAsync(string permission, CancellationToken ct = default)
Guard/Core/6-Services/PersonalIpService.cs:18: public async Task<PersonalIpAssignmentsDto> GetAsync(Guid personalId, CancellationToken ct = default)
Guard/Core/6-Services/PersonalIpService.cs:35: public async Task UpdateAsync(Guid personalId, IEnumerable<Guid> ipIds, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:35: public async Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:48: public async Task<TResult> QueryPersonalsAsync<TResult>(
Guard/Core/6-Services/PersonalService.cs:55: public async Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:64: public async Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:75: public async Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:86: public async Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
Guard/Core/6-Services/PersonalService.cs:104: public async Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:127: public async Task UpdateAsync(Personal Personal, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:153: public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/PersonalService.cs:168: public async Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/PersonalService.cs:183: public async Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/PersonalService.cs:198: public async Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/PersonalService.cs:213: public async Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
Guard/Core/6-Services/PersonalService.cs:260: public async Task<PersonalSearchResult> SearchAsync(PersonalSearchRequest request, CancellationToken ct = default)
Guard/Core/6-Services/PersonalService.cs:269: public Task<PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default) =>
Guard/Core/6-Services/PersonalService.cs:313: public Task<Guid> CreateFromDtoAsync(CreatePersonalDto input, CancellationToken ct = default) {
Guard/Core/6-Services/PersonalService.cs:318: public async Task UpdateFromDtoAsync(EditPersonalDto input, CancellationToken ct = default) {
Guard/Core/6-Services/RoleAccessService.cs:29: public async Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:40: public async Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:73: public async Task DeleteAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:91: public async Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:103: public async Task SetUserRolesAsync(string userId, IEnumerable<string> roles, string version, CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:134: public async Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default)
Guard/Core/6-Services/RoleAccessService.cs:145: public async Task InitializeAdministratorAsync(CancellationToken ct = default)
Guard/Core/6-Services/SecurityService.cs:15: public Task<TResult> QueryUsersAsync<TResult>(Func<IQueryable<ApplicationUser>,Task<TResult>> query, CancellationToken ct=default) => userReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:16: public Task<TResult> QueryRolesAsync<TResult>(Func<IQueryable<ApplicationRole>,Task<TResult>> query, CancellationToken ct=default) => roleReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:17: public Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>,Task<TResult>> query, CancellationToken ct=default) => personalReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:18: public Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>,Task<TResult>> query, CancellationToken ct=default) => ips.QueryIpAddressesAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:19: public Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct=default) => userReads.GetByIdAsync(id,ct);
Guard/Core/6-Services/SecurityService.cs:20: public async Task<IReadOnlyList<Guard.Core.Services.DTOs.UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct=default)
Guard/Core/6-Services/SecurityService.cs:36: public async Task<Guard.Core.Services.DTOs.UserEditData> GetUserEditAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/SecurityService.cs:56: public Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default)
Guard/Core/6-Services/SecurityService.cs:118: public Task<IdentityResult> CreateUserAsync(ApplicationUser user,string password,IEnumerable<string>? roles=null,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:131: public Task<IdentityResult> UpdateUserAsync(ApplicationUser user,IEnumerable<string> roles,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:143: public Task<IdentityResult> ToggleUserLockoutAsync(string userId,bool lockout,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:156: public Task<IdentityResult> ResetPasswordAsync(string userId,string newPassword,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:166: public async Task<List<string>> GetUserRolesAsync(string userId,CancellationToken ct=default) {
Guard/Core/6-Services/SecurityService.cs:172: public async Task<List<string>> GetRoleClaimsAsync(string roleId,CancellationToken ct=default) => (await roles.GetAsync(roleId,ct)).Permissions.ToList();
Guard/Core/6-Services/SecurityService.cs:173: public async Task<IdentityResult> UpdateRoleClaimsAsync(string roleId,IEnumerable<string> values,CancellationToken ct=default) {
Guard/Core/6-Services/SecurityService.cs:177: public async Task<List<string>> GetUserClaimsAsync(string userId,CancellationToken ct=default) {
Guard/Core/6-Services/SecurityService.cs:183: public Task<IdentityResult> UpdateUserClaimsAsync(string userId,IEnumerable<string> values,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:185: public Task UpdatePersonalIpAddressesAsync(Guid personalId,IEnumerable<Guid> ids,CancellationToken ct=default) => assignments.UpdateAsync(personalId,ids,ct);
Guard/Core/6-Services/SecurityService.cs:186: public Task<bool> IsIpAllowedForUserAsync(string userId,string clientIp,CancellationToken ct=default) => ipGate.IsAllowedAsync(userId,clientIp,ct);
Guard/Core/6-Services/SynchronizationService.cs:11: Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default);
Guard/Core/6-Services/SynchronizationService.cs:12: Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false);
Guard/Core/6-Services/SynchronizationService.cs:14: Task<SynchronizationRun> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false);
Guard/Core/6-Services/SynchronizationService.cs:15: Task<IReadOnlyList<SynchronizationRun>> GetHistoryAsync(CancellationToken ct = default);
Guard/Core/6-Services/SynchronizationService.cs:22: public async Task CheckConnectionAsync(CancellationToken ct = default)
Guard/Core/6-Services/SynchronizationService.cs:32: public async Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default)
Guard/Core/6-Services/SynchronizationService.cs:46: public async Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false)
Guard/Core/6-Services/SynchronizationService.cs:58: public async Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null, bool? completeSnapshotGuaranteed = null)
Guard/Core/6-Services/SynchronizationService.cs:102: public async Task<SynchronizationRun> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false)
Guard/Core/6-Services/SynchronizationService.cs:125: public async Task<IReadOnlyList<SynchronizationRun>> GetHistoryAsync(CancellationToken ct = default)
Guard/Core/6-Services/SynchronizationService.cs:138: public Task CheckConnectionAsync(string baseUrl, string username, string password, CancellationToken ct) => client.PingAsync(baseUrl, username, password, ct);
Guard/Core/6-Services/SynchronizationService.cs:140: public async Task RecoverInterruptedAsync(CancellationToken ct)
Guard/Core/6-Services/SynchronizationService.cs:154: public async Task<SynchronizationRun> RunAsync(string actor, Guid? pool, bool scheduled, CancellationToken ct, DateOnly? updatedSince = null, Guid? queuedRunId = null)
```

## IQueryable и утечки Identity в сервисах/DTO

```text
Guard/Core/6-Services/Administrator/ILogService.cs:8: Func<IQueryable<LogEntry>, Task<TResult>> query,
Guard/Core/6-Services/Administrator/ISubdivisionService.cs:15: Task<TResult> QuerySubdivisionsAsync<TResult>(Func<IQueryable<Subdivision>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/Administrator/LogService.cs:24: Func<IQueryable<LogEntry>, Task<TResult>> query,
Guard/Core/6-Services/Administrator/SubdivisionService.cs:47: Func<IQueryable<Subdivision>, Task<TResult>> query,
Guard/Core/6-Services/ApplicationRoleService.cs:6: public class ApplicationRoleService : IApplicationRoleService
Guard/Core/6-Services/ApplicationRoleService.cs:8: private readonly IReadRepository<ApplicationRole> _readRoleRepository;
Guard/Core/6-Services/ApplicationRoleService.cs:9: private readonly ILogger<ApplicationRoleService> _logger;
Guard/Core/6-Services/ApplicationRoleService.cs:12: public ApplicationRoleService(
Guard/Core/6-Services/ApplicationRoleService.cs:13: IReadRepository<ApplicationRole> readRoleRepository,
Guard/Core/6-Services/ApplicationRoleService.cs:14: ILogger<ApplicationRoleService> logger, IRoleAccessService access)
Guard/Core/6-Services/ApplicationRoleService.cs:24: Func<IQueryable<ApplicationRole>, Task<TResult>> query,
Guard/Core/6-Services/ApplicationRoleService.cs:30: public async Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:37: public async Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:49: public async Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:72: public async Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default)
Guard/Core/6-Services/ApplicationRoleService.cs:77: public async Task UpdateAsync(ApplicationRole role, CancellationToken ct = default)
Guard/Core/6-Services/ClassifierService.cs:29: Func<IQueryable<Classifier>, Task<TResult>> query,
Guard/Core/6-Services/Exports/ExportFilters.cs:3: using Radzen;
Guard/Core/6-Services/Exports/ExportFilters.cs:7: // Same Radzen predicate semantics as the lists, with a serializable independent snapshot.
Guard/Core/6-Services/Exports/ExportFilters.cs:23: public static IQueryable<T> Apply<T>(IQueryable<T> query, ExportFilter? filter, IReadOnlySet<string> fields)
Guard/Core/6-Services/Exports/ExportQueries.cs:15: public static IQueryable<T> Statuses<T>(IQueryable<T> query, DataViewMode mode) where T : BaseEntity => mode switch {
Guard/Core/6-Services/Exports/ExportQueries.cs:21: public static IQueryable<T> Sort<T>(IQueryable<T> query, string? order, IReadOnlySet<string> fields)
Guard/Core/6-Services/Exports/ExportQueries.cs:34: public static async Task<IQueryable<Personal>> PersonalsAsync(ApplicationDbContext db, PersonalSearchRequest request,
Guard/Core/6-Services/Exports/ExportQueries.cs:64: public static IQueryable<Subdivision> Subdivisions(ApplicationDbContext db, ExportRequest request, ExportAccessScope scope)
Guard/Core/6-Services/Exports/ExportQueries.cs:72: public static IQueryable<IpAddress> IpAddresses(ApplicationDbContext db, ExportRequest request, ExportAccessScope scope)
Guard/Core/6-Services/Exports/ExportSnapshot.cs:19: IQueryable<Guid?> ids; IAsyncEnumerable<RawRow> rows;
Guard/Core/6-Services/IApplicationRoleService.cs:6: /// Сервис для управления ролями пользователей (ApplicationRole).
Guard/Core/6-Services/IApplicationRoleService.cs:8: public interface IApplicationRoleService
Guard/Core/6-Services/IApplicationRoleService.cs:16: Func<IQueryable<ApplicationRole>, Task<TResult>> query,
Guard/Core/6-Services/IApplicationRoleService.cs:24: /// <returns>Экземпляр <see cref="ApplicationRole"/> или <c>null</c>, если роль не найдена.</returns>
Guard/Core/6-Services/IApplicationRoleService.cs:25: Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:32: /// <returns>Экземпляр <see cref="ApplicationRole"/> или <c>null</c>, если роль не найдена.</returns>
Guard/Core/6-Services/IApplicationRoleService.cs:33: Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:39: Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:57: Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default);
Guard/Core/6-Services/IApplicationRoleService.cs:64: Task UpdateAsync(ApplicationRole role, CancellationToken ct = default);
Guard/Core/6-Services/IClassifierService.cs:17: Func<IQueryable<Classifier>, Task<TResult>> query,
Guard/Core/6-Services/IIpAddressService.cs:16: Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/IpAddressService.cs:13: private static IQueryable<IpAddress> Accessible(IQueryable<IpAddress> q, IpManagementScope scope) =>
Guard/Core/6-Services/IpAddressService.cs:16: public async Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default)
Guard/Core/6-Services/IPersonalService.cs:27: Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>, Task<TResult>> query, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:9: /// включая фильтрацию, сортировку и пагинацию в Radzen DataGrid.
Guard/Core/6-Services/ISecurityService.cs:16: /// Выполняет произвольный LINQ-запрос к пользователям (ApplicationUser).
Guard/Core/6-Services/ISecurityService.cs:19: Func<IQueryable<ApplicationUser>, Task<TResult>> query,
Guard/Core/6-Services/ISecurityService.cs:23: /// Выполняет произвольный LINQ-запрос к ролям (ApplicationRole).
Guard/Core/6-Services/ISecurityService.cs:26: Func<IQueryable<ApplicationRole>, Task<TResult>> query,
Guard/Core/6-Services/ISecurityService.cs:33: Func<IQueryable<IpAddress>, Task<TResult>> query,
Guard/Core/6-Services/ISecurityService.cs:40: Func<IQueryable<Personal>, Task<TResult>> query,
Guard/Core/6-Services/ISecurityService.cs:47: Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:49: Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:51: Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:52: Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:53: Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:54: Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:62: Task<IdentityResult> UpdateRoleClaimsAsync(string roleId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/ISecurityService.cs:64: Task<IdentityResult> UpdateUserClaimsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);
Guard/Core/6-Services/PersonalListQuery.cs:8: public static IOrderedQueryable<Personal> Sort(IQueryable<Personal> query, string? orderBy)
Guard/Core/6-Services/PersonalQueryFilter.cs:9: public static IQueryable<Personal> Apply(IQueryable<Personal> query, PersonalFilter? filter)
Guard/Core/6-Services/PersonalService.cs:49: Func<IQueryable<Personal>, Task<TResult>> query,
Guard/Core/6-Services/PersonalService.cs:245: private async Task<IQueryable<Personal>> ListQueryAsync(ApplicationDbContext db, PersonalSearchRequest request, CancellationToken ct)
Guard/Core/6-Services/PersonalService.cs:252: private static IQueryable<PersonalListItemDto> ProjectList(IQueryable<Personal> query) =>
Guard/Core/6-Services/RoleAccessService.cs:22: private static void Check(IdentityResult result)
Guard/Core/6-Services/RoleAccessService.cs:33: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:47: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:52: var role = create ? new ApplicationRole() :
Guard/Core/6-Services/RoleAccessService.cs:78: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:109: var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/RoleAccessService.cs:150: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/SecurityService.cs:10: IReadRepository<ApplicationUser> userReads, IReadRepository<ApplicationRole> roleReads, IReadRepository<Personal> personalReads,
Guard/Core/6-Services/SecurityService.cs:15: public Task<TResult> QueryUsersAsync<TResult>(Func<IQueryable<ApplicationUser>,Task<TResult>> query, CancellationToken ct=default) => userReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:16: public Task<TResult> QueryRolesAsync<TResult>(Func<IQueryable<ApplicationRole>,Task<TResult>> query, CancellationToken ct=default) => roleReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:17: public Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>,Task<TResult>> query, CancellationToken ct=default) => personalReads.QueryAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:18: public Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>,Task<TResult>> query, CancellationToken ct=default) => ips.QueryIpAddressesAsync(query,ct);
Guard/Core/6-Services/SecurityService.cs:19: public Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct=default) => userReads.GetByIdAsync(id,ct);
Guard/Core/6-Services/SecurityService.cs:33: private static void Check(IdentityResult result) {
Guard/Core/6-Services/SecurityService.cs:56: public Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default)
Guard/Core/6-Services/SecurityService.cs:90: private async Task CheckTargetAsync(ApplicationDbContext db, ApplicationUser user, CancellationToken ct)
Guard/Core/6-Services/SecurityService.cs:99: private async Task<IdentityResult> WriteAsync(Func<ApplicationDbContext,UserManager<ApplicationUser>,Task> write, CancellationToken ct,
Guard/Core/6-Services/SecurityService.cs:106: var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/SecurityService.cs:116: return IdentityResult.Success;
Guard/Core/6-Services/SecurityService.cs:118: public Task<IdentityResult> CreateUserAsync(ApplicationUser user,string password,IEnumerable<string>? roles=null,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:126: var created=new ApplicationUser { UserName=user.UserName, Email=user.Email, PersonalId=user.PersonalId };
Guard/Core/6-Services/SecurityService.cs:131: public Task<IdentityResult> UpdateUserAsync(ApplicationUser user,IEnumerable<string> roles,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:143: public Task<IdentityResult> ToggleUserLockoutAsync(string userId,bool lockout,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:156: public Task<IdentityResult> ResetPasswordAsync(string userId,string newPassword,CancellationToken ct=default) =>
Guard/Core/6-Services/SecurityService.cs:169: var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/SecurityService.cs:173: public async Task<IdentityResult> UpdateRoleClaimsAsync(string roleId,IEnumerable<string> values,CancellationToken ct=default) {
Guard/Core/6-Services/SecurityService.cs:175: await roles.SaveAsync(role,ct); return IdentityResult.Success;
Guard/Core/6-Services/SecurityService.cs:183: public Task<IdentityResult> UpdateUserClaimsAsync(string userId,IEnumerable<string> values,CancellationToken ct=default) =>
```

## EF в UI и потребители query-методов

```text
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:12: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Classifiers/Index.razor.cs:8: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Classifiers/Index.razor.cs:93: var result = await ClassifierService.QueryClassifiersAsync(async query => {
Guard/Components/Pages/Classifiers/Index.razor.cs:95: var total = await query.CountAsync(ct);
Guard/Components/Pages/Classifiers/Index.razor.cs:97: var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 20).Select(p => new ClassifierListDto(p.Id, p.Version, p.Type, p.Code, p.ClassifierName, p.Value, p.IsActive, p.UpdatedAt)).ToListAsync(ct);
Guard/Components/Pages/IpAddresses/Index.razor.cs:9: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/IpAddresses/Index.razor.cs:122: var (items, totalCount) = await IpAddressService.QueryIpAddressesAsync(async query =>
Guard/Components/Pages/IpAddresses/Index.razor.cs:126: var total = await query.CountAsync(ct);
Guard/Components/Pages/IpAddresses/Index.razor.cs:131: }).ToListAsync(ct);
Guard/Components/Pages/Logs/Index.razor.cs:7: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Logs/Index.razor.cs:164: query = filter(query); var total = await query.CountAsync(ct);
Guard/Components/Pages/Logs/Index.razor.cs:166: var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 100).ToListAsync(ct);
Guard/Components/Pages/Roles/Index.razor.cs:8: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Roles/Index.razor.cs:81: var result = await RoleService.QueryRolesAsync(async query => {
Guard/Components/Pages/Roles/Index.razor.cs:83: var total = await query.CountAsync(ct);
Guard/Components/Pages/Roles/Index.razor.cs:85: var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 20).Select(p => new RoleListDto(p.Id, p.Name, p.NormalizedName)).ToListAsync(ct);
Guard/Components/Pages/Subdivisions/Index.razor.cs:12: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Subdivisions/Index.razor.cs:118: var result = await SubdivisionService.QuerySubdivisionsAsync(async query => {
Guard/Components/Pages/Subdivisions/Index.razor.cs:126: var total = await query.CountAsync(ct);
Guard/Components/Pages/Subdivisions/Index.razor.cs:128: var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 20).Select(p => new SubdivisionListDto { Id=p.Id, Version=p.Version, Status=p.Status, Name=p.Name, OrganTypeName=p.OrganType == null ? null : p.OrganType.Value, StatusClassifierName=p.StatusClassifier == null ? null : p.StatusClassifier.Value, ParentName=p.Parent == null ? null : p.Parent.Name, PositionFormationName=p.PositionFormationName, PostalCode=p.PostalCode, Address=p.Address, Phone=p.Phone, StaffCount=p.StaffCount, LevelOrder=p.LevelOrder, IsDepartment=p.IsDepartment }).ToListAsync(ct);
Guard/Components/Pages/Users/Index.razor.cs:10: using Microsoft.EntityFrameworkCore;
Guard/Components/Pages/Users/Index.razor.cs:81: var (items, totalCount) = await Security.QueryUsersAsync(async query =>
Guard/Components/Pages/Users/Index.razor.cs:85: var total = await query.CountAsync(ct);
Guard/Components/Pages/Users/Index.razor.cs:106: .ToListAsync(ct);
```

## Прямое разрешение DI и фабрики scope

```text
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:157: var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
Guard/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs:30: await using var scope = _scopeFactory.CreateAsyncScope();
Guard/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs:31: var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
Guard/Components/Account/Shared/PasskeySubmit.razor:2: @inject IServiceProvider Services
Guard/Components/Account/Shared/PasskeySubmit.razor:38: tokens = Services.GetService<IAntiforgery>()?.GetTokens(HttpContext);
Guard/Core/3-Contexts/DatabaseInitializer.cs:70: public static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
Guard/Core/3-Contexts/DatabaseInitializer.cs:72: using var scope = serviceProvider.CreateScope();
Guard/Core/3-Contexts/DatabaseInitializer.cs:75: var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/3-Contexts/DatabaseInitializer.cs:79: // var thirdContext = scope.ServiceProvider.GetRequiredService<ThirdDbContext>();
Guard/Core/5-UnitOfWork/UnitOfWork.cs:18: public UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider, ILogger<UnitOfWork> logger)
Guard/Core/5-UnitOfWork/UnitOfWorkFactory.cs:7: IPermissionService permissions, IServiceProvider services, ILogger<UnitOfWork> logger) : IUnitOfWorkFactory
Guard/Core/6-Services/AccountPolicyService.cs:10: IServiceProvider services, IAuditService audit, IAccessChangeNotifier notifier) : IAccountPolicyService
Guard/Core/6-Services/AccountPolicyService.cs:21: var permissions = services.GetRequiredService<IPermissionService>();
Guard/Core/6-Services/Exports/ExportProcessor.cs:158: await using var scope = scopes.CreateAsyncScope(); var processor = scope.ServiceProvider.GetRequiredService<ExportProcessor>();
Guard/Core/6-Services/Exports/ExportProcessor.cs:161: var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
Guard/Core/6-Services/RoleAccessService.cs:32: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:33: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:45: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:46: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:47: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:76: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:77: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:78: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/RoleAccessService.cs:94: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:95: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:107: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:108: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:109: var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/RoleAccessService.cs:137: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:138: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:148: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/RoleAccessService.cs:149: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/RoleAccessService.cs:150: var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
Guard/Core/6-Services/SecurityService.cs:24: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/SecurityService.cs:25: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/SecurityService.cs:40: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/SecurityService.cs:41: var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/SecurityService.cs:104: await using var scope = scopes.CreateAsyncScope();
Guard/Core/6-Services/SecurityService.cs:105: var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/SecurityService.cs:106: var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/SecurityService.cs:168: await using var scope=scopes.CreateAsyncScope();
Guard/Core/6-Services/SecurityService.cs:169: var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Core/6-Services/SecurityService.cs:179: await using var scope=scopes.CreateAsyncScope();
Guard/Core/6-Services/SecurityService.cs:180: var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Guard/Core/6-Services/SynchronizationService.cs:359: using var scope = scopes.CreateScope();
Guard/Core/6-Services/SynchronizationService.cs:360: var runner = scope.ServiceProvider.GetRequiredService<ApiSynchronizationRunner>();
Guard/Core/6-Services/SynchronizationService.cs:362: var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
Guard/Core/Identity/AccountPolicyWorker.cs:13: await using var scope = scopes.CreateAsyncScope();
Guard/Core/Identity/AccountPolicyWorker.cs:14: await scope.ServiceProvider.GetRequiredService<IAccountPolicyService>().SweepAsync(stoppingToken);
Guard/Core/Identity/PolicyUserManager.cs:11: IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<ApplicationUser>> logger,
Guard/Core/Identity/PolicyUserManager.cs:28: var ct = ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestAborted ?? default;
Guard/Core/Identity/PolicyUserManager.cs:29: var policy = await ServiceProvider.GetRequiredService<IAccountPolicyService>().GetAsync(ct);
Guard/Program.cs:127: var env = sp.GetRequiredService<IWebHostEnvironment>();
Guard/Program.cs:142: var env = sp.GetRequiredService<IWebHostEnvironment>();
Guard/Program.cs:147: optionsBuilder.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
Guard/Program.cs:310: using var scope = app.Services.CreateScope();
Guard/Program.cs:313: var context = services.GetRequiredService<ApplicationDbContext>();
Guard/Program.cs:314: var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
Guard/Program.cs:315: var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
```

## Транзакции и блокировки

```text
Guard/Core/3-Contexts/ApplicationDbContext.cs:46: internal const long ManagementLock = 73460219;
Guard/Core/3-Contexts/ApplicationDbContext.cs:67: await using var transaction = ownTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
Guard/Core/3-Contexts/ApplicationDbContext.cs:69: await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ManagementLock})", cancellationToken);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:67: Task BeginTransactionAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:75: Task CommitTransactionAsync(CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:89: void BeginTransaction();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:96: void CommitTransaction();
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:111: Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/IUnitOfWork.cs:121: Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:109: public async Task BeginTransactionAsync(CancellationToken ct = default)
Guard/Core/5-UnitOfWork/UnitOfWork.cs:118: _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:120: public async Task CommitTransactionAsync(CancellationToken ct = default)
Guard/Core/5-UnitOfWork/UnitOfWork.cs:168: public void BeginTransaction()
Guard/Core/5-UnitOfWork/UnitOfWork.cs:177: _currentTransaction = _context.Database.BeginTransaction();
Guard/Core/5-UnitOfWork/UnitOfWork.cs:180: public void CommitTransaction()
Guard/Core/5-UnitOfWork/UnitOfWork.cs:229: public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
Guard/Core/5-UnitOfWork/UnitOfWork.cs:233: await BeginTransactionAsync(ct);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:237: await CommitTransactionAsync(ct);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:241: _logger.LogError(ex, "Сбой при выполнении действия в транзакции ExecuteInTransactionAsync");
Guard/Core/5-UnitOfWork/UnitOfWork.cs:247: public async Task<TResult> ExecuteInTransactionAsync<TResult>(
Guard/Core/5-UnitOfWork/UnitOfWork.cs:252: await BeginTransactionAsync(ct);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:256: await CommitTransactionAsync(ct);
Guard/Core/5-UnitOfWork/UnitOfWork.cs:261: _logger.LogError(ex, "Сбой при выполнении действия с результатом в транзакции ExecuteInTransactionAsync");
Guard/Core/6-Services/AccountPolicyService.cs:26: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/AccountPolicyService.cs:27: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/AccountPolicyService.cs:65: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/AccountPolicyService.cs:66: var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(74162002) AS \"Value\"").SingleAsync(ct);
Guard/Core/6-Services/AccountPolicyService.cs:95: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/AccountPolicyService.cs:96: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/Administrator/SubdivisionService.cs:90: return await _uow.ExecuteInTransactionAsync(async () =>
Guard/Core/6-Services/Administrator/SubdivisionService.cs:179: await _uow.ExecuteInTransactionAsync(async () =>
Guard/Core/6-Services/Administrator/SubdivisionService.cs:341: await _uow.ExecuteInTransactionAsync(async () =>
Guard/Core/6-Services/ApiDiagnosticService.cs:28: if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
Guard/Core/6-Services/ApiDiagnosticService.cs:97: await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None);
Guard/Core/6-Services/ClassifierService.cs:96: return await _uow.ExecuteInTransactionAsync(async () =>
Guard/Core/6-Services/Exports/DataExportService.cs:41: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/Exports/DataExportService.cs:42: await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.UserId}, 8181))", ct);
Guard/Core/6-Services/Exports/ExportEndpoints.cs:35: await using (var transaction = await db.Database.BeginTransactionAsync(ct)) {
Guard/Core/6-Services/Exports/ExportEndpoints.cs:65: await using var transaction = await db.Database.BeginTransactionAsync(finish.Token);
Guard/Core/6-Services/Exports/ExportProcessor.cs:36: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/Exports/ExportProcessor.cs:122: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/Exports/ExportProcessor.cs:151: await lease.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ExportQueue})", release.Token);
Guard/Core/6-Services/Exports/ExportProcessor.cs:165: if (await candidate.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ExportQueue}) AS \"Value\"").SingleAsync(stoppingToken)) lease = candidate;
Guard/Core/6-Services/Exports/ExportSnapshot.cs:18: await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
Guard/Core/6-Services/Exports/ExportStorage.cs:14: public SemaphoreSlim ArchiveGate { get; } = new(1, 1);
Guard/Core/6-Services/IpAddressService.cs:66: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/IpAddressService.cs:114: db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162001)", ct);
Guard/Core/6-Services/PersonalIpService.cs:41: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/PersonalIpService.cs:42: await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
Guard/Core/6-Services/PersonalIpService.cs:47: await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Personals\" WHERE \"Id\" = {personalId} FOR UPDATE", ct);
Guard/Core/6-Services/PostgresAdvisoryLocks.cs:3: internal static class PostgresAdvisoryLocks
Guard/Core/6-Services/PostgresAdvisoryLocks.cs:6: internal const long ApiSynchronization = 73460220;
Guard/Core/6-Services/PostgresAdvisoryLocks.cs:9: internal const long ExportQueue = 73460221;
Guard/Core/6-Services/RoleAccessService.cs:48: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/RoleAccessService.cs:49: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/RoleAccessService.cs:79: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/RoleAccessService.cs:80: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/RoleAccessService.cs:110: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/RoleAccessService.cs:111: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/RoleAccessService.cs:151: await using var tx = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/RoleAccessService.cs:152: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
Guard/Core/6-Services/SecurityService.cs:107: await using var tx=await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/SecurityService.cs:108: await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)",ct);
Guard/Core/6-Services/SynchronizationService.cs:80: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/SynchronizationService.cs:81: await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
Guard/Core/6-Services/SynchronizationService.cs:109: await using var transaction = await queueDb.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/SynchronizationService.cs:110: if (!await queueDb.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_xact_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
Guard/Core/6-Services/SynchronizationService.cs:112: await queueDb.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
Guard/Core/6-Services/SynchronizationService.cs:144: if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct)) return;
Guard/Core/6-Services/SynchronizationService.cs:151: finally { await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None); }
Guard/Core/6-Services/SynchronizationService.cs:160: if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
Guard/Core/6-Services/SynchronizationService.cs:240: await using var transaction = await db.Database.BeginTransactionAsync(ct);
Guard/Core/6-Services/SynchronizationService.cs:243: await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
Guard/Core/6-Services/SynchronizationService.cs:316: await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None);
```

## Конфигурация схемы

```text
Guard/Core/2-Configurations/Abstarct/BaseEntityConfiguration.cs:25: builder.HasKey(e => e.Id);
Guard/Core/2-Configurations/Abstarct/BaseEntityConfiguration.cs:48: .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")
Guard/Core/2-Configurations/Abstarct/BaseEntityConfiguration.cs:69: builder.ToTable(t =>
Guard/Core/2-Configurations/Concrates/AccountPolicyConfiguration.cs:11: builder.ToTable("AccountPolicies", table => {
Guard/Core/2-Configurations/Concrates/AccountPolicyConfiguration.cs:16: builder.HasKey(p => p.Id);
Guard/Core/2-Configurations/Concrates/AccountPolicyConfiguration.cs:18: builder.Property(p => p.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:11: builder.HasKey(s => s.Id);
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:13: builder.Property(s => s.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:17: builder.Property(s => s.CompleteSnapshotGuaranteed).HasDefaultValue(false);
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:19: builder.ToTable("SynchronizationSettings", table => table.HasCheckConstraint(
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:29: builder.HasKey(r => r.Id);
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:33: builder.HasIndex(r => new { r.PoolId, r.StartedAt });
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:34: builder.HasIndex(r => r.Status);
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:42: builder.HasKey(e => e.PersonalExternalId);
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs:44: builder.ToTable("ApiPersonnelExits");
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs:16: builder.HasIndex(u => u.LastActivityAtUtc);
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs:20: .HasForeignKey<ApplicationUser>(u => u.PersonalId)
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs:21: .OnDelete(DeleteBehavior.SetNull);
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs:23: builder.HasIndex(u => u.PersonalId)
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs:24: .IsUnique();
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:11: builder.Property(x => x.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:12: builder.ToTable("Classifiers", t => t.HasComment("Универсальный справочник классификаторов системы"));
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:14: builder.HasKey(c => c.Id);
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:39: .HasDefaultValue(true)
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:44: .HasDefaultValueSql("CURRENT_TIMESTAMP")
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:48: builder.HasIndex(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:49: .IsUnique();
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs:52: builder.HasIndex(c => c.Type);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:11: b.ToTable("ExportJobs"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:15: b.HasIndex(x => new { x.State, x.CreatedAtUtc }); b.HasIndex(x => x.ExpiresAtUtc);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:16: b.HasIndex(x => new { x.AuthorId, x.CreatedAtUtc });
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:23: b.ToTable("ExportArtifacts"); b.HasKey(x => x.JobId);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:24: b.HasOne(x => x.Job).WithOne(x => x.Artifact).HasForeignKey<ExportArtifact>(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:27: b.Property(x => x.SubdivisionIdsJson).HasColumnType("jsonb"); b.HasIndex(x => x.StorageKey).IsUnique();
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:34: b.ToTable("ExportDeliveryAttempts"); b.HasKey(x => x.JobId);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:35: b.HasOne(x => x.Job).WithOne(x => x.Delivery).HasForeignKey<ExportDeliveryAttempt>(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:42: b.ToTable("ExportRetentionSettings", t => {
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs:46: b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs:20: builder.ToTable("IpAddresses", t => t.HasComment(@"Справочник IP-адресов и CIDR-подсетей системы Guard. Центральный каталог всех известных IP-адресов и подсетей, которые могут быть назначены пользователям, подразделениям и другим сущностям."));
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs:22: builder.HasKey(x => x.Id);
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs:35: .HasForeignKey(p => p.SubdivisionId)
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs:36: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs:38: builder.HasIndex(x => x.Address).IsUnique();
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:14: builder.Property(x => x.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:16: builder.HasIndex(p => p.ExternalId).IsUnique();
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:18: builder.ToTable("Personals", t =>
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:75: builder.HasIndex(x => x.PersonalSubdivisionId)
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:85: .HasForeignKey(p => p.SubdivisionId)
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:86: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:91: .HasForeignKey(p => new { p.PersonnelCategoryType, p.PersonnelCategoryCode })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:92: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:94: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:99: .HasForeignKey(p => new { p.SpecialRankType, p.SpecialRankCode })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:100: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:102: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:107: .HasForeignKey(p => new { p.PositionType, p.PositionCode })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:108: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:110: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:115: .HasForeignKey(p => new { p.WorkerCategoryType, p.WorkerCategoryCode })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:116: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:118: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:123: .HasForeignKey(p => new { p.StatusType, p.StatusCode })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:124: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:126: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:131: .UsingEntity(j => j.ToTable("PersonalIpAddresses"));
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:133: builder.HasIndex(p => p.PersonalId)
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs:134: .IsUnique()
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:11: builder.Property(x => x.Version).IsConcurrencyToken();
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:13: builder.HasIndex(s => s.ExternalId).IsUnique();
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:15: builder.ToTable("Subdivisions", t => t.HasComment("Иерархический справочник подразделений организационной структуры системы Guard."));
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:26: builder.HasIndex(x => x.ParentSubdivisionId)
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:29: builder.HasIndex(x => x.SubdivisionId)
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:30: .IsUnique()
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:68: .HasDefaultValue(false)
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:77: .HasForeignKey(x => x.ParentId)
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:78: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:83: .HasForeignKey(s => new { s.StatusType, s.StatusCode })
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:84: .HasPrincipalKey(c => new { c.Type, c.Code })
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:86: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:91: .HasForeignKey(s => new { s.OrganTypeId, s.OrganTypeCode })
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:92: .HasPrincipalKey(o => new { o.Type, o.Code })
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:94: .OnDelete(DeleteBehavior.Restrict);
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:97: builder.HasIndex(x => x.ParentId);
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:99: builder.HasIndex(s => s.Path)
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:100: .HasOperators("varchar_pattern_ops");
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs:102: builder.HasIndex(x => new { x.Status, x.InsertedDate })
Guard/Core/3-Contexts/ApplicationDbContext.cs:32: public DbSet<IpAddress> IpAddresses { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:33: public DbSet<Subdivision> Subdivisions { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:34: public DbSet<Classifier> Classifiers { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:35: public DbSet<Personal> Personals { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:36: public DbSet<ExportJob> ExportJobs { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:37: public DbSet<ExportArtifact> ExportArtifacts { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:38: public DbSet<ExportDeliveryAttempt> ExportDeliveryAttempts { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:39: public DbSet<ExportRetentionSettings> ExportRetentionSettings { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:40: public DbSet<SynchronizationSettings> SynchronizationSettings { get; set; } = null!;
Guard/Core/3-Contexts/ApplicationDbContext.cs:41: public DbSet<SynchronizationRun> SynchronizationRuns { get; set; } = null!;
Guard/Core/3-Contexts/LogsDbContext.cs:10: public DbSet<LogEntry> Logs => Set<LogEntry>();
Guard/Core/3-Contexts/LogsDbContext.cs:18: entity.ToTable("logs");
```

## Маршруты и endpoints

```text
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:22: public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:26: var accountGroup = endpoints.MapGroup("/Account");
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:28: accountGroup.MapPost("/ChangeOwnPassword", async (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:52: accountGroup.MapPost("/PerformExternalLogin", (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:71: accountGroup.MapPost("/Logout", async (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:99: accountGroup.MapPost("/PasskeyCreationOptions", async (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:124: accountGroup.MapPost("/PasskeyRequestOptions", async (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:138: var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:140: manageGroup.MapPost("/LinkExternalLogin", async (
Guard/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:160: manageGroup.MapPost("/DownloadPersonalData", async (
Guard/Components/Account/Pages/AccessDenied.razor:1: @page "/Account/AccessDenied"
Guard/Components/Account/Pages/ConfirmEmail.razor:1: @page "/Account/ConfirmEmail"
Guard/Components/Account/Pages/ConfirmEmailChange.razor:1: @page "/Account/ConfirmEmailChange"
Guard/Components/Account/Pages/ExternalLogin.razor:1: @page "/Account/ExternalLogin"
Guard/Components/Account/Pages/ForgotPassword.razor:1: @page "/Account/ForgotPassword"
Guard/Components/Account/Pages/ForgotPasswordConfirmation.razor:1: @page "/Account/ForgotPasswordConfirmation"
Guard/Components/Account/Pages/InvalidPasswordReset.razor:1: @page "/Account/InvalidPasswordReset"
Guard/Components/Account/Pages/InvalidUser.razor:1: @page "/Account/InvalidUser"
Guard/Components/Account/Pages/Lockout.razor:1: @page "/Account/Lockout"
Guard/Components/Account/Pages/Login.razor:1: @page "/Account/Login"
Guard/Components/Account/Pages/LoginWith2fa.razor:1: @page "/Account/LoginWith2fa"
Guard/Components/Account/Pages/LoginWithRecoveryCode.razor:1: @page "/Account/LoginWithRecoveryCode"
Guard/Components/Account/Pages/Logout.razor:1: @page "/Account/Logout"
Guard/Components/Account/Pages/Manage/ChangePassword.razor:1: @page "/Account/Manage/ChangePassword"
Guard/Components/Account/Pages/Manage/DeletePersonalData.razor:1: @page "/Account/Manage/DeletePersonalData"
Guard/Components/Account/Pages/Manage/Disable2fa.razor:1: @page "/Account/Manage/Disable2fa"
Guard/Components/Account/Pages/Manage/Email.razor:1: @page "/Account/Manage/Email"
Guard/Components/Account/Pages/Manage/EnableAuthenticator.razor:1: @page "/Account/Manage/EnableAuthenticator"
Guard/Components/Account/Pages/Manage/ExternalLogins.razor:1: @page "/Account/Manage/ExternalLogins"
Guard/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor:1: @page "/Account/Manage/GenerateRecoveryCodes"
Guard/Components/Account/Pages/Manage/Index.razor:1: @page "/Account/Manage"
Guard/Components/Account/Pages/Manage/Passkeys.razor:1: @page "/Account/Manage/Passkeys"
Guard/Components/Account/Pages/Manage/PersonalData.razor:1: @page "/Account/Manage/PersonalData"
Guard/Components/Account/Pages/Manage/RenamePasskey.razor:1: @page "/Account/Manage/RenamePasskey/{Id}"
Guard/Components/Account/Pages/Manage/ResetAuthenticator.razor:1: @page "/Account/Manage/ResetAuthenticator"
Guard/Components/Account/Pages/Manage/SetPassword.razor:1: @page "/Account/Manage/SetPassword"
Guard/Components/Account/Pages/Manage/TwoFactorAuthentication.razor:1: @page "/Account/Manage/TwoFactorAuthentication"
Guard/Components/Account/Pages/Register.razor:1: @page "/Account/Register"
Guard/Components/Account/Pages/RegisterConfirmation.razor:1: @page "/Account/RegisterConfirmation"
Guard/Components/Account/Pages/ResendEmailConfirmation.razor:1: @page "/Account/ResendEmailConfirmation"
Guard/Components/Account/Pages/ResetPassword.razor:1: @page "/Account/ResetPassword"
Guard/Components/Account/Pages/ResetPasswordConfirmation.razor:1: @page "/Account/ResetPasswordConfirmation"
Guard/Components/Pages/AccountPolicy/AccountPolicySettings.razor:1: @page "/account-policy"
Guard/Components/Pages/Auth.razor:1: @page "/auth"
Guard/Components/Pages/Classifiers/Page.razor:1: @page "/classifiers"
Guard/Components/Pages/Counter.razor:1: @page "/counter"
Guard/Components/Pages/Error.razor:1: @page "/Error"
Guard/Components/Pages/ExportArchive/Page.razor:1: @page "/export-archive"
Guard/Components/Pages/Home.razor:1: @page "/"
Guard/Components/Pages/Home.razor:2: @page "/AdminPannel"
Guard/Components/Pages/IpAddresses/Page.razor:1: @page "/ip"
Guard/Components/Pages/Logs/Page.razor:1: @page "/Logs"
Guard/Components/Pages/NotFound.razor:1: @page "/not-found"
Guard/Components/Pages/Personals/Page.razor:1: @page "/personal"
Guard/Components/Pages/Roles/Page.razor:1: @page "/roles"
Guard/Components/Pages/Subdivisions/Page.razor:1: @page "/subdivisions"
Guard/Components/Pages/Synchronization/Page.razor:1: @page "/synchronization"
Guard/Components/Pages/Users/Page.razor:1: @page "/users"
Guard/Components/Pages/Weather.razor:1: @page "/weather"
Guard/Core/6-Services/Exports/ExportEndpoints.cs:12: public static IEndpointConventionBuilder MapExportEndpoints(this IEndpointRouteBuilder routes) =>
Guard/Core/6-Services/Exports/ExportEndpoints.cs:13: routes.MapPost("/exports/{id:guid}/download", DownloadAsync).RequireAuthorization();
```

## Permissions и состояния

```text
Guard/Core/0-Enums/MaintenanceTaskStatus.cs:5: Planned = 0,     // Запланировано
Guard/Core/0-Enums/MaintenanceTaskStatus.cs:6: InProgress = 1,  // В процессе
Guard/Core/0-Enums/MaintenanceTaskStatus.cs:7: Completed = 2,   // Завершено
Guard/Core/0-Enums/MaintenanceTaskStatus.cs:8: Canceled = 3,    // Отменено
Guard/Core/0-Enums/MaintenanceTaskStatus.cs:9: Failed = 4       // Сорвано / Перенесено
Guard/Core/0-Enums/Status.cs:5: Inserted = 1, //Добавлено
Guard/Core/0-Enums/Status.cs:6: Modified = 2, //Изменено
Guard/Core/0-Enums/Status.cs:7: Blocked = 3, //Заблокировано для редактирования
Guard/Core/0-Enums/Status.cs:8: Archived = 4, //Помещено в архив
Guard/Core/0-Enums/Status.cs:9: ArchivedBlocked = 5, //Помещено в архив, заблокировано для редактирования
Guard/Core/0-Enums/Status.cs:10: Deleted = 6  //Удалено
Guard/Core/Identity/EntityStatusTransitions.cs:8: public static Status ApplyManual(BaseEntity entity, StatusOperation operation, bool isRoot = false)
Guard/Core/Identity/EntityStatusTransitions.cs:16: public static bool CanApply(Status current, StatusOperation operation, bool isRoot = false) =>
Guard/Core/Identity/EntityStatusTransitions.cs:19: (StatusOperation.Archive, Status.Inserted or Status.Modified) => true,
Guard/Core/Identity/EntityStatusTransitions.cs:20: (StatusOperation.Block, Status.Inserted or Status.Modified or Status.Archived) => true,
Guard/Core/Identity/EntityStatusTransitions.cs:21: (StatusOperation.Unblock, Status.Blocked or Status.ArchivedBlocked) => true,
Guard/Core/Identity/EntityStatusTransitions.cs:22: (StatusOperation.Delete, Status.Inserted or Status.Modified or Status.Archived) => true,
Guard/Core/Identity/EntityStatusTransitions.cs:23: (StatusOperation.Restore, Status.Archived) => true,
Guard/Core/Identity/EntityStatusTransitions.cs:24: (StatusOperation.Restore, Status.Deleted) => isRoot,
Guard/Core/Identity/EntityStatusTransitions.cs:25: _ => false
Guard/Core/Identity/EntityStatusTransitions.cs:28: public static Status Apply(Status current, StatusOperation operation, bool isRoot = false)
Guard/Core/Identity/EntityStatusTransitions.cs:30: if (current == Status.Deleted && operation == StatusOperation.Restore && !isRoot)
Guard/Core/Identity/EntityStatusTransitions.cs:36: StatusOperation.Archive => Status.Archived,
Guard/Core/Identity/EntityStatusTransitions.cs:37: StatusOperation.Block => current == Status.Archived ? Status.ArchivedBlocked : Status.Blocked,
Guard/Core/Identity/EntityStatusTransitions.cs:38: StatusOperation.Unblock => current == Status.ArchivedBlocked ? Status.Archived : Status.Modified,
Guard/Core/Identity/EntityStatusTransitions.cs:39: StatusOperation.Delete => Status.Deleted,
Guard/Core/Identity/EntityStatusTransitions.cs:40: StatusOperation.Restore => Status.Modified,
Guard/Core/Identity/EntityStatusTransitions.cs:41: _ => throw new ArgumentOutOfRangeException(nameof(operation))
Guard/Core/Identity/IpStatusTransitions.cs:7: public static Status Apply(Status current, string operation) => (operation, current) switch
Guard/Core/Identity/IpStatusTransitions.cs:9: ("delete", Status.Inserted or Status.Modified or Status.Archived) => Status.Deleted,
Guard/Core/Identity/IpStatusTransitions.cs:10: ("archive", Status.Inserted or Status.Modified) => Status.Archived,
Guard/Core/Identity/IpStatusTransitions.cs:11: ("restore", Status.Archived or Status.Deleted) => Status.Modified,
Guard/Core/Identity/IpStatusTransitions.cs:12: ("block", Status.Inserted or Status.Modified) => Status.Blocked,
Guard/Core/Identity/IpStatusTransitions.cs:13: ("block", Status.Archived) => Status.ArchivedBlocked,
Guard/Core/Identity/IpStatusTransitions.cs:14: ("unblock", Status.Blocked) => Status.Modified,
Guard/Core/Identity/IpStatusTransitions.cs:15: ("unblock", Status.ArchivedBlocked) => Status.Archived,
Guard/Core/Identity/IpStatusTransitions.cs:16: _ => throw new InvalidOperationException("Недопустимый переход статуса IP-адреса.")
Guard/Core/Identity/PermissionDefinition.cs:9: new(Permissions.Personals.Read, "Сотрудники", "Список", "Основные сведения о сотрудниках"),
Guard/Core/Identity/PermissionDefinition.cs:10: new(Permissions.Personals.ReadDetails, "Сотрудники", "Полная карточка", "Все поля сотрудника", Permissions.Personals.Read),
Guard/Core/Identity/PermissionDefinition.cs:11: new(Permissions.Personals.ReadArchive, "Сотрудники", "Архив", "Архивные сотрудники", Permissions.Personals.Read),
Guard/Core/Identity/PermissionDefinition.cs:12: new(Permissions.Personals.Write, "Сотрудники", "Редактирование", "Создание, изменение и статусы", Permissions.Personals.ReadDetails),
Guard/Core/Identity/PermissionDefinition.cs:13: new(Permissions.Personals.Export, "Сотрудники", "Экспорт", "Выгрузка основных сведений", Permissions.Personals.Read),
Guard/Core/Identity/PermissionDefinition.cs:14: new(Permissions.Subdivisions.Read, "Подразделения", "Просмотр", "Доступная часть дерева"),
Guard/Core/Identity/PermissionDefinition.cs:15: new(Permissions.Subdivisions.Export, "Подразделения", "Экспорт", "Выгрузка доступных подразделений", Permissions.Subdivisions.Read),
Guard/Core/Identity/PermissionDefinition.cs:16: new(Permissions.Subdivisions.ReadArchive, "Подразделения", "Архив", "Архивные подразделения", Permissions.Subdivisions.Read),
Guard/Core/Identity/PermissionDefinition.cs:17: new(Permissions.Subdivisions.Write, "Подразделения", "Редактирование", "Изменение дерева и статусов", Permissions.Subdivisions.Read),
Guard/Core/Identity/PermissionDefinition.cs:18: new(Permissions.Users.Read, "Пользователи", "Просмотр", "Учётные записи в доступных подразделениях"),
Guard/Core/Identity/PermissionDefinition.cs:19: new(Permissions.Users.Manage, "Пользователи", "Управление", "Изменение учётных записей", Permissions.Users.Read),
Guard/Core/Identity/PermissionDefinition.cs:20: new(Permissions.IpAddresses.Read, "IP-адреса", "Просмотр", "Каталог доступных IP"),
Guard/Core/Identity/PermissionDefinition.cs:21: new(Permissions.IpAddresses.Export, "IP-адреса", "Экспорт", "Выгрузка собственных полей IP-адресов", Permissions.IpAddresses.Read),
Guard/Core/Identity/PermissionDefinition.cs:22: new(Permissions.IpAddresses.Manage, "IP-адреса", "Управление", "Каталог и назначения сотрудникам", Permissions.IpAddresses.Read),
Guard/Core/Identity/PermissionDefinition.cs:23: new(Permissions.Classifiers.Read, "Классификаторы", "Просмотр", "Общий справочник"),
Guard/Core/Identity/PermissionDefinition.cs:24: new(Permissions.Classifiers.Manage, "Классификаторы", "Управление", "Изменение справочника", Permissions.Classifiers.Read),
Guard/Core/Identity/PermissionDefinition.cs:25: new(Permissions.Logs.Read, "Логи", "Просмотр", "Первоначально только Root", RootOnly: true),
Guard/Core/Identity/PermissionDefinition.cs:26: new(Permissions.Exports.ReadArchive, "Архив выгрузок", "Расследования", "Просмотр точных копий выгрузок", RootOnly: true),
Guard/Core/Identity/PermissionDefinition.cs:27: new(Permissions.Exports.Configure, "Архив выгрузок", "Хранение", "Настройка срока хранения", Permissions.Exports.ReadArchive, true),
Guard/Core/Identity/PermissionDefinition.cs:28: new(Permissions.Logs.Configure, "Логи", "Настройка", "Первоначально только Root", Permissions.Logs.Read, true),
Guard/Core/Identity/PermissionDefinition.cs:29: new(Permissions.Roles.Read, "Роли", "Просмотр", "Системная операция Root", RootOnly: true),
Guard/Core/Identity/PermissionDefinition.cs:30: new(Permissions.Roles.Manage, "Роли", "Управление", "Системная операция Root", Permissions.Roles.Read, true)
Guard/Core/Identity/PermissionDefinition.cs:36: if (selected.Any(code => !All.Any(p => p.Code == code)))
Guard/Core/Identity/PermissionDefinition.cs:38: if (selected.Any(code => All.Single(p => p.Code == code).RootOnly))
Guard/Core/Identity/PermissionDefinition.cs:40: foreach (var permission in All.Where(p => selected.Contains(p.Code)))
Guard/Core/Identity/Permissions.cs:7: public const string Run = "Permissions.Synchronization.Run";
Guard/Core/Identity/Permissions.cs:11: public const string Read = "Permissions.Personals.Read";
Guard/Core/Identity/Permissions.cs:12: public const string ReadDetails = "Permissions.Personals.ReadDetails";
Guard/Core/Identity/Permissions.cs:13: public const string ReadArchive = "Permissions.Personals.ReadArchive";
Guard/Core/Identity/Permissions.cs:14: public const string Export = "Permissions.Personals.Export";
Guard/Core/Identity/Permissions.cs:15: public const string Write = "Permissions.Personals.Write";
Guard/Core/Identity/Permissions.cs:19: public const string Export = "Permissions.Subdivisions.Export";
Guard/Core/Identity/Permissions.cs:20: public const string Read = "Permissions.Subdivisions.Read";
Guard/Core/Identity/Permissions.cs:21: public const string ReadArchive = "Permissions.Subdivisions.ReadArchive";
Guard/Core/Identity/Permissions.cs:22: public const string Write = "Permissions.Subdivisions.Write";
Guard/Core/Identity/Permissions.cs:26: public const string Read = "Permissions.Users.Read";
Guard/Core/Identity/Permissions.cs:27: public const string Manage = "Permissions.Users.Manage";
Guard/Core/Identity/Permissions.cs:31: public const string Export = "Permissions.IpAddresses.Export";
Guard/Core/Identity/Permissions.cs:32: public const string Read = "Permissions.IpAddresses.Read";
Guard/Core/Identity/Permissions.cs:33: public const string Manage = "Permissions.IpAddresses.Manage";
Guard/Core/Identity/Permissions.cs:37: public const string Read = "Permissions.Classifiers.Read";
Guard/Core/Identity/Permissions.cs:38: public const string Manage = "Permissions.Classifiers.Manage";
Guard/Core/Identity/Permissions.cs:43: public const string Read = "Permissions.Logs.Read";
Guard/Core/Identity/Permissions.cs:44: public const string Configure = "Permissions.Logs.Configure";
Guard/Core/Identity/Permissions.cs:48: public const string ReadArchive = "Permissions.Exports.ReadArchive";
Guard/Core/Identity/Permissions.cs:49: public const string Configure = "Permissions.Exports.Configure";
Guard/Core/Identity/Permissions.cs:53: public const string Read = "Permissions.Roles.Read";
Guard/Core/Identity/Permissions.cs:54: public const string Manage = "Permissions.Roles.Manage";
Guard/Core/Identity/Permissions.cs:56: public static List<string> GetAll() => PermissionCatalog.All.Select(p => p.Code).ToList();
```

## Контрольные суммы схемы и миграций (SHA256)

```text
Guard/Core/1-Entities/Abstarct/BaseEntity.cs 535399CB38EB1298A90A7C624F07019E675502A79C3B6DD8FADA62455AF4316C
Guard/Core/1-Entities/Abstarct/HasArchiveBatchIdEntity.cs 1CBF7452F71FC72AB4895BD432955F332A235A17A8304F9D6E04E73F84619E2B
Guard/Core/1-Entities/Abstarct/HasFolderEntity.cs 5679790E17388437B752EF1DB7490801643EFAA0142C7E96F3CAEB33E7DA751C
Guard/Core/1-Entities/Abstarct/IHasSubdivision.cs 7E900809ADD9C15D0E85E842515186FB00E41A81667E720C4882145EE3E97408
Guard/Core/1-Entities/Concrates/AccountPolicy.cs 8F466438D63C8F921951D04AA12BEE1F07976F8778882774F3127DB54288850E
Guard/Core/1-Entities/Concrates/ApiSynchronization.cs 273EA66F7786EEB1574042588821E781A4302AD4339E6DF1E24CD290DD444168
Guard/Core/1-Entities/Concrates/ApplicationRole.cs ED7399D1A6156190EEF00EBA995327E162EC2E31A9CC1CB7C35BA8C2D37B6E68
Guard/Core/1-Entities/Concrates/ApplicationUser.cs 26DAFC7DE8B226A812FE549AEE1CDA59B14E56383CC430BBD589D16532ED4FC2
Guard/Core/1-Entities/Concrates/Classifier.cs 3DDBBCA12143834E43ED7CBD1902EB519C937CB187A705D30EA0C4DD103995C2
Guard/Core/1-Entities/Concrates/ExportJob.cs 58D5A59DC68AB6811FC7AC32795D52F0BBF01A9A8F6D37D81C109A1055D4F553
Guard/Core/1-Entities/Concrates/IpAddress.cs 56B6DB1B3D287B071A714AA81844621278CAF439BBC7AA0BA9A3D785CD4721C0
Guard/Core/1-Entities/Concrates/LogEntry.cs 5C4AF30579B6DAE56F3E34995CA0510B610B0EC2F02C9BEAA378C96472B68B5A
Guard/Core/1-Entities/Concrates/Personal.cs 06F27EAF53EC5DE724B4532444B1E6049CCC86075100016DBD3F134F4FE6A03E
Guard/Core/1-Entities/Concrates/Subdivision.cs 643E1E5DDC6B744738FB369EF7E7CF656F87044C86C0909BB1B17D2B62752C62
Guard/Core/2-Configurations/Abstarct/BaseEntityConfiguration.cs E3D9DBEBCF3FCA905F7291C74D2CA7CB5CDCAF97FF4EA54CD961F3B0DADD655B
Guard/Core/2-Configurations/Concrates/AccountPolicyConfiguration.cs 1B34AAB59DEF2ABF878B9B04CB04963F33F2DEC2128F2EE5FB40469870D95F62
Guard/Core/2-Configurations/Concrates/ApiSynchronizationConfiguration.cs 036D21012F964FA19320CFA5C7127AC28E103D5F9E294632CDC6AFBF317C401D
Guard/Core/2-Configurations/Concrates/ApplicationUserConfiguration.cs 529AF9B2727F415C74C41697421F293F152CBF60B9DACBB7422AB0E1B4947BE1
Guard/Core/2-Configurations/Concrates/ClassifierConfiguration.cs D3E343B28AB98C2E7FA3A8B141293075475603ECBA9979F0DCF95FABDCEA4B34
Guard/Core/2-Configurations/Concrates/ExportConfiguration.cs DCD1685D0C0468CB0DE5EB390C0309CC418F50D9B53591CC4711FC09CBE0F8B0
Guard/Core/2-Configurations/Concrates/IpAddressConfiguration.cs C1EC743F84E1DF6EBE631E5F953B27ED573AD9DA176F5854341B289E522D71B4
Guard/Core/2-Configurations/Concrates/PersonalConfiguration.cs 10E5A2E7CC1A44FDFD84D30F87E9CF9CFC4FB537C655770D7688985719C1904F
Guard/Core/2-Configurations/Concrates/SubdivisionConfiguration.cs DF3E76C192FD8DBF3153B7A6C680A4C189D89B8EDB741698F8F52EAD0B11CC61
Guard/Core/3-Contexts/ApplicationDbContext.cs 672A46CF7AEF8E86F869425260E6BA436DA65F22D1266158AC8718F9B110CB8F
Guard/Core/3-Contexts/ApplicationDbContextDesignFactory.cs 549ECB151B0F239D15DB2E208CCF1D6FBE8B3BC59DD31F2043A2822896E29CA3
Guard/Core/3-Contexts/DatabaseInitializer.cs 3715588994FC8913CC2209D4761AA7CDC2591935101A5F5D944558A33F5EE2AE
Guard/Core/3-Contexts/DatabaseSeeder.cs 800EA9A762B2D346CEE1E4F7B98C5725DA78A91C6A71E61AD592BB66326E49EA
Guard/Core/3-Contexts/LogsDbContext.cs DA7B6BAE548D07A8BB6687766491D364FCD7EF244C199529084295AAE9BABA1E
Guard/Migrations/20260610123225_InitialCreate.cs 613BBED55CBB61BBDE5462FC57FFEAD297FBE2151486736FB8B5AEEEA6B1D9E4
Guard/Migrations/20260610123225_InitialCreate.Designer.cs BA9F4768268D503C325625D86249CD6867CF0DE712EF26B6B0C8116BA66944FD
Guard/Migrations/20260717140552_AddIpAddressCatalogWithManyToMany.cs 29E068820B8E411004A89B9A299EDCA5DD21A7BEEFDB0A52A1FDF53C9BBB3AE4
Guard/Migrations/20260717140552_AddIpAddressCatalogWithManyToMany.Designer.cs 49ABCF7676E8EB39FC4F8E7BB68FC4269DC9F9F3684B2002491D102F96D04D75
Guard/Migrations/20260720090311_123123123.cs F8610CCEFBF47451BDC197C8E2DEB408A269D6033C10C06425854EF981F71CDB
Guard/Migrations/20260720090311_123123123.Designer.cs C5F14AAE8471BF17C805C9CE79DA482126F89FA07EEA9CC81AB780F82EC4DA2F
Guard/Migrations/20260723120841_AddIp.cs 85A5190FAC172463F98926D5A4330818F2F14D523FEC93C4C51B6F8AA084838A
Guard/Migrations/20260723120841_AddIp.Designer.cs F70CC012620895E3405ACC319A086E1B33F47753EFC2784C13305C4AE1BCB4B5
Guard/Migrations/20260805121645_AddSubdivision.cs DF8E2BBB859BEB381A133F27C6EDA81CCEA8DDFC40088AF7AFAB5E8B68B8C78D
Guard/Migrations/20260805121645_AddSubdivision.Designer.cs 4D37973E626236E51959476C95D0924B498669AF84D75C56FF421A5215BAC6C9
Guard/Migrations/20260915073735_AddClassifiers.cs 4E677F65F20767B3FFE41A67A7DA9BD58326D021B64E42E162AF20B18C65AF36
Guard/Migrations/20260915073735_AddClassifiers.Designer.cs 69C1351BA5E6DC9730E1932CF6A9D20820EEEE5E86BB2A2553C0CE9C5BA8F20C
Guard/Migrations/20260915075544_AddClassifiers1.cs 66A877249667A25F29ED5875F98F1E28ED79B21C7986307E1431AAD74C188535
Guard/Migrations/20260915075544_AddClassifiers1.Designer.cs 98A790E60679320B74DE6E7A682A7CF57308CA662001B28AACF04FC69539781E
Guard/Migrations/20260917085422_ChangeSub.cs C877C039C8A46657C62294F442E2E068D98C72D1BE9EE6B47653AE6E6541EABE
Guard/Migrations/20260917085422_ChangeSub.Designer.cs 52A8FDB044EF545F56DF09F6AF359C07BE5CC545C86839BFF5E189C81DA285C5
Guard/Migrations/20260921110232_AddPersonals.cs CA56A0031CC4436A04E7C47EB44DFE7D91C4C40D61905DC912A499CA7FE7975D
Guard/Migrations/20260921110232_AddPersonals.Designer.cs DD06FB7EC09D40ED8AB803F44CF47AF9D8C7AC1FB47403C8163AEFE4594CAC68
Guard/Migrations/20260921110549_AddPersonals1.cs E7122CAA094AA56675A345170D268F4E9A1A12F7C0A52D3E5F95D6CFED880863
Guard/Migrations/20260921110549_AddPersonals1.Designer.cs C75E63CFF45DAC45C0794F0CCA4AF6F564694589F266133CDE98234AB04AC515
Guard/Migrations/20260921123101_AddPersonals2.cs FBA91E01A87510E0A81C16824E067B5706BECE8CD1D90029D4695D43CC047182
Guard/Migrations/20260921123101_AddPersonals2.Designer.cs F53D8130A30B665B708ECEE70449FCC3A3E0166F09B133B4BA203E0C441E8ED5
Guard/Migrations/20260922071849_Mig11.cs 56124D975803B3A3F1D11C5436F077AAD46820BDA5665D40A44B69EF449EEA6C
Guard/Migrations/20260922071849_Mig11.Designer.cs 3931B17BACE06BD730433F6A4E672A2D67EC12870A636A13A608DE0BDE261681
Guard/Migrations/20260922082844_Mig12.cs E2597B05212E601A06737882AA78FC1F4E3B3F8E774029961A46BFBEB07D3503
Guard/Migrations/20260922082844_Mig12.Designer.cs 07C7B054B099D38C4F508912F0B1E655EFAA4A5F5E491CFB60C4C35E9F572F0C
Guard/Migrations/20260922090427_Mig13.cs 7EFF6A5D85095963B1C3835D998A5098C218134B872367E0AD8417BD1A4BBB84
Guard/Migrations/20260922090427_Mig13.Designer.cs 271027C2315A20106EA486E21867F985B55401B6D3A066D0705D174D49912555
Guard/Migrations/20260922092706_Mig14.cs EDBDEC5C69506DA0479CA8541C50CE662CA844E163843FBB9B2CFD1175C71334
Guard/Migrations/20260922092706_Mig14.Designer.cs 6AF0DDAC1D6CF018BCB1764C5112708D749517AA2EFC6180DAA728E14873EE41
Guard/Migrations/20260922093211_Mig15.cs AF1156476D42C1883A6B82C720C9779B698BFC0400FBEBCC8A437ABFBC355F70
Guard/Migrations/20260922093211_Mig15.Designer.cs 374C8261F2583D476BAB953EDBE90B7F4907681CFEAE6DA283BE57F156F4CCCD
Guard/Migrations/20260922110604_Addop4.cs DC8D9F99C1E7FA4F3A2BEB4F008C3B98CC2B9C93D50104C7384D54A09B0C1367
Guard/Migrations/20260922110604_Addop4.Designer.cs A16775D4826F0D40EF795C1E075742BCA64131F8B8CF1F330D141B57EDEB87DC
Guard/Migrations/20260922123419_Addop43.cs FC96454885CD63F6724775F9AE85C00D0064DE546F070B55E21BA3CDC1985CD0
Guard/Migrations/20260922123419_Addop43.Designer.cs D271E718BB3C6DD69A88D693E05E2E0A97D403E943959EB4E4F52D8C2FFD96AE
Guard/Migrations/20260922130426_Addop433.cs 00EA5F422AA679C513534E159ABE2C795B5D60212BA45F0226252A43B6BA63DD
Guard/Migrations/20260922130426_Addop433.Designer.cs 06E1FC8B317255DAF272F0406FC54966013E9E36E568BEE1B1EC4D457B2A8BCE
Guard/Migrations/20260923074544_Apt67.cs 5587D23F914DF66F7727DF158156BC401FBD820C87A523E36AA74A8D3BA66236
Guard/Migrations/20260923074544_Apt67.Designer.cs 53483594B0C29A2E1C4D5B0C41B469AEC2792272C21FA03879C9A88E6CF70DD6
Guard/Migrations/20260923075328_Apt617.cs 22CA3B7E55A9A7B90C1C84E395810E53799B29BBEA58688AF34986CF9131AFFE
Guard/Migrations/20260923075328_Apt617.Designer.cs 6A4DE4E029475F935E9BAEFF60DCC3D43CF3693869FC51FDF6EBAEE31313F8C0
Guard/Migrations/20260923124507_Awd22.cs B464DF76D5829460DAE684C7527DDE6B6ECB7EA28D8725034C6A4296B62EA286
Guard/Migrations/20260923124507_Awd22.Designer.cs 8E6A652F898F5F0AEA66895406F300D33F9833841700C4D021C8DA076E5064C0
Guard/Migrations/20260929065524_Mig554.cs 1397F69A49852359BB39CA5B12DDC5547E974B5A44E11BBEE03AB9CD88B9921D
Guard/Migrations/20260929065524_Mig554.Designer.cs 01D65E6B8AF90BF793F16FF472D1C9C2077DD3130FC8848BF281A088D82248F2
Guard/Migrations/20260929070747_Mig5534.cs 40A529FFAB94C589A85DD2972D6752C930C70DCE27EF5579E1B417B927D1A5CA
Guard/Migrations/20260929070747_Mig5534.Designer.cs DC27E4E55D24A18B4B93A5362252D2F58E6263BF32A57EFBF3BAEA2074521C50
Guard/Migrations/20260929113434_ad-ng.cs 75BC065E977DC4B772F01BD17446B36BB83E55A4BCC374F6EAA71C2ED88DCB54
Guard/Migrations/20260929113434_ad-ng.Designer.cs 82897C4B72C725753BB0329BE55908D480E837C01A24BC611C15D27DFDA129EF
Guard/Migrations/20260929121404_ad-ng1.cs 3704680E748FEF06F51574932714B24488DA310BAA167CE6C9F8830A850CC570
Guard/Migrations/20260929121404_ad-ng1.Designer.cs 9D9B22027CFCD3EBC2E519AD0BEC0FE23B41186ABB83B9A6ED0B73297730FA50
Guard/Migrations/20260929121910_ad-ng13.cs A585E334ABB2CF5D4ACAF4644F1CF470B4C004883E3D1B004D6FEA793F9DA692
Guard/Migrations/20260929121910_ad-ng13.Designer.cs 30399354441D2B1EEE266EABBE071CA9661E0D8A0A11D13E223A2035DD04CF58
Guard/Migrations/20261001174220_fgr12.cs 0DB81F93B092223970ED02D21B36576CEF7EC62F352B025E4949E2EDA611DD43
Guard/Migrations/20261001174220_fgr12.Designer.cs 970165F430B6012175C916BDCA5F762F029CDB13E2202CC47B5EB4C4699DCAA4
Guard/Migrations/20261008100218_AddAccountPolicy.cs 8B13BF2F52E3255538A128A23BAE3245A0775590C8304C8FD48EE0C241D09279
Guard/Migrations/20261008100218_AddAccountPolicy.Designer.cs 02E52C033A76135F61F2ECE92B8C2FB44F7BA0DB5FBB005C8B6007A2ADF113C3
Guard/Migrations/20261008160000_AddEntityVersions.cs 6F5DF5D8D8681DF0E4572667A3AAF5F928A4A030DB54872C62282AF181B3B375
Guard/Migrations/20261008160000_AddEntityVersions.Designer.cs BE56807E75F7AB60F21BE7912420CDFC9D079754F30D1F4C583346C386EFF153
Guard/Migrations/20261009090000_AddLoginFailurePolicy.cs 9EA072418E6FDB0FCAD5AAD9FFF69506F795F28359E605E95B4D16DAE936D57E
Guard/Migrations/20261009090000_AddLoginFailurePolicy.Designer.cs F01BA8D3D78A5A7CEE98765E24B464FE450A91022DFB6AC8F3E304CC1CD10DFF
Guard/Migrations/20261009103606_AddApiSynchronization.cs FC73B25B28497F151A2FC9121D0694E8B463ADCC5BB2BD2CF131DA8CF2FCA4D8
Guard/Migrations/20261009103606_AddApiSynchronization.Designer.cs A704797FED97028FF73FD96F1507F0AE0BB8143B4E56D8D156727509E612FEB4
Guard/Migrations/20261009114922_AddApiSourceSettings.cs 87E72B09772BB87F2B726B1E21381449BF564D2BDE64A2A4ED86CCE84D48C551
Guard/Migrations/20261009114922_AddApiSourceSettings.Designer.cs 063AF2B52704C612FAA108EEB3150723634333166AE10C5EACF00F08FAD382B6
Guard/Migrations/20261009120337_AddApiBasicCredentials.cs A8A56B9AE665ACDD56262C01C6376268C6A57B1877AD691746EDA379C8D20F9B
Guard/Migrations/20261009120337_AddApiBasicCredentials.Designer.cs 21DDD5F4B80D9DF529D10DC6F6842315407C9D172A05088CDA8F4101D96FFABE
Guard/Migrations/20261009133225_AddSynchronizationUpdatedSince.cs DAC0F9C04304D1E7BEB90E0296DEF1E8DB926E27C3F55A80EC2E2823E7D5AB18
Guard/Migrations/20261009133225_AddSynchronizationUpdatedSince.Designer.cs 7627D36617DDEFAF812CBB83786EEE08F8F381F517A0B2B79EEA617028DD895E
Guard/Migrations/20261010130000_AddApiHeadSubdivision.cs 9DED8252D8E8955F9CAE892A14716A4DE6CFEB178F46882B4C162531EBFE96AB
Guard/Migrations/20261010130000_AddApiHeadSubdivision.Designer.cs 169B8106E1922B0AC2583556C4BBE1B7F0AD35277B00225922409EF196195D6A
Guard/Migrations/20261014073616_AddApiExitEventsAndQueue.cs DE50F474EA13E7BEC26714ED129CB1F4F900BD3A188C3A48A0CC850D51796470
Guard/Migrations/20261014073616_AddApiExitEventsAndQueue.Designer.cs 35796C44EF3F8577584E9F8F2F14E9BF805509BB25ACABC4CCF6CE94440641C5
Guard/Migrations/20261014080024_AddApiRecoveryWindow.cs 801334B2BD454853DE5B0C13842F6243ECA0B72AD22C7ED7BF562A16B5C056FB
Guard/Migrations/20261014080024_AddApiRecoveryWindow.Designer.cs 073B2FCBA6B9D6DB1246C9787CDEB4654369E98ED328AA809AA1635E2A7F0699
Guard/Migrations/20261014132937_AddDataExport.cs 1CA1EFE49A96866AB1B22EC82403DAE7195C43CD9ACFE34AC43C3F25DAF1A149
Guard/Migrations/20261014132937_AddDataExport.Designer.cs 9C7DFBC7A9AE88034319C5BE9ACA518E8FCA6675C6F84B368321677E71BB6062
Guard/Migrations/20261015174116_AddApiSnapshotConfirmation.cs E619144B815AB24F46068C1B8CC0C6E134C93C5EBE12ED40EC961EDE1A556A56
Guard/Migrations/20261015174116_AddApiSnapshotConfirmation.Designer.cs 162CD9BA9636B5E8ABFE64B80603F7E91220882C4F0B9FE7B1B5DAB68ABD38F5
Guard/Migrations/ApplicationDbContextModelSnapshot.cs D04CCC10981DDC233EE76DB7B5F0AFBE5AF9CC1402575AE69882D48D24916895
```
