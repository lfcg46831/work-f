param(
    [string]$ProfilePath = "",
    [switch]$UseInstallPlan,
    [string[]]$Steps = @("full")
)

$ErrorActionPreference = "Stop"

# Define paths and variables
$downloadPath = "C:\TotalCheckout"
$downloadFolder = "$downloadPath\install-pos"
$logFile = "$downloadFolder\install-log.txt"

# .NET SDK Variables
$dotnetSDKVersion = "8.0.401"
$dotnetSDKInstallerUrl = "https://download.visualstudio.microsoft.com/download/pr/f5f1c28d-7bc9-431e-98da-3e2c1bbd1228/864e152e374b5c9ca6d58ee953c5a6ed/dotnet-sdk-8.0.401-win-x64.exe"
$dotnetSDKInstallerFile = "$downloadFolder\dotnet-sdk-$dotnetSDKVersion-win-x64.exe"
$dotnetSDKPath = "C:\Program Files\dotnet\sdk\$dotnetSDKVersion"

# JDK Variables
$jdkVersion = "17.0.11"
$jdkInstallerFile = "$downloadFolder\jdk-$jdkVersion`_windows-x64_bin.exe"
$jdkInstallDir = "C:\Program Files\Java\jdk-$jdkVersion"
$jdkLogFile = "$downloadFolder\jdk-install.log"
$jdkInstallerUrl = "https://download.oracle.com/java/17/archive/jdk-17.0.11_windows-x64_bin.exe"

# Microsoft Visual C++ Redistributable
$vcRedist86InstallerPath = "C:\TotalCheckout\PackagePOS\VC_redist.x86.exe"
$vcRedist64InstallerPath = "C:\TotalCheckout\PackagePOS\VC_redist.x64.exe"

# Define the paths for Epson JavaPOS
$epsonInstallFolder = "C:\TotalCheckout\PackagePOS\Epson_JavaPOS_ADK_11429"
$epsonInstallerName = "EPSON_JavaPOS_1.14.29.exe"
$epsonInstallerPath = Join-Path -Path $epsonInstallFolder -ChildPath $epsonInstallerName
$epsonRegistryKey = "HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Installer\Dependencies\{855D735A-B51F-446C-A4C5-73676BD59463}"
$epsonDisplayNameValue = "Epson JavaPOS ADK 1.14.29"

# Define the paths for Datalogic JavaPOS
$datalogicInstallFolder = "C:\TotalCheckout\PackagePOS\Datalogic\610008819"
$datalogicInstallerName = "JavaPOS_Setup.jar"
$datalogicInstallerPath = Join-Path -Path $datalogicInstallFolder -ChildPath $datalogicInstallerName
$datalogicRegistryKey = "HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Installer\Dependencies\{DATALOGIC-JAVAPOS}"
$datalogicDisplayNameValue = "Datalogic JavaPOS"

# Define the paths for Citizen JavaPOS
$citizenInstallFolder = "C:\TotalCheckout\PackagePOS\Citizen\JavaPOS_V1.14.0.5E"
$citizenInstallerName = "CSJ_JPOS11405_setup64EN.exe"
$citizenInstallerPath = Join-Path -Path $citizenInstallFolder -ChildPath $citizenInstallerName

# Define the paths for HP Cash Drawer Driver
$hpCashDrawerInstallFolder = "C:\TotalCheckout\PackagePOS\HP"
$hpCashDrawerInstallerName = "sp142606.exe"
$hpCashDrawerInstallerPath = Join-Path -Path $hpCashDrawerInstallFolder -ChildPath $hpCashDrawerInstallerName

# Path to Java executable
$javaPath = "C:\Program Files\Java\jdk-17.0.11\bin\java.exe"

# Define the DLL copying paths
$sourceDirectory = "C:\Program Files\EPSON\JavaPOS\bin"
$destinationDirectory = "C:\Program Files\Java\jdk-17.0.11\bin"
$dllFiles = @("BluetoothIO.DLL", "epsonjpos.dll", "EthernetIO31.DLL", "SerialIO31.dll", "USBIO31.DLL")

# Define the new directory to be added
$javaPOSNewPath = "C:\Program Files\Datalogic\JavaPOS\SupportJars"
# Get the current system PATH variable
$currentPath = [System.Environment]::GetEnvironmentVariable('Path', [System.EnvironmentVariableTarget]::Machine)

# Define the folder path
$folderPath = "C:\TotalCheckout\Database"

# Define the source and destination paths for nginx
$sourceFolderForNginx = "C:\TotalCheckout\PackagePOS\nginx"
$destinationFolderForNginx = "C:\nginx"

# Define the source and destination paths for nwjs
$sourceFolderForNwjs = "C:\TotalCheckout\PackagePOS\nwjs"
$destinationFolderForNwjs = "C:\nwjs"

# Define the source and destination paths for nssm
$sourceFolderForNssm = "C:\TotalCheckout\PackagePOS\nssm"
$destinationFolderForNssm = "C:\nssm"

# Define the URL for the FFmpeg build and local download path
$ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$downloadPathFFmpeg = "$downloadFolder\ffmpeg-release-essentials.zip"
$extractPathFFmpeg = "C:\ffmpeg"

# Define the service name to create the services
$ServicePOSApiName = "TotalCheckoutPOS.Services.POS.Api"
$ServiceLOGCName = "TotalCheckoutPOS.Services.LOGC"
$ServicePOSCoreAPIName = "TotalCheckoutPOS.Services.POS.Core.API"
$UIUtilsSignalRCommunicationName = "TotalCheckoutPOS.UI.Utils.SignalRCommunication"
$ServiceUPDCName = "TotalCheckoutPOS.Services.UPDC"
$ServiceServerOlcasAPIName = "TotalCheckoutPOS.Services.Server.OlcasAPI"
$ServiceGatewayExternalPayments = "TotalCheckoutPOS.Services.Gateway.ExternalPayments"
$ServiceSmartRetention = "TotalCheckoutPOS.Services.SmartRetention.Api"
$NginxName = "Nginx"

# Define the source and destination paths for Services
$sourceServicesPath = "C:\TotalCheckout\PackagePOS\Solutions\"
$destinationReleasesPath = "C:\Releases"

# IaaS (POS) - NSSM service
$IaaSServiceName = "TotalCheckoutPOS.IaaS"
$IaaSExePath = "C:\POS_Main\IaaS.exe"
$IaaSWorkingDir = "C:\POS_Main"
$IaaSPort = "10000"
$IaaSConfigPath = "C:\POS_MAIN\Configuration.dat"
$IngelinkSourceFiles = @(
    "C:\TotalCheckout\PackagePOS\Ingelink\Configuration.dat",
    "C:\TotalCheckout\PackagePOS\Ingelink\RPConfig.dat"
)
$POSMainDestination = "C:\POS_MAIN"
$NSSM_PATH = "C:\nssm\win64\nssm.exe"

# Perfil POS carregado de um ficheiro JSON (opcional)
$PosProfile = $null

function Get-ProfileValue {
    param(
        [Parameter(Mandatory = $true)]
        $Node,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        $DefaultValue = $null
    )

    if ($null -eq $Node) {
        return $DefaultValue
    }

    if ($Node.PSObject.Properties.Name -contains $PropertyName) {
        return $Node.$PropertyName
    }

    return $DefaultValue
}

function ConvertTo-Hashtable {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$InputObject
    )

    $hash = @{}
    foreach ($property in $InputObject.PSObject.Properties) {
        $hash[$property.Name] = $property.Value
    }

    return $hash
}

