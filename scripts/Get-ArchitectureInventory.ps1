param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    # Только исходники: не читать конфигурацию, рабочие данные, bin/obj и секреты.
    $sourceFiles = @(& rg --files Guard Guard.Domain -g '*.cs' -g '*.razor' -g '!**/Migrations/**' -g '!**/obj/**' -g '!**/bin/**' | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось перечислить исходники через rg.' }
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# Статический инвентарь архитектуры Guard')
    $lines.Add('')
    $lines.Add('Создан scripts/Get-ArchitectureInventory.ps1. Поиск текстовый: совпадение не доказывает вызов или нарушение границы. Комментарии могут попадать в выдачу. Контрольные суммы фиксируют файлы, а не соответствие рабочей БД модели EF.')
    $lines.Add('')

    function Add-Matches([string] $title, [string[]] $files, [string] $pattern) {
        $lines.Add("## $title")
        $lines.Add('')
        $lines.Add('```text')
        foreach ($file in $files) {
            foreach ($hit in Select-String -LiteralPath $file -Pattern $pattern) {
                $path = $file.Replace('\', '/')
                $lines.Add("${path}:$($hit.LineNumber): $($hit.Line.Trim())")
            }
        }
        $lines.Add('```')
        $lines.Add('')
    }

    $coreFiles = @($sourceFiles | Where-Object { $_ -match '[/\\]Core[/\\]' -and $_ -notmatch '[/\\]Migrations[/\\]' })
    $uiFiles = @($sourceFiles | Where-Object { $_ -match '[/\\]Components[/\\]' })
    $contracts = @($coreFiles | Where-Object { (Split-Path $_ -Leaf) -cmatch '^I[A-Z].*\.cs$' })
    Add-Matches 'Контракты операций и репозиториев (включая legacy)' $contracts '^\s*(Task|ValueTask|IQueryable|IEnumerable|IReadOnly|void|bool|event|Func|CancellationToken|Guid|string|int|I[A-Z]).*(\(|\{|;)'
    Add-Matches 'Публичные операции конкретных сервисов (включая интерфейсы в общих файлах)' @($coreFiles | Where-Object { $_ -match '6-Services' }) '^\s*(public (async )?(Task|ValueTask)|Task<).*\('
    Add-Matches 'IQueryable и утечки Identity в сервисах/DTO' @($coreFiles | Where-Object { $_ -match '6-Services' }) 'IQueryable|IdentityResult|ApplicationUser|ApplicationRole|Radzen'
    Add-Matches 'EF в UI и потребители query-методов' $uiFiles 'EntityFrameworkCore|\.(Query\w*Async|CountAsync|ToListAsync|Include|ThenInclude)\('
    Add-Matches 'Прямое разрешение DI и фабрики scope' $sourceFiles 'GetRequiredService|GetService<|IServiceProvider|CreateAsyncScope|CreateScope\('
    Add-Matches 'Транзакции и блокировки' $coreFiles 'BeginTransaction|CommitTransaction|ExecuteInTransaction|advisory|SemaphoreSlim|ManagementLock|ApiSynchronization =|ExportQueue =|FOR UPDATE'
    Add-Matches 'Конфигурация схемы' @($coreFiles | Where-Object { $_ -match '2-Configurations|3-Contexts' }) 'HasKey|HasIndex|HasForeignKey|HasPrincipalKey|OnDelete|IsConcurrencyToken|IsRowVersion|HasDefaultValue|HasOperators|IsUnique|ToTable|UsingEntity|DbSet<'
    Add-Matches 'Маршруты и endpoints' @($sourceFiles | Where-Object { $_ -match 'Components|ExportEndpoints' }) '@page|Map(Get|Post|Group)|MapAdditionalIdentityEndpoints|MapExportEndpoints'
    Add-Matches 'Permissions и состояния' @($coreFiles | Where-Object { $_ -match 'Permissions\.cs$|PermissionDefinition\.cs$|Status\.cs$|StatusTransitions\.cs$' }) 'const string|new\(| = [0-9]|=>|StatusOperation'

    $lines.Add('## Контрольные суммы схемы и миграций (SHA256)')
    $lines.Add('')
    $lines.Add('```text')
    $schemaFiles = @(& rg --files Guard/Migrations Guard/Core/2-Configurations Guard/Core/1-Entities Guard.Domain/Core/1-Entities Guard/Core/3-Contexts -g '*.cs' | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось перечислить файлы схемы.' }
    foreach ($file in $schemaFiles) {
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        $lines.Add("$($file.Replace('\', '/')) $hash")
    }
    $lines.Add('```')
    $lines.Add('')
    # Вывод в stdout: вызывающий выбирает файл отчёта. БД/приложение не запускаются.
    ($lines -join "`n").TrimEnd()
}
finally {
    Pop-Location
}
