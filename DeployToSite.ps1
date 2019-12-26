$Webroot = "C:\inetpub\wwwroot\tst93sc.dev.local"

Copy-Item "$PSScriptRoot\PackageAutoloader\App_Config\Include\PackageAutoloader.config" "$(New-Item "$Webroot\App_Config\Include\PackageAutoloaderTest" -ItemType directory -Force)/PackageAutoloader.config" -Force
Get-ChildItem "$PSScriptRoot\PackageAutoloader\bin\Debug" | Foreach-Object{
	Write-Host "moving $($_.FullName) to bin root" -ForegroundColor Green
	Copy-Item $_.FullName "$Webroot\bin\$(Split-Path $_.FullName -Leaf)" -Force
}

Copy-Item "$PSScriptRoot\PackageAutoloaderDemo\PackageAutoloader\demo2.zip" "$(New-Item "$Webroot\PackageAutoloader" -ItemType directory -Force)/Demo2.zip" -Force
Copy-Item "$PSScriptRoot\PackageAutoloaderDemo\PackageAutoloader\demo3.zip" "$(New-Item "$Webroot\PackageAutoloader" -ItemType directory -Force)/Demo3.zip" -Force
Copy-Item "$PSScriptRoot\PackageAutoloaderDemo\App_Config\Include\PackageAutoloaderDemo.config" "$(New-Item "$Webroot\App_Config\Include\PackageAutoloaderTest" -ItemType directory -Force)/PackageAutoloaderDemo.config" -Force
Get-ChildItem "$PSScriptRoot\PackageAutoloaderDemo\bin\Debug" | Foreach-Object{
	Write-Host "moving $($_.FullName) to bin root" -ForegroundColor Green
	Copy-Item $_.FullName "$Webroot\bin\$(Split-Path $_.FullName -Leaf)" -Force
}