function Load-PosProfile {
    param (
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Write-Output "No POS profile provided. Running with script defaults."
        return $null
    }

    if (-Not (Test-Path -Path $Path)) {
        throw "POS profile not found at '$Path'."
    }

    try {
        $rawProfile = Get-Content -Path $Path -Raw -Encoding UTF8
        $profile = $rawProfile | ConvertFrom-Json
        Write-Output "Loaded POS profile: $($profile.model)"
        return $profile
    } catch {
        throw "Failed to load POS profile from '$Path'. Error: $($_.Exception.Message)"
    }
}

function Apply-PosProfile {
    param (
        $Profile
    )
	if ($null -eq $Profile) { throw "POS profile is null. Ensure -ProfilePath is provided and the JSON file exists and is valid." }
	if ($null -eq $Profile.paths) { throw "Profile is missing 'paths' node." }
	if ($null -eq $Profile.environment) { throw "Profile is missing 'environment' node." }

    $global:sourceFolderForNginx = Get-ProfileValue -Node $Profile.paths -PropertyName "nginxSource" -DefaultValue $global:sourceFolderForNginx
    $global:destinationFolderForNginx = Get-ProfileValue -Node $Profile.paths -PropertyName "nginxDestination" -DefaultValue $global:destinationFolderForNginx

    $global:sourceFolderForNwjs = Get-ProfileValue -Node $Profile.paths -PropertyName "nwjsSource" -DefaultValue $global:sourceFolderForNwjs
    $global:destinationFolderForNwjs = Get-ProfileValue -Node $Profile.paths -PropertyName "nwjsDestination" -DefaultValue $global:destinationFolderForNwjs

    $global:sourceFolderForNssm = Get-ProfileValue -Node $Profile.paths -PropertyName "nssmSource" -DefaultValue $global:sourceFolderForNssm
    $global:destinationFolderForNssm = Get-ProfileValue -Node $Profile.paths -PropertyName "nssmDestination" -DefaultValue $global:destinationFolderForNssm

    $global:folderPath = Get-ProfileValue -Node $Profile.paths -PropertyName "databaseFolder" -DefaultValue $global:folderPath

    $global:Environment = Get-ProfileValue -Node $Profile.environment -PropertyName "releaseMode" -DefaultValue $global:Environment

    Write-Output "POS profile overrides applied successfully."
}

function Get-DevicesLibraryPath {
    $basePaths = @()

    $peripheralPathMap = @{
        "datalogic-scanner" = @(
            "C:\Program Files\Datalogic\JavaPOS",
            "C:\Program Files\Datalogic\JavaPOS\SupportJars"
        )
        "epson-printer" = @(
            "C:\Program Files\EPSON\JavaPOS\lib",
            "C:\Program Files\EPSON\JavaPOS\bin",
            "C:\Program Files\EPSON\JavaPOS\SetupPOS"
        )
        "hp-cash-drawer" = @(
            "C:\Program Files (x86)\HP\HP Cash Drawer Port JPOS\lib",
            "C:\Program Files (x86)\HP\HP Cash Drawer Port JPOS\lib\x64"
        )
    }

    $enabledInstallers = @()
    if ($null -ne $global:PosProfile -and $global:PosProfile.PSObject.Properties.Name -contains "peripherals") {
        foreach ($peripheral in $global:PosProfile.peripherals) {
            $enabled = Get-ProfileValue -Node $peripheral -PropertyName "enabled" -DefaultValue $false
            $installer = Get-ProfileValue -Node $peripheral -PropertyName "installer"
			
			Write-Host "Peripheral: $($peripheral.name)"
			Write-Host "  -> Enabled  : $enabled"
			Write-Host "  -> Installer: $installer"
			Write-Host "------------------------------------"

            if ($enabled -and -not [string]::IsNullOrWhiteSpace($installer)) {
                $enabledInstallers += $installer.ToLowerInvariant()
            }
        }
    }

    $libraryPaths = New-Object System.Collections.Generic.List[string]
    $basePaths | ForEach-Object { [void]$libraryPaths.Add($_) }

    foreach ($installer in $enabledInstallers) {
        if ($peripheralPathMap.ContainsKey($installer)) {
            foreach ($path in $peripheralPathMap[$installer]) {
                if (-not $libraryPaths.Contains($path)) {
                    [void]$libraryPaths.Add($path)
                }
            }
        }
    }

    return ($libraryPaths -join ";")
}

function Test-IsPeripheralEnabled {
    param(
        [string]$InstallerName
    )

    if ([string]::IsNullOrWhiteSpace($InstallerName)) {
        return $false
    }

    if ($null -eq $global:PosProfile -or -not ($global:PosProfile.PSObject.Properties.Name -contains "peripherals")) {
        return $false
    }

    $normalizedInstallerName = $InstallerName.Trim().ToLowerInvariant()
    foreach ($peripheral in @($global:PosProfile.peripherals)) {
        $peripheralInstaller = (Get-ProfileValue -Node $peripheral -PropertyName "installer" -DefaultValue "").ToLowerInvariant()
        $isEnabled = Get-ProfileValue -Node $peripheral -PropertyName "enabled" -DefaultValue $false

        if ($peripheralInstaller -eq $normalizedInstallerName -and $isEnabled) {
            return $true
        }
    }

    return $false
}

# Create the download folder if it doesn't exist
if (-Not (Test-Path $downloadFolder)) {
    New-Item -Path $downloadFolder -ItemType Directory | Out-Null
    Write-Host "Created folder: $downloadFolder"
}

# Function to download and install .NET SDK
function Install-DotnetSDK {
    param (
        [string]$version,
        [string]$installerUrl,
        [string]$installerFile,
        [string]$testPath
    )

    if (Test-Path $testPath) {
        Write-Host ".NET SDK $version is already installed at $testPath."
        return $true
    }

    if (Test-Path $installerFile) {
        Write-Host ".NET SDK installer already exists at $installerFile. Skipping download."
    } else {
        try {
            Write-Host "Downloading .NET SDK $version..."
            Invoke-WebRequest -Uri $installerUrl -OutFile $installerFile -UseBasicParsing
            Write-Host "Download completed."
        } catch {
            Write-Host "❌ Failed to download .NET SDK. Error: $($_.Exception.Message)"
            return $false
        }
    }

    try {
        Write-Host "Installing .NET SDK $version..."
        Start-Process -FilePath $installerFile -ArgumentList "/quiet", "/norestart" -Wait -NoNewWindow
        Write-Host "✅ .NET SDK installation completed."
    } catch {
        Write-Host "❌ Failed to install .NET SDK. Error: $($_.Exception.Message)"
        return $false
    }

    if (Test-Path $testPath) {
        Write-Host "✅ .NET SDK $version installed successfully."
        return $true
    } else {
        Write-Host "❌ Installation failed. Check the logs for more details."
        return $false
    }
}

function Invoke-DotnetSdkInstallStep {
    $dotnetInstallSuccess = Install-DotnetSDK -version $dotnetSDKVersion -installerUrl $dotnetSDKInstallerUrl -installerFile $dotnetSDKInstallerFile -testPath $dotnetSDKPath
    if ($dotnetInstallSuccess) {
        Write-Host ".NET SDK installation completed successfully!"
    }
    else {
        Write-Host ".NET SDK installation failed. Please check the log file for more details."
        exit 1
    }
}

