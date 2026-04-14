# name of the compiled dll
$dllName = "NINA.Plugin.DarksCustoms.dll"
# path to the manifest repository
$manifestPath = "$PSScriptRoot\..\..\..\nina.plugin.manifests\"
# download url
$url = "https://darkarchon.internet-box.ch:8443/$dllName"
# local publish path
$copyToPath = "Z:\wwwroot"

# file
$file = "$PSScriptRoot\bin\Release\$dllName"

# Read Metadata out of assembly
$bytes = [System.IO.File]::ReadAllBytes($file)
$assembly = [Reflection.Assembly]::Load($bytes)
$meta = [reflection.customattributedata]::GetCustomAttributes($assembly)
foreach($val in $meta) {
	if($val.AttributeType -like "System.Reflection.AssemblyTitleAttribute") {
		$name = $val.ConstructorArguments[0].Value
	}
	if($val.AttributeType -like "System.Reflection.AssemblyFileVersionAttribute") {
        $version = $val.ConstructorArguments[0];
	}
}

$manifestItemPath = "$manifestPath\manifests\$name\$($version.Value)"

# check if manifest path exists for specific version
if(!(Test-Path -Path $manifestItemPath)) {
    New-Item -ItemType Directory -Force -Path $manifestItemPath
}

# create manifest
."$PSScriptRoot\CreateManifest.ps1" -file $file -installerUrl $url

# copy manifest to manifest path and plugin dll to nina plugins folder
Copy-Item manifest.json $manifestItemPath
Copy-Item $file $env:LOCALAPPDATA\NINA\Plugins\

# publish item, can be replaced with a rest call if you figure out the api to upload to bitbucket
Copy-Item $file $copyToPath

# push to git
Set-Location -Path $manifestPath
git add -A
git commit -m "[AUTO] updated plugin manifest"
git push

# go back
Set-Location -Path $PSScriptRoot