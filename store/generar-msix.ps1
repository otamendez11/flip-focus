# Genera el paquete MSIX para Microsoft Store a partir del paquete autocontenido.
#
# Necesita dos datos que solo aparecen al reservar el nombre de la app en el
# Partner Center: el Identity Name y el Publisher (los dos son cadenas de texto).
#
# Uso:
#   .\generar-msix.ps1 -IdentityName "12345EduardoOtamendez.FlipFocus" -Publisher "CN=1A2B3C4D-...."
#
# Opcional: -Firmar para firmarlo con un certificado de prueba (la Store lo
# vuelve a firmar con el suyo, asi que para enviarlo no hace falta).

param(
    [Parameter(Mandatory = $true)][string]$IdentityName,
    [Parameter(Mandatory = $true)][string]$Publisher,
    [string]$PublisherDisplayName = "Eduardo Otamendez",
    [string]$Version = "1.0.0.0",
    [switch]$Firmar
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$msix = Join-Path $raiz 'msix'
$origen = Join-Path $raiz 'dist-app'
$paquete = Join-Path $msix 'paquete'
$salida = Join-Path $msix "FlipFocus-$Version-x64.msix"

$tool = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter makeappx.exe |
    Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
if (-not $tool) { throw 'No encuentro makeappx.exe (paquete NuGet Microsoft.Windows.SDK.BuildTools).' }
$makeappx = $tool.FullName

if (-not (Test-Path "$origen\FlipFocus.exe")) {
    throw "Falta el paquete autocontenido en $origen. Ejecuta antes: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o dist-app"
}

Write-Host "Preparando la carpeta del paquete..."
if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
New-Item -ItemType Directory -Force -Path $paquete | Out-Null
Copy-Item "$origen\*" $paquete -Recurse -Force
Copy-Item (Join-Path $raiz 'LEEME.txt') (Join-Path $paquete 'LEEME-es.txt') -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $raiz 'README-en.txt') (Join-Path $paquete 'README.txt') -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $msix 'Assets') (Join-Path $paquete 'Assets') -Recurse -Force

Write-Host "Escribiendo el manifiesto..."
$manifiesto = Get-Content (Join-Path $msix 'AppxManifest.template.xml') -Raw
$manifiesto = $manifiesto.Replace('IDENTITY_NAME', $IdentityName)
$manifiesto = $manifiesto.Replace('PUBLISHER_DISPLAY_NAME', $PublisherDisplayName)
$manifiesto = $manifiesto.Replace('PUBLISHER', $Publisher)
$manifiesto = $manifiesto.Replace('VERSION', $Version)
Set-Content (Join-Path $paquete 'AppxManifest.xml') $manifiesto -Encoding utf8

Write-Host "Empaquetando (tarda un par de minutos)..."
& $makeappx pack /d $paquete /p $salida /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx fallo al empaquetar.' }

# makeappx pack ya valida el manifiesto contra el esquema. Ademas se comprueba
# que el paquete se puede volver a abrir y que dentro esta la app.
Write-Host "Comprobando el paquete..."
$verificacion = Join-Path $msix 'verificacion'
if (Test-Path $verificacion) { Remove-Item $verificacion -Recurse -Force }
& $makeappx unpack /p $salida /d $verificacion /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'El paquete no se puede volver a abrir.' }
$exe = Join-Path $verificacion 'FlipFocus.exe'
$mani = Join-Path $verificacion 'AppxManifest.xml'
if (-not (Test-Path $exe)) { throw 'Dentro del paquete no esta FlipFocus.exe.' }
if (-not (Test-Path $mani)) { throw 'Dentro del paquete no esta AppxManifest.xml.' }
$leido = Get-Content $mani -Raw
if ($leido -notmatch [regex]::Escape($IdentityName)) { throw 'El manifiesto no lleva el Identity Name indicado.' }
if ($leido -notmatch [regex]::Escape($Publisher)) { throw 'El manifiesto no lleva el Publisher indicado.' }
"  contiene la app: si"
"  contiene el manifiesto con la identidad: si"
"  archivos dentro: $((Get-ChildItem $verificacion -Recurse -File | Measure-Object).Count)"
Remove-Item $verificacion -Recurse -Force

if ($Firmar) {
    Write-Host "Firmando con un certificado de prueba..."
    $signtool = (Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter signtool.exe |
        Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1).FullName
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
        -FriendlyName 'Flip Focus (prueba)' -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    & $signtool sign /fd SHA256 /a /f (Export-PfxCertificate -Cert $cert -FilePath (Join-Path $msix 'prueba.pfx') -Password (ConvertTo-SecureString 'flipfocus' -AsPlainText -Force)).FullName /p flipfocus $salida
    Write-Host "El certificado de prueba queda en Cert:\CurrentUser\My y en msix\prueba.pfx (clave: flipfocus)."
}

$tamano = [math]::Round((Get-Item $salida).Length / 1MB, 1)
Write-Host ""
Write-Host "LISTO: $salida  ($tamano MB)"
Write-Host "Sube ese archivo en el Partner Center, en la pagina del envio."