# Function to install the JDK
function Install-JDK {
    param (
        [string]$installerFile,
        [string]$installDir,
        [string]$logFile
    )

    # Check if JDK is already installed at the target directory
    if (Test-Path $javaPath) {
        Write-Host "JDK is already installed at $installDir."
        return $true
    }

    # Download the JDK installer only when it does not already exist in install-pos
    if (Test-Path $installerFile) {
        Write-Host "JDK installer already exists at $installerFile. Skipping download."
    } else {
        try {
            Write-Host "Downloading JDK to $installDir..."
            Invoke-WebRequest -Uri $jdkInstallerUrl -OutFile $installerFile -UseBasicParsing
            Write-Host "JDK download completed."
        } catch {
            Write-Host "Failed to download JDK. Error: $($_.Exception.Message)"
            exit 1
        }
    }

    # Run the JDK installer silently
    try {
        Write-Host "Running JDK installer silently..."
        $arguments = "/s INSTALLDIR=`"$installDir`" /L*v `"$logFile`""
        Write-Host "Running command: $installerFile $arguments"
        $process = Start-Process -FilePath $installerFile -ArgumentList $arguments -NoNewWindow -Wait -PassThru

        if ($process.ExitCode -eq 0) {
            Write-Host "JDK installation completed successfully."
        } else {
            Write-Host "JDK installer exited with code: $($process.ExitCode). Check the logs for details."
            return $false
        }
    } catch {
        Write-Host "Failed to run JDK installer. Error: $($_.Exception.Message)"
        return $false
    }

    # Verify JDK installation
    if (Test-Path $javaPath) {
        Write-Host "JDK installed successfully at $installDir."
        return $true
    } else {
        Write-Host "Installation failed or incomplete. Check the logs for more details."
        Write-Host "Logs can be found at: $logFile"
        return $false
    }
}

function Invoke-JdkInstallStep {
    $jdkInstallSuccess = Install-JDK -installerFile $jdkInstallerFile -installDir $jdkInstallDir -logFile $jdkLogFile
    if ($jdkInstallSuccess) {
        Write-Host "JDK installation completed successfully!"
    }
    else {
        Write-Host "JDK installation failed. Please check the log file for more details."
        exit 1
    }
}

function Install-VCRedist86 {
    if (-Not (Test-Path -Path $vcRedist86InstallerPath)) {
        throw "VC++ Redistributable installer not found at '$vcRedist86InstallerPath'."
    }

    Write-Output "Installing Microsoft Visual C++ Redistributable (x86) from '$vcRedist86InstallerPath'..."
    $process = Start-Process -FilePath $vcRedist86InstallerPath -ArgumentList "/install /quiet /norestart" -Wait -PassThru -NoNewWindow

    if ($process.ExitCode -in @(0, 1638, 3010)) {
        Write-Output "Microsoft Visual C++ Redistributable (x86) installation completed with exit code $($process.ExitCode)."
        return
    }

    throw "VC++ Redistributable installation failed with exit code $($process.ExitCode)."
}

function Invoke-VCRedist86InstallStep {
    Install-VCRedist86
}

function Invoke-VCRedistInstallStep {
    Install-VCRedist86
    Install-VCRedist64
}

function Install-VCRedist64 {
    if (-Not (Test-Path -Path $vcRedist64InstallerPath)) {
        throw "VC++ Redistributable installer not found at '$vcRedist64InstallerPath'."
    }

    Write-Output "Installing Microsoft Visual C++ Redistributable (x64) from '$vcRedist64InstallerPath'..."
    $process = Start-Process -FilePath $vcRedist64InstallerPath -ArgumentList "/install /quiet /norestart" -Wait -PassThru -NoNewWindow

    if ($process.ExitCode -in @(0, 1638, 3010)) {
        Write-Output "Microsoft Visual C++ Redistributable (x64) installation completed with exit code $($process.ExitCode)."
        return
    }

    throw "VC++ Redistributable installation failed with exit code $($process.ExitCode)."
}

function Invoke-VCRedist64InstallStep {
    Install-VCRedist64
}

# Function to install Epson JavaPOS
function Install-EpsonJavaPOS {
    $isInstalled = $false
    if (Test-Path -Path $epsonRegistryKey) {
        $installedDisplayName = (Get-ItemProperty -Path $epsonRegistryKey -Name DisplayName -ErrorAction SilentlyContinue).DisplayName
        if ($installedDisplayName -eq $epsonDisplayNameValue) {
            Write-Output "Epson JavaPOS is already installed. Skipping installation."
            $isInstalled = $true
        }
    }

    if (-not $isInstalled) {
        if (-Not (Test-Path -Path $epsonInstallerPath)) {
            Write-Error "Epson installer not found at $epsonInstallerPath. Please check the path."
            exit 1
        }

        Write-Output "Installing Epson JavaPOS from $epsonInstallerPath..."
        try {
            Start-Process -FilePath $epsonInstallerPath -ArgumentList "/S" -Wait -NoNewWindow
            Write-Output "Epson JavaPOS installation completed successfully."
        } catch {
            Write-Error "An error occurred during Epson JavaPOS installation: $_"
            exit 1
        }
    }

    Copy-DLLFiles
}

# Function to install Datalogic JavaPOS
function Test-DatalogicJavaPOSInstalled {
    $isInstalled = $false

    if (Test-Path -Path $datalogicRegistryKey) {
        $installedDisplayName = (Get-ItemProperty -Path $datalogicRegistryKey -Name DisplayName -ErrorAction SilentlyContinue).DisplayName
        if ($installedDisplayName -eq $datalogicDisplayNameValue) {
            $isInstalled = $true
        }
    }

    if (-not $isInstalled) {
        $datalogicBasePath = Split-Path -Path $javaPOSNewPath -Parent
        if ((Test-Path -Path $datalogicBasePath) -or (Test-Path -Path $javaPOSNewPath)) {
            $isInstalled = $true
        }
    }

    return $isInstalled
}

function Install-DatalogicJavaPOS {

    if (Test-DatalogicJavaPOSInstalled) {
        Write-Output "Datalogic JavaPOS is already installed. Skipping installation."
        return
    }

    if (-not (Test-Path -Path $datalogicInstallerPath)) {
        throw "Datalogic installer not found at '$datalogicInstallerPath'. Please check the path."
    }

    if (-not (Test-Path -Path $javaPath)) {
        throw "Java is not installed at the specified path: '$javaPath'. Please check and try again."
    }

    Write-Output "Installing Datalogic JavaPOS from $datalogicInstallerPath..."

    $baseArgs = @("-jar", $datalogicInstallerPath)

    # 1) tenta com -silent
    $argsSilent = $baseArgs + @("-silent")
    Write-Output ("Command: `"$javaPath`" " + ($argsSilent -join ' '))

    $proc = Start-Process -FilePath $javaPath -ArgumentList $argsSilent -Wait -PassThru -NoNewWindow
    Write-Output "Datalogic installer exit code (silent): $($proc.ExitCode)"

    if (($proc.ExitCode -eq 0) -and (Test-DatalogicJavaPOSInstalled)) {
        Write-Output "Datalogic JavaPOS installation completed successfully (silent)."
        Add-JavaPOSPath
        return
    }

    Write-Warning "Silent install failed (exit code $($proc.ExitCode)). Retrying without -silent..."

    # 2) tenta sem -silent (modo interactivo / default)
    Write-Output ("Command: `"$javaPath`" " + ($baseArgs -join ' '))
    $proc2 = Start-Process -FilePath $javaPath -ArgumentList $baseArgs -Wait -PassThru -NoNewWindow
    Write-Output "Datalogic installer exit code (normal): $($proc2.ExitCode)"

    if ($proc2.ExitCode -ne 0) {
        throw "Datalogic JavaPOS installation failed. ExitCode silent=$($proc.ExitCode), normal=$($proc2.ExitCode). Check installer output."
    }

    if (-not (Test-DatalogicJavaPOSInstalled)) {
        throw "Datalogic JavaPOS installation did not complete. The installer finished without installing the package (it may have been cancelled)."
    }

    Write-Output "Datalogic JavaPOS installation completed successfully."
    Add-JavaPOSPath
}

# Function to install Citizen JavaPOS
function Install-CitizenJavaPOS {
    if (-Not (Test-Path -Path $citizenInstallerPath)) {
        Write-Error "Citizen installer not found at $citizenInstallerPath. Please check the path."
        exit 1
    }

    Write-Output "Installing Citizen JavaPOS from $citizenInstallerPath..."
    try {
        $process = Start-Process -FilePath $citizenInstallerPath -ArgumentList "/S /v/qn" -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "Citizen JavaPOS installer exited with code $($process.ExitCode)."
        }

        Write-Output "Citizen JavaPOS installation completed successfully."
    } catch {
        Write-Error "An error occurred during Citizen JavaPOS installation: $_"
        exit 1
    }
}

