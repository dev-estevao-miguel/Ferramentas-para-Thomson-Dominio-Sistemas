#requires -Version 5.1
<############################################################
Coletor do B.T: diagnostico, teste ODBC e exportacao de metadados.
Usado pelo aplicativo (embutido no executavel) ou diretamente pelo PowerShell.
Somente SELECTs fixos de metadados; nao executa SQL informado pelo usuario
e nao consulta registros das tabelas.
#############################################################>
[CmdletBinding()]
param(
    [ValidateSet('Diagnostico', 'Testar', 'Exportar')]
    [string]$Modo = 'Diagnostico',
    [string]$Dsn,
    [string]$Usuario,
    [ValidateNotNullOrEmpty()][string]$Esquema = 'DBA',
    [System.Management.Automation.PSCredential]$Credencial,
    [string]$Saida,
    [ValidateRange(1, 300)][int]$Timeout = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
# Mesma pasta lida pelo aplicativo (AppPaths.DefaultCatalogsFolder).
if (-not $Saida) { $Saida = Join-Path $env:LOCALAPPDATA 'BuscaTabelasDemo\Catalogos' }

function Get-OdbcFailureCode($Exception) {
    $cause = $Exception
    while ($cause -and -not ($cause -is [System.Data.Odbc.OdbcException])) {
        $cause = $cause.InnerException
    }
    if ($cause) {
        return (($cause.Errors | ForEach-Object {
            '{0}/{1}' -f $_.SQLState, $_.NativeError
        } | Select-Object -Unique) -join ', ')
    }
    return $Exception.GetType().Name
}

function Read-Metadata([string]$Sql, [object[]]$Parameters = @()) {
    $command = $connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $Timeout
    $reader = $null
    try {
        foreach ($value in $Parameters) {
            $parameter = $command.Parameters.Add('p', [System.Data.Odbc.OdbcType]::VarChar, 128)
            $parameter.Value = $value
        }
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $record = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $record[$reader.GetName($i).ToLowerInvariant()] = if ($reader.IsDBNull($i)) {
                    $null
                } else { $reader.GetValue($i) }
            }
            [pscustomobject]$record
        }
    } finally {
        if ($reader) { $reader.Dispose() }
        $command.Dispose()
    }
}

if ($Modo -eq 'Diagnostico') {
    [pscustomobject]@{
        processo_bits = [IntPtr]::Size * 8
        drivers = @(Get-OdbcDriver | Where-Object Name -like '*SQL Anywhere*' |
            Select-Object Name, Platform)
        conexoes = @(Get-OdbcDsn | Where-Object DriverName -like '*SQL Anywhere*' |
            Select-Object Name, Platform, DsnType, DriverName)
    } | ConvertTo-Json -Depth 5
    return
}

if ([string]::IsNullOrWhiteSpace($Dsn)) { throw 'Informe -Dsn com a conexao de teste.' }
if ($Esquema.Length -gt 128) { throw 'O nome do esquema excede 128 caracteres.' }
$platform = if ([IntPtr]::Size -eq 4) { '32-bit' } else { '64-bit' }
$available = @(Get-OdbcDsn -Platform $platform | Where-Object Name -eq $Dsn)
if (-not $available.Count) {
    throw "DSN nao encontrado em $platform. Para DSN 32-bit, execute com Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe."
}
if ($available[0].DriverName -notlike '*SQL Anywhere*') {
    throw 'O coletor atende apenas ao SQL Anywhere.'
}
if (-not $Credencial) {
    Write-Host "Conexao: $Dsn"
    $user = $Usuario
    if ([string]::IsNullOrWhiteSpace($user)) {
        $user = Read-Host 'Usuario do banco (somente leitura)'
    } else {
        Write-Host "Usuario: $user"
    }
    $securePassword = Read-Host 'Senha (nao sera gravada)' -AsSecureString
    $Credencial = [pscredential]::new($user, $securePassword)
}

