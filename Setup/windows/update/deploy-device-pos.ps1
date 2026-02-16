# Paths and variables
$ServiceName = "TotalCheckoutPOS.Devices"
$WorkingDir = "C:\Releases\TotalCheckoutPOS.Devices"
$JarFileName = "Devices-all.jar"
$ConfigFileName = "application.properties"
$JarFile = Join-Path $WorkingDir $JarFileName
$ConfigFile = Join-Path $WorkingDir $ConfigFileName
$Log4jFile = Join-Path $WorkingDir "log4j2.xml"
$NssmPath = "C:\nssm\win64\nssm.exe"
$UtilsSource = "C:\TotalCheckoutPOS.UI.Utils\LinuxConfigs\Utils\"

# Java and library paths
$JavaPath = "C:\Program Files\Java\jdk-17.0.11\bin\java.exe"
$LibPath = "C:\Program Files (x86)\HP\HP Cash Drawer Port JPOS\lib;C:\Program Files (x86)\HP\HP Cash Drawer Port JPOS\lib\x64;C:\Program Files\Datalogic\JavaPOS;C:\Program Files\Datalogic\JavaPOS\SupportJars;C:\Program Files\EPSON\JavaPOS\lib;C:\Program Files\EPSON\JavaPOS\bin;C:\Program Files\EPSON\JavaPOS\SetupPOS"

Write-Host "Starting update process for service '$ServiceName'..."

# Stop existing service
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Existing service found. Stopping..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue

    # Esperar até o processo Java ser finalizado
    Get-Process java -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*$WorkingDir*" } | ForEach-Object {
        Write-Host "Waiting for process $($_.Id) to exit..."
        $_ | Wait-Process
    }
} else {
    Write-Host "Service '$ServiceName' not found. Installing service..."
    & $NssmPath install $ServiceName $JavaPath "-Dmicronaut.config.files=$ConfigFile" "-Djava.library.path=$LibPath" "-Dlog4j.configurationFile=$Log4jFile" -jar $JarFile
	if (!$?) {
		Write-Error "Error creating service! Exiting..."
		exit 1
	}
	
    & $NssmPath set $ServiceName AppDirectory $WorkingDir
	if (!$?) {
		Write-Error "Error setting AppDirectory! Exiting..."
		exit 1
	}
	
    & $NssmPath set $ServiceName DisplayName "TotalCheckoutPOS.Devices.Api"
	if (!$?) {
		Write-Error "Error setting DisplayName! Exiting..."
		exit 1
	}
	
    & $NssmPath set $ServiceName Start SERVICE_AUTO_START
	if (!$?) {
		Write-Error "Error setting Start! Exiting..."
		exit 1
	}
	
	Write-Output "Service '$ServiceName' installed successfully."
}

# Ensure working directory exists
if (-Not (Test-Path $WorkingDir)) {
    Write-Host "Working directory does not exist. Creating: $WorkingDir"
    New-Item -ItemType Directory -Force -Path $WorkingDir | Out-Null
} else {
    Write-Host "Working directory already exists: $WorkingDir"
}

# Determine agent path and locate new JAR from drop
$AgentPath = Get-Location
$DropJarPath = Join-Path -Path $AgentPath -ChildPath "TotalCheckoutPOS.Devices\drop\build\libs\$JarFileName"

# Validate and copy JAR from drop
if (-Not (Test-Path -Path $DropJarPath)) {
    Write-Error "JAR file not found at: $DropJarPath"
    exit 1
}

Write-Host "Copying new JAR from $DropJarPath to $JarFile"
Copy-Item -Path $DropJarPath -Destination $JarFile -Force

$DropResourcesPath = Join-Path -Path $AgentPath -ChildPath "TotalCheckoutPOS.Devices\drop\resources"

# Validate and copy resources from drop
if (-Not (Test-Path -Path $DropResourcesPath)) {
    Write-Error "Resources folder not found at: $DropResourcesPath"
    exit 1
}

Write-Host "Copying resources from $DropResourcesPath to $WorkingDir"
Copy-Item -Path "$DropResourcesPath\*" -Destination $WorkingDir -Recurse -Force

# Copy Utils files if available
if (Test-Path $UtilsSource) {
    Write-Host "Copying Utils files..."
    Copy-Item -Path "$UtilsSource\*" -Destination $WorkingDir -Recurse -Force
} else {
    Write-Warning "Utils folder not found at: $UtilsSource"
}

# Confirm final JAR exists
if (-Not (Test-Path $JarFile)) {
    Write-Error "Final JAR file not found at: $JarFile"
    exit 1
}

# Confirm final Config exists
if (-Not (Test-Path $ConfigFile)) {
    Write-Error "Final Config file not found at: $ConfigFile"
    exit 1
}

# Start the service
Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

Write-Host "Service '$ServiceName' updated and started successfully." -ForegroundColor Green