# Function to install HP Cash Drawer Driver
function Install-HPCashDrawerDriver {
    if (-Not (Test-Path -Path $hpCashDrawerInstallerPath)) {
        Write-Error "HP Cash Drawer installer not found at $hpCashDrawerInstallerPath. Please check the path."
        exit 1
    }

    Write-Output "Installing HP Cash Drawer driver from $hpCashDrawerInstallerPath..."
    try {
        $process = Start-Process -FilePath $hpCashDrawerInstallerPath -ArgumentList "/s" -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            Write-Error "HP Cash Drawer driver installer exited with code $($process.ExitCode)."
            exit 1
        }

        Write-Output "HP Cash Drawer driver installation completed successfully."
    } catch {
        Write-Error "An error occurred during HP Cash Drawer driver installation: $_"
        exit 1
    }
}

# Function to copy DLL files
function Copy-DLLFiles {
    Write-Output "Starting the DLL file copy process..."

    # Check if the source directory exists
    if (-Not (Test-Path -Path $sourceDirectory)) {
        Write-Error "Source directory does not exist: $sourceDirectory. Please check the path."
        exit 1
    }

    # Check if the destination directory exists
    if (-Not (Test-Path -Path $destinationDirectory)) {
        Write-Error "Destination directory does not exist: $destinationDirectory. Please check the path."
        exit 1
    }

    # Copy each file from the source to the destination
    foreach ($file in $dllFiles) {
        $sourcePath = Join-Path -Path $sourceDirectory -ChildPath $file
        $destinationPath = Join-Path -Path $destinationDirectory -ChildPath $file

        # Check if the source file exists before copying
        if (Test-Path -Path $sourcePath) {
            try {
                Write-Output "Copying $file from $sourceDirectory to $destinationDirectory..."
                Copy-Item -Path $sourcePath -Destination $destinationPath -Force
            } catch {
                Write-Error ("Failed to copy " + $file + ": " + $_)
            }
        } else {
            Write-Warning "File $file does not exist in the source directory. Skipping."
        }
    }

    Write-Output "DLL file copy process completed successfully."
}

# Function to add new path variable
function Add-JavaPOSPath {
    if (-not (Test-IsPeripheralEnabled -InstallerName "datalogic-scanner")) {
        Write-Output "Datalogic peripheral is not enabled in POS profile. Skipping JavaPOS PATH update."
        return
    }

    # Check if the new path is already in the system PATH
    if ($currentPath -notlike "*$javaPOSNewPath*") {
        # Append the new path to the system PATH
        $newPathValue = $currentPath + ";" + $javaPOSNewPath
        [System.Environment]::SetEnvironmentVariable('Path', $newPathValue, [System.EnvironmentVariableTarget]::Machine)
        Write-Output "Path successfully updated."
    } else {
        Write-Output "Path already contains the specified directory."
    }

    # Reload environment variables to reflect changes in the current session
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', [System.EnvironmentVariableTarget]::Machine)

    # Verify the Path is updated
    if ($env:Path -like "*$javaPOSNewPath*") {
        Write-Output "The path is now correctly added: $javaPOSNewPath"
    } else {
        Write-Output "The path was not added."
    }
}

function Create-TotalCheckoutDatabaseFolder {
    # Check if the folder already exists
    if (-Not (Test-Path -Path $folderPath)) {
        # Create the folder
        New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
        Write-Output "Folder created successfully: $folderPath"
    } else {
        Write-Output "Folder already exists: $folderPath"
    }
}

function Copy-NginxFolder {
    # Check if the source folder exists
    if (Test-Path -Path $sourceFolderForNginx) {
        # Copy the folder to the destination
        Copy-Item -Path $sourceFolderForNginx -Destination $destinationFolderForNginx -Recurse -Force -ErrorAction Stop
        Write-Output "Folder successfully copied to: $destinationFolderForNginx"
    } else {
        throw "Source folder not found: $sourceFolderForNginx. Aborting script execution."
    }
}

function Copy-NwjsFolder {
    # Check if the source folder exists
    if (Test-Path -Path $sourceFolderForNwjs) {
        # Copy the folder to the destination
        Copy-Item -Path $sourceFolderForNwjs -Destination $destinationFolderForNwjs -Recurse -Force -ErrorAction Stop
        Write-Output "Folder successfully copied to: $destinationFolderForNwjs"
    } else {
        throw "Source folder not found: $sourceFolderForNwjs. Aborting script execution."
    }
}

function Copy-NssmFolder {
    # Check if the source folder exists
    if (Test-Path -Path $sourceFolderForNssm) {
        # Copy the folder to the destination
        Copy-Item -Path $sourceFolderForNssm -Destination $destinationFolderForNssm -Recurse -Force -ErrorAction Stop
        Write-Output "Folder successfully copied to: $destinationFolderForNssm"
    } else {
        throw "Source folder not found: $sourceFolderForNssm. Aborting script execution."
    }
}

function Download-And-Setup-FFmpeg {
    # Ensure the install folder exists before using it for download/extraction
    if (-Not (Test-Path -Path $downloadFolder)) {
        New-Item -ItemType Directory -Path $downloadFolder -Force | Out-Null
    }

    # Download the FFmpeg zip file only if it doesn't already exist
    if (Test-Path -Path $downloadPathFFmpeg) {
        Write-Output "FFmpeg package already exists: $downloadPathFFmpeg"
    } else {
        Write-Output "Downloading FFmpeg from $ffmpegUrl..."
        Invoke-WebRequest -Uri $ffmpegUrl -OutFile $downloadPathFFmpeg
        Write-Output "Download complete: $downloadPathFFmpeg"
    }

    # Extract the zip file to a temporary location inside install-pos
    $tempExtractPath = Join-Path -Path $downloadFolder -ChildPath "ffmpeg-temp"
    if (Test-Path -Path $tempExtractPath) {
        Remove-Item -Path $tempExtractPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempExtractPath -Force | Out-Null
    Write-Output "Extracting FFmpeg to temporary path..."
    Expand-Archive -Path $downloadPathFFmpeg -DestinationPath $tempExtractPath -Force

    # Move the contents of the nested folder directly to C:\ffmpeg
    $nestedFolder = Get-ChildItem -Path $tempExtractPath | Where-Object { $_.PSIsContainer } | Select-Object -First 1
    if ($nestedFolder) {
        Write-Output "Moving contents of nested folder: $($nestedFolder.FullName)"
        if (Test-Path -Path $extractPathFFmpeg) {
            Remove-Item -Path $extractPathFFmpeg -Recurse -Force
        }
        New-Item -ItemType Directory -Path $extractPathFFmpeg -Force | Out-Null
        Move-Item -Path (Join-Path -Path $nestedFolder.FullName -ChildPath "*") -Destination $extractPathFFmpeg -Force
    } else {
        Write-Output "No nested folder found. Exiting."
        return
    }

    # Determine the bin folder path
    $ffmpegBinPath = Join-Path -Path $extractPathFFmpeg -ChildPath "bin"

    # Add the FFmpeg bin folder to the system Path variable
    $currentPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
    if ($currentPath -notlike "*$ffmpegBinPath*") {
        $newPath = "$currentPath;$ffmpegBinPath"
        [System.Environment]::SetEnvironmentVariable("Path", $newPath, [System.EnvironmentVariableTarget]::Machine)
        Write-Output "Path updated successfully with: $ffmpegBinPath"
    } else {
        Write-Output "FFmpeg bin folder is already in the system Path."
    }

    # Update current session's Path variable
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)

    # Clean up temporary extraction files
    Remove-Item -Path $tempExtractPath -Recurse -Force
    Write-Output "Temporary extraction files cleaned up."

    Write-Output "FFmpeg setup complete! You can now use FFmpeg from any command line."
}