$builder = [System.Data.Odbc.OdbcConnectionStringBuilder]::new()
$builder['DSN'] = $Dsn
$builder['UID'] = $Credencial.UserName
$builder['PWD'] = $Credencial.GetNetworkCredential().Password
$connection = [System.Data.Odbc.OdbcConnection]::new($builder.ConnectionString)
$builder.Clear()
try {
    $connection.ConnectionTimeout = $Timeout
    $connection.Open()
    $info = @(Read-Metadata "SELECT PROPERTY('ProductVersion') AS versao_servidor, DB_PROPERTY('Name') AS banco")
    $tables = @(Read-Metadata ([IO.File]::ReadAllText((Join-Path $scriptDirectory 'sql\tabelas.sql'))) @($Esquema))
    if (-not $tables.Count) {
        throw 'Nenhuma tabela visivel no esquema solicitado. Confira banco, esquema e permissoes.'
    }
    if ($Modo -eq 'Testar') {
        [pscustomobject]@{
            status = 'conectado'
            servidor = $info
            esquema = $Esquema
            tabelas_visiveis = $tables.Count
        } | ConvertTo-Json -Depth 5
        return
    }
    Write-Host "Tabelas/views encontradas: $($tables.Count). Lendo metadados das colunas..."
    $columns = @(Read-Metadata ([IO.File]::ReadAllText((Join-Path $scriptDirectory 'sql\colunas.sql'))) @($Esquema))
    if (-not $columns.Count) { throw 'Nenhuma coluna retornada; exportacao cancelada.' }

    # Dicionario de codigos: opcional e especifico de cada produto. Sem sql\valores.sql, o catalogo
    # e salvo sem codigos (status_valores = indisponivel).
    $values = @()
    $valueStatus = 'indisponivel'
    $warnings = @()
    $valuesSql = Join-Path $scriptDirectory 'sql\valores.sql'
    if (Test-Path -LiteralPath $valuesSql) {
        Write-Host "Colunas encontradas: $($columns.Count). Lendo dicionario de valores..."
        try {
            $values = @(Read-Metadata ([IO.File]::ReadAllText($valuesSql)) @($Esquema))
            $valueStatus = if ($values.Count) { 'disponivel' } else { 'vazio' }
        } catch {
            $warnings += 'Valores do dicionario indisponiveis: ' + (Get-OdbcFailureCode $_.Exception)
        }
    }
    $catalog = [ordered]@{
        formato = 'bt-catalogo-v1'
        gerado_em = [DateTimeOffset]::Now.ToString('o')
        servidor = $info
        esquema = $Esquema
        escopo = 'Metadados; sem registros operacionais; sem chaves estrangeiras.'
        status_valores = $valueStatus
        avisos = $warnings
        tabelas = $tables
        colunas = $columns
        valores = $values
    }
    $folder = Join-Path $Saida ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $null = New-Item -ItemType Directory -Path $folder -Force
    $output = Join-Path $folder 'catalogo.json'
    Write-Host 'Gravando catalogo JSON...'
    $json = ConvertTo-Json -InputObject $catalog -Depth 12
    [IO.File]::WriteAllText($output, $json, [Text.UTF8Encoding]::new($false))
    Write-Output "Catalogo: $output"
    Write-Output "Tabelas: $($tables.Count); colunas: $($columns.Count); valores: $($values.Count)."
    foreach ($warning in $warnings) { Write-Warning $warning }
} catch {
    $code = Get-OdbcFailureCode $_.Exception
    if ($code -eq 'RuntimeException') { throw }
    if ($code -match '28000/-103') {
        Write-Host "Usuario/senha rejeitados pelo banco ($code)." -ForegroundColor Red
        Write-Host 'Confira o usuario, a senha e se ele tem permissao de leitura do catalogo nesta base.'
        exit 1
    }
    throw "Falha no teste/exportacao ($code). Confira DSN, servidor, usuario e permissoes. Nenhuma credencial foi gravada pelo BT."
} finally {
    $connection.Dispose()
    $Credencial = $null
}