# Move services to Release folder
function Copy-Services-Folders {
# Check if the source directory exists
if (-Not (Test-Path -Path $sourceServicesPath)) {
    Write-Error "Source path '$sourceServicesPath' does not exist. Aborting operation."
    exit 1
}

# Check if the destination directory exists; if not, create it
if (-Not (Test-Path -Path $destinationReleasesPath)) {
    New-Item -ItemType Directory -Path $destinationReleasesPath -Force | Out-Null
    Write-Output "Created destination directory: $destinationPath"
}

# Move all folders from the source to the destination
Get-ChildItem -Path $sourceServicesPath -Directory | ForEach-Object {
    $folderName = $_.Name
    $sourceFolder = $_.FullName
    $destinationFolder = Join-Path -Path $destinationReleasesPath -ChildPath $folderName

    # Check if the folder already exists in the destination
    if (-Not (Test-Path -Path $destinationFolder)) {
        Move-Item -Path $sourceFolder -Destination $destinationReleasesPath -Force
        Write-Output "Moved folder: $folderName to $destinationReleasesPath"
    } else {
        Write-Output "Folder '$folderName' already exists in the destination. Skipping."
    }
}

Write-Output "All folders moved successfully!"

}

# Criar os serviços
function Create-Services{
    # Check if the service exists POS.Api
    if (Get-Service -Name $ServicePOSApiName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServicePOSApiName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServicePOSApiName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.POS.Api\TotalCheckoutPOS.Services.POS.Api.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.POS.Api" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServicePOSApiName' created successfully."
    }
    
    # Check if the service exists ServiceLOGCName
    if (Get-Service -Name $ServiceLOGCName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServiceLOGCName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServiceLOGCName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.LOGC\TotalCheckoutPOS.Services.LOGC.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.LOGC" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServiceLOGCName' created successfully."
    }
    
    # Check if the service exists ServicePOSCoreAPIName
    if (Get-Service -Name $ServicePOSCoreAPIName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServicePOSCoreAPIName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServicePOSCoreAPIName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.POS.Core.Api\TotalCheckoutPOS.Services.POS.Core.API.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.POS.Core.Api" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServicePOSCoreAPIName' created successfully."
    }   
    
    # Check if the service exists UIUtilsSignalRCommunication
    if (Get-Service -Name $UIUtilsSignalRCommunicationName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$UIUtilsSignalRCommunicationName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $UIUtilsSignalRCommunicationName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.UI.Utils.SignalRCommunication\TotalCheckoutPOS.UI.Utils.SignalRCommunication.exe --contentRoot C:\Releases\TotalCheckoutPOS.UI.Utils.SignalRCommunication" `
                    -StartupType "Automatic"
        Write-Output "Service '$UIUtilsSignalRCommunicationName' created successfully."
    }   
    
    # Check if the service exists ServiceUPDCName
    if (Get-Service -Name $ServiceUPDCName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServiceUPDCName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServiceUPDCName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.UPDC\TotalCheckoutPOS.Services.UPDC.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.UPDC" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServiceUPDCName' created successfully."
    }     
       
    # Check if the service exists ServiceServerOlcasAPIName
    if (Get-Service -Name $ServiceServerOlcasAPIName -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServiceServerOlcasAPIName' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServiceServerOlcasAPIName `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.Server.OlcasAPI\TotalCheckoutPOS.Services.Server.OlcasAPI.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.Server.OlcasAPI" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServiceServerOlcasAPIName' created successfully."
    } 
	
	# Check if the service exists ServiceGatewayExternalPayments
    if (Get-Service -Name $ServiceGatewayExternalPayments -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServiceGatewayExternalPayments' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServiceGatewayExternalPayments `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Gateway.ExternalPayments.WebApi\TotalCheckoutPOS.Gateway.ExternalPayments.WebApi.exe --contentRoot C:\Releases\TotalCheckoutPOS.Gateway.ExternalPayments.WebApi" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServiceGatewayExternalPayments' created successfully."
    }
    # Check if the service exists ServiceSmartRetention
    if (Get-Service -Name $ServiceSmartRetention -ErrorAction SilentlyContinue) {
        Write-Output "Service '$ServiceSmartRetention' already exists. Skipping creation."
    } else {
        # Create the service if it does not exist
        New-Service -Name $ServiceSmartRetention `
                    -BinaryPathName "C:\Releases\TotalCheckoutPOS.Services.SmartRetention.Api\TotalCheckoutPOS.Services.SmartRetention.Api.exe --contentRoot C:\Releases\TotalCheckoutPOS.Services.SmartRetention.Api" `
                    -StartupType "Automatic"
        Write-Output "Service '$ServiceSmartRetention' created successfully."
    }

    
    # Check if the service exists NginxName
    # Variables
$nginxPath = "C:\nginx\nginx.exe"
$serviceName = "nginx"
$serviceDisplayName = "Nginx Web Server"
$serviceDescription = "Nginx Web Server running as a Windows Service via NSSM"

# Install Nginx as a service
& $NSSM_PATH install $serviceName $nginxPath

# Configure optional parameters (optional)
& $NSSM_PATH set $serviceName DisplayName $serviceDisplayName
& $NSSM_PATH set $serviceName Description $serviceDescription
& $NSSM_PATH set $serviceName Start SERVICE_AUTO_START

# Start the service
Start-Service -Name $serviceName

Write-Host "Nginx service '$serviceName' installed and started successfully." 
}

# Install devices_service
function Install-Devices-Service {
# Define variables
$SERVICE_NAME = "TotalCheckoutPOS.Devices"
$WORKING_DIR = "C:\Releases\TotalCheckoutPOS.Devices"
$JAR_FILE = Join-Path $WORKING_DIR "Devices-all.jar"
$CONFIG_FILE = Join-Path $WORKING_DIR "application.properties"
$LOG4J_CONFIG = Join-Path $WORKING_DIR "log4j2.xml"
$LIB_PATH = Get-DevicesLibraryPath
Write-Output "Using java.library.path: $LIB_PATH"

# Check if the service is already installed
Write-Output "Checking if service '$SERVICE_NAME' is already installed..."
$service = Get-Service -Name $SERVICE_NAME -ErrorAction SilentlyContinue

if ($service) {
    Write-Output "Service '$SERVICE_NAME' is already installed. Stopping it..."
    Stop-Service -Name $SERVICE_NAME -Force -ErrorAction SilentlyContinue
    Write-Output "Service '$SERVICE_NAME' stopped. Removing it..."
    sc.exe delete $SERVICE_NAME | Out-Null
    Write-Output "Service '$SERVICE_NAME' removed."

    # Add a delay to ensure the service is completely deleted
    Start-Sleep -Seconds 5
}

# Install the service
Write-Output "Installing service '$SERVICE_NAME'..."
& $NSSM_PATH install $SERVICE_NAME "$javaPath" "-Dmicronaut.config.files=$CONFIG_FILE" "-Djava.library.path=$LIB_PATH" "-Dlog4j.configurationFile=$LOG4J_CONFIG" -jar $JAR_FILE
if (!$?) {
    Write-Error "Error creating service! Exiting..."
    exit 1
}

& $NSSM_PATH set $SERVICE_NAME AppDirectory $WORKING_DIR
if (!$?) {
    Write-Error "Error setting AppDirectory! Exiting..."
    exit 1
}

& $NSSM_PATH set $SERVICE_NAME DisplayName "TotalCheckoutPOS.Devices.Api"
if (!$?) {
    Write-Error "Error setting DisplayName! Exiting..."
    exit 1
}

& $NSSM_PATH set $SERVICE_NAME Start SERVICE_AUTO_START
if (!$?) {
    Write-Error "Error setting Start! Exiting..."
    exit 1
}

Write-Output "Service '$SERVICE_NAME' installed successfully."

# Start the service
# Validate the existence of the JAR file
if (-Not (Test-Path -Path $JAR_FILE)) {
    Write-Error "JAR file '$JAR_FILE' does not exist. Service creation aborted."
    exit 1
}
else {
    Write-Output "Starting service '$SERVICE_NAME'..."
    Start-Service -Name $SERVICE_NAME
    if (!$?) {
        Write-Error "Error starting service! Exiting..."
        exit 1
    }
    
    Write-Output "Service '$SERVICE_NAME' started successfully."
}

}

function Start-TotalCheckoutPOSServices {
    # Define a string that uniquely identifies services related to TotalCheckoutPOS
    $serviceNamePattern = "TotalCheckoutPOS"

    # Get a list of all services with names containing the pattern
    $services = Get-Service | Where-Object { $_.Name -like "*$serviceNamePattern*" }

    foreach ($service in $services) {
        Write-Host "Processing service: $($service.Name)"
        
        try {
            # If the service is running, stop it
            if ($service.Status -eq 'Running') {
                Write-Host "Stopping service: $($service.Name)"
                Stop-Service -Name $service.Name -Force -ErrorAction Stop
                # Wait a few seconds before restarting
                Start-Sleep -Seconds 3
            }
            
            # Start the service again
            Write-Host "Starting service: $($service.Name)"
            Start-Service -Name $service.Name -ErrorAction Stop

        } catch {
            Write-Host "Error managing service $($service.Name): $_"
        }
    }
}

function Install-SQLServerAndCreateUser {
    $installerPath = "C:\TotalCheckout\PackagePOS\SQL2019-SSEI-Expr.exe"
    $saPassword = "olc"  # Password for the 'sa' user
    $newUsername = "olc" # New user name to be created
    $newPassword = "olc" # Password for the new user

    # Step 1: Install SQL Server Express Edition with default instance
    Write-Host "Starting SQL Server installation..."

    $installArgs = "/QS /IACCEPTSQLSERVERLICENSETERMS /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=MSSQLSERVER /SQLSYSADMINACCOUNTS=SA /SAPWD=$saPassword"
    
    # Run the SQL Server installer
    Start-Process -FilePath $installerPath -ArgumentList $installArgs -Wait

    # Step 2: Verify if SQL Server is installed
    Write-Host "SQL Server installation completed. Verifying installation..."
    
    $sqlServerPath = "C:\Program Files\Microsoft SQL Server\2019\SQL10_50_1\Tools\Binn\sqlcmd.exe"

    if (-Not (Test-Path $sqlServerPath)) {
        Write-Host "SQL Server was not installed correctly. Exiting..."
        return
    }

    # Step 3: Create the new user 'olc' in the SQL Server database
    Write-Host "Creating new user '$newUsername'..."

    $sqlQuery = @"
    CREATE LOGIN $newUsername WITH PASSWORD = '$newPassword';
    CREATE USER $newUsername FOR LOGIN $newUsername;
    ALTER ROLE db_datareader ADD MEMBER $newUsername;
    ALTER ROLE db_datawriter ADD MEMBER $newUsername;
"@

    # Execute the SQL query to create the new user
    $sqlcmdArgs = "-S localhost -U sa -P $saPassword -Q $sqlQuery"
    Start-Process -FilePath $sqlServerPath -ArgumentList $sqlcmdArgs -Wait

    Write-Host "User '$newUsername' created successfully."
}

function Add-OlcasEnviroment-Variables{
    param (
        [hashtable]$CustomEnvVars
    )

    # Define the environment variables
    $envVars = @{
        "STORE_TYPE" = "04"
        "OLC_STORE_ID" = "0659"
        "SVRNAME" = "\\172.16.244.40"
        "SERVERNAME1" = "\\172.16.244.40"
        "SERVERNAME2" = "\\172.16.244.40"
        "POS_ID" = "30"
    }

    if ($null -ne $CustomEnvVars) {
        foreach ($customKey in $CustomEnvVars.Keys) {
            $envVars[$customKey] = $CustomEnvVars[$customKey]
        }
    }
    
    # Add the variables to the system environment
    foreach ($key in $envVars.Keys) {
        # Get the current value of the environment variable
        $currentValue = [System.Environment]::GetEnvironmentVariable($key, [System.EnvironmentVariableTarget]::Machine)
        
        # Check if the variable exists and its value matches
        if ($currentValue -eq $envVars[$key]) {
            Write-Output "Environment variable '$key' already exists with the correct value '${envVars[$key]}'. Skipping update."
        } else {
            # Update the system environment variable if it doesn't exist or the value is different
            [System.Environment]::SetEnvironmentVariable($key, $envVars[$key], [System.EnvironmentVariableTarget]::Machine)
            Write-Output "Environment variable '$key' set to '${envVars[$key]}' at the system level."
        }
    }
    
    # Update the current session's environment variables
    foreach ($key in $envVars.Keys) {
        # Get the current session value of the environment variable
        $currentSessionValue = [System.Environment]::GetEnvironmentVariable($key, [System.EnvironmentVariableTarget]::Process)
    
        # Check if the variable exists in the session and its value matches
        if ($currentSessionValue -eq $envVars[$key]) {
            Write-Output "Environment variable '$key' already exists in the current session with the correct value '${envVars[$key]}'. Skipping update."
        } else {
            # Update the session variable if it doesn't exist or the value is different
            [System.Environment]::SetEnvironmentVariable($key, $envVars[$key], [System.EnvironmentVariableTarget]::Process)
            Write-Output "Environment variable '$key' updated in the current session to '${envVars[$key]}'."
        }
    }
    
    Write-Output "All environment variables have been added or validated successfully!"
}

function Copy-OlcasFolderContents {
    param (
        [string]$SourcePath = "C:\TotalCheckout\PackagePOS\Olcas",
        [string]$DestinationPath = "C:\"
    )

    # Ensure source path exists
    if (-Not (Test-Path -Path $SourcePath)) {
        Write-Error "Source path '$SourcePath' does not exist."
        return
    }

    # Ensure destination path exists, create if it doesn't
    if (-Not (Test-Path -Path $DestinationPath)) {
        Write-Host "Destination path '$DestinationPath' does not exist. Creating it..."
        New-Item -ItemType Directory -Path $DestinationPath -Force
    }

    try {
        # Copy contents of source folder to destination
        Get-ChildItem -Path $SourcePath -Recurse | ForEach-Object {
            $destination = Join-Path -Path $DestinationPath -ChildPath $_.FullName.Substring($SourcePath.Length).TrimStart("\")
            if ($_.PSIsContainer) {
                # Create destination folder if it doesn't exist
                if (-Not (Test-Path -Path $destination)) {
                    New-Item -ItemType Directory -Path $destination -Force
                }
            } else {
                # Copy file to destination
                Copy-Item -Path $_.FullName -Destination $destination -Force
            }
        }
        Write-Host "Contents copied successfully from '$SourcePath' to '$DestinationPath'."
    } catch {
        Write-Error "An error occurred while copying: $_"
    }
}

function Execute-InstallOlcasCmd {
    # Navigate to the directory
    Set-Location -Path "C:\Olcas\Install"

    # Run the command
    Start-Process "install.cmd" -NoNewWindow -Wait
}

function Invoke-OlcasInstallStep {
    Add-OlcasEnviroment-Variables
    Copy-OlcasFolderContents -SourcePath "C:\TotalCheckout\PackagePOS\Olcas" -DestinationPath "C:\"
    Execute-InstallOlcasCmd
}

function Invoke-PeripheralInstallPlan {
    param(
        $Profile
    )

    if ($null -eq $Profile) {
        Write-Output "No profile loaded. Install plan will not run."
        return
    }
	
	Write-Host "Invoke-PeripheralInstallPlan CALLED"
	Write-Host "Peripherals count: $(@($Profile.peripherals).Count)"
	Write-Host ("Peripherals: " + ($Profile.peripherals | ConvertTo-Json -Depth 5))

    $peripherals = @($Profile.peripherals)
    foreach ($peripheral in $peripherals) {
        if (-Not (Get-ProfileValue -Node $peripheral -PropertyName "enabled" -DefaultValue $true)) {
			Write-Output "Skipping peripheral '$name' because it is disabled in profile."
            continue
        }

        $name = Get-ProfileValue -Node $peripheral -PropertyName "name" -DefaultValue "unknown"
        $installer = (Get-ProfileValue -Node $peripheral -PropertyName "installer" -DefaultValue "").ToLowerInvariant()

        Write-Output "Executing peripheral step for '$name' using installer '$installer'."

        switch ($installer) {
            "epson-printer" { Install-EpsonJavaPOS; break }
            "datalogic-scanner" { Install-DatalogicJavaPOS; break }
            "citizen-printer" { Install-CitizenJavaPOS; break }
            "hp-cash-drawer" { Install-HPCashDrawerDriver; break }
            default { Write-Warning "No installer mapped for peripheral '$name' (installer='$installer'). Skipping." }
        }
    }
}

function Invoke-PeripheralInstallStep {
    param(
        $Profile
    )

    if ($null -eq $Profile) {
        Write-Warning "Nenhum perfil POS foi carregado. Passo de periféricos ignorado."
        return
    }

    Invoke-PeripheralInstallPlan -Profile $Profile
}

# Define the environment variable
$Environment = "Dev" # Change this to "Release" to prevent the function from running

function Copy-FolderContents {
    param (
        [Parameter(Mandatory = $true)]
        [string]$SourceFolder,

        [Parameter(Mandatory = $true)]
        [string]$DestinationFolder
    )

    try {
        # Check if the source folder exists
        if (-Not (Test-Path -Path $SourceFolder)) {
            throw "Source folder '$SourceFolder' does not exist."
        }

        # Create the destination folder if it does not exist
        if (-Not (Test-Path -Path $DestinationFolder)) {
            Write-Output "Destination folder '$DestinationFolder' does not exist. Creating it now..."
            New-Item -ItemType Directory -Path $DestinationFolder -Force
        }

        # Perform the copy operation
        Write-Output "Copying files from '$SourceFolder' to '$DestinationFolder'..."
        Copy-Item -Path "$SourceFolder\*" -Destination $DestinationFolder -Recurse -Force
        Write-Output "Copy operation completed successfully."
    }
    catch {
        Write-Error "An error occurred: $_"
    }
}

function Invoke-ServicesWindowsCopyStep {
    if ($Environment -eq "Dev") {
        Write-Output "Environment is set to Dev. Running the copy function..."
        Copy-FolderContents -SourceFolder "C:\TotalCheckout\PackagePOS\ServicesWindows" -DestinationFolder "C:\ServicesWindows"
    }
    elseif ($Environment -eq "Release") {
        Write-Output "Environment is set to Release. Function will not run."
    }
    else {
        Write-Output "Unknown environment value: '$Environment'. Function will not run."
    }
}

# Ensure the ServicesWindows folder exists
$servicesWindowsFolder = "C:\ServicesWindows"
if (-Not (Test-Path -Path $servicesWindowsFolder)) {
    Write-Output "Folder 'C:\ServicesWindows' does not exist. Creating it now..."
    New-Item -ItemType Directory -Path $servicesWindowsFolder -Force
}

# Function to check and install .NET Framework 3.5
Function Install-DotNetFramework {
    param (
        [string]$SetupPath
    )

    # Check if the script is running as Administrator
    If (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        Write-Output "This script must be run as Administrator!"
        Return
    }

    # Check if .NET Framework 3.5 is already installed (via Registry)
    $regPath = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5"
    If (Test-Path $regPath) {
        $version = (Get-ItemProperty -Path $regPath -Name "Version").Version
        Write-Output ".NET Framework 3.5 is already installed. Version: $version"
        Return
    }

    # Try to install using DISM if the setup file isn't available
    If (-Not (Test-Path -Path $SetupPath)) {
        Write-Output "The setup file does not exist at $SetupPath. Attempting to install via DISM..."
        Try {
            DISM /Online /Enable-Feature /FeatureName:NetFx3 /All /Quiet /NoRestart
            Write-Output ".NET Framework 3.5 installation via DISM completed successfully."
        } Catch {
            Write-Output "An error occurred during the installation via DISM: $_"
        }
        Return
    }

    # Install .NET Framework 3.5 using the setup file
    Write-Output "Starting the installation of .NET Framework 3.5 using the setup file..."
    Try {
        Start-Process -FilePath $SetupPath -ArgumentList "/quiet /norestart" -Wait -NoNewWindow
        Write-Output ".NET Framework 3.5 installation using setup file completed successfully."
    } Catch {
        Write-Output "An error occurred during the installation: $_"
    }
}

function Invoke-DotNetFrameworkInstallStep {
    $setupPath = "C:\TotalCheckout\PackagePOS\dotNetFx35setup.exe"
    Install-DotNetFramework -SetupPath $setupPath
}

function Install-IaaS-Service {
    foreach ($sourceFile in $IngelinkSourceFiles) {
        if (-Not (Test-Path -Path $sourceFile)) {
            Write-Error "Required Ingelink file not found at '$sourceFile'."
            exit 1
        }

        Write-Output "Copying '$sourceFile' to '$POSMainDestination'..."
        Copy-Item -Path $sourceFile -Destination $POSMainDestination -Force
    }

    # Validations
    if (-Not (Test-Path -Path $NSSM_PATH)) {
        Write-Error "NSSM not found at '$NSSM_PATH'. Did you run Copy-NssmFolder?"
        exit 1
    }
    if (-Not (Test-Path -Path $IaaSExePath)) {
        Write-Error "IaaS.exe not found at '$IaaSExePath'."
        exit 1
    }
    if (-Not (Test-Path -Path $IaaSConfigPath)) {
        Write-Error "IaaS config not found at '$IaaSConfigPath'."
        exit 1
    }

    Write-Output "Checking if service '$IaaSServiceName' exists..."
    $existing = Get-Service -Name $IaaSServiceName -ErrorAction SilentlyContinue

    if ($existing) {
        Write-Output "Service '$IaaSServiceName' already exists. Stopping and removing..."
        try { Stop-Service -Name $IaaSServiceName -Force -ErrorAction SilentlyContinue } catch {}
        & $NSSM_PATH remove $IaaSServiceName confirm | Out-Null
        Start-Sleep -Seconds 2
    }

    Write-Output "Installing service '$IaaSServiceName' via NSSM..."
    & $NSSM_PATH install $IaaSServiceName $IaaSExePath $IaaSPort $IaaSConfigPath
    if (!$?) { Write-Error "Failed to install service '$IaaSServiceName'."; exit 1 }

    # Set working directory
    & $NSSM_PATH set $IaaSServiceName AppDirectory $IaaSWorkingDir | Out-Null

    # Optional: display details
    & $NSSM_PATH set $IaaSServiceName DisplayName $IaaSServiceName | Out-Null
    & $NSSM_PATH set $IaaSServiceName Description "TotalCheckoutPOS IaaS running as a Windows Service via NSSM" | Out-Null

    # Startup
    & $NSSM_PATH set $IaaSServiceName Start SERVICE_AUTO_START | Out-Null

    # restart on crash
    & $NSSM_PATH set $IaaSServiceName AppExit Default Restart | Out-Null
    & $NSSM_PATH set $IaaSServiceName AppRestartDelay 5000 | Out-Null

    Write-Output "Starting service '$IaaSServiceName'..."
    Start-Service -Name $IaaSServiceName

    Write-Output "Service '$IaaSServiceName' installed and started."
}

function Resolve-SelectedInstallSteps {
    param (
        [string[]]$RequestedSteps,
        [string[]]$AvailableSteps
    )

    $normalizedSteps = @()

    foreach ($requestedStep in $RequestedSteps) {
        if ([string]::IsNullOrWhiteSpace($requestedStep)) {
            continue
        }

        $splitSteps = $requestedStep -split ","
        foreach ($stepValue in $splitSteps) {
            $normalizedValue = $stepValue.Trim().ToLowerInvariant()
            if (-not [string]::IsNullOrWhiteSpace($normalizedValue)) {
                $normalizedSteps += $normalizedValue
            }
        }
    }

    if ($normalizedSteps.Count -eq 0) {
        throw "No installation steps were provided. Use -Steps full or define the step numbers to execute."
    }

    if ($normalizedSteps -contains "full") {
        return $AvailableSteps
    }

    $stepAliases = @{}

    $invalidSteps = New-Object System.Collections.Generic.List[string]
    $selectedSteps = New-Object System.Collections.Generic.HashSet[string]

    foreach ($normalizedStep in $normalizedSteps) {
        if ($stepAliases.ContainsKey($normalizedStep)) {
            foreach ($aliasStep in $stepAliases[$normalizedStep]) {
                [void]$selectedSteps.Add($aliasStep)
            }
            continue
        }

        if ($AvailableSteps -contains $normalizedStep) {
            [void]$selectedSteps.Add($normalizedStep)
        }
        else {
            $invalidSteps.Add($normalizedStep)
        }
    }

    if ($invalidSteps.Count -gt 0) {
        $availableStepText = [string]::Join(", ", $AvailableSteps)
        $invalidStepText = [string]::Join(", ", $invalidSteps)
        throw "Invalid step(s): $invalidStepText. Available values: full, $availableStepText"
    }

    $orderedSelectedSteps = @()
    foreach ($availableStep in $AvailableSteps) {
        if ($selectedSteps.Contains($availableStep)) {
            $orderedSelectedSteps += $availableStep
        }
    }

    return $orderedSelectedSteps
}

function Invoke-InstallStep {
    param (
        [Parameter(Mandatory = $true)]
        [string]$StepId,

        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [ScriptBlock]$Action
    )

    Write-Output "Running step $StepId - $Description"

    try {
        & $Action
        Write-Output "Step $StepId completed successfully."
    }
    catch {
        throw "Step $StepId failed ($Description): $($_.Exception.Message)"
    }
}

# Main Script Execution
Write-Output "Starting installation process..."

try {
    $PosProfile = Load-PosProfile -Path $ProfilePath
    Apply-PosProfile -Profile $PosProfile
} catch {
    Write-Error $_
    exit 1
}

if ($UseInstallPlan -and $null -ne $PosProfile) {
    $profileEnvVarsObject = Get-ProfileValue -Node $PosProfile.environment -PropertyName "variables" -DefaultValue $null
    if ($null -ne $profileEnvVarsObject) {
        $profileEnvVars = ConvertTo-Hashtable -InputObject $profileEnvVarsObject
        Add-OlcasEnviroment-Variables -CustomEnvVars $profileEnvVars
    }
}

$stepDefinitions = [ordered]@{
    "1" = @{
        Description = "Install .NET SDK 8.0.401"
        Action = { Invoke-DotnetSdkInstallStep }
    }
	"2" = @{
        Description = "Instalar .NET Framework 3.5"
        Action = { Invoke-DotNetFrameworkInstallStep }
    }
    "3" = @{
        Description = "Instalar o jdk-17.0.11_windows-x64_bin"
        Action = { Invoke-JdkInstallStep }
    }
    "4" = @{
        Description = "Instalar Microsoft Visual C++ Redistributable"
        Action = { Invoke-VCRedistInstallStep }
    }
    "5" = @{
        Description = "Instalar periféricos do perfil POS"
        Action = { Invoke-PeripheralInstallStep -Profile $PosProfile }
    }
    "6" = @{ 
		Description = "Criar pasta C:\TotalCheckout\Database"; 
		Action = { Create-TotalCheckoutDatabaseFolder } }
    "7" = @{ 
		Description = "Copiar pasta nginx"; 
		Action = { Copy-NginxFolder } }
    "8" = @{ 
		Description = "Copiar pasta nwjs"; 
		Action = { Copy-NwjsFolder } }
    "9" = @{ 
		Description = "Copiar pasta nssm"; 
		Action = { Copy-NssmFolder } }
    "10" = @{ 
		Description = "Download e instalação de FFmpeg"; 
		Action = { Download-And-Setup-FFmpeg } }
    "11" = @{ 
		Description = "Instalar IaaS.exe como Windows Service"; 
		Action = { Install-IaaS-Service } }
    "12" = @{
        Description = "Copiar ServicesWindows para C:\"
        Action = { Invoke-ServicesWindowsCopyStep }
    }
	"13" = @{ 
		Description = "Copiar soluções para releases"; 
		Action = { Copy-Services-Folders } }
    "14" = @{ 
		Description = "Criar serviços Windows para APIs"; 
		Action = { Create-Services } }
    "15" = @{ 
		Description = "Instalar Devices API como Windows Service"; 
		Action = { Install-Devices-Service } }
    "16" = @{ 
		Description = "Iniciar serviços Windows do TotalCheckoutPOS"; 
		Action = { Start-TotalCheckoutPOSServices } }
	#"17" = @{ 
	#	Description = "Instalar SQL Server Express para Olcas"; 
	#	Action = { Install-SQLServerAndCreateUser } }
    #"18" = @{
    #    Description = "Configurar e instalar Olcas"
    #    Action = { Invoke-OlcasInstallStep }
    #}
}

$availableSteps = @($stepDefinitions.Keys)
$selectedSteps = Resolve-SelectedInstallSteps -RequestedSteps $Steps -AvailableSteps $availableSteps

Write-Output "Selected installation steps: $([string]::Join(', ', $selectedSteps))"

foreach ($stepId in $selectedSteps) {
    $stepDefinition = $stepDefinitions[$stepId]
    Invoke-InstallStep -StepId $stepId -Description $stepDefinition.Description -Action $stepDefinition.Action
}

Write-Output "All installations are complete."

# Exemplo de execução por perfil:
# powershell -ExecutionPolicy Bypass -File .\install-pos-windowns.ps1 -ProfilePath .\profiles\pos-default.json
# powershell -ExecutionPolicy Bypass -File .\install-pos-windowns.ps1 -ProfilePath .\profiles\pos-default.json -UseInstallPlan
# powershell -ExecutionPolicy Bypass -File .\install-pos-windowns.ps1 -ProfilePath .\profiles\pos-default.json -Steps 1,2,5
# powershell -ExecutionPolicy Bypass -File .\install-pos-windowns.ps1 -ProfilePath .\profiles\pos-default.json -Steps full
