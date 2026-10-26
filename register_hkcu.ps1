$clsid = '{50C1F407-FFDB-4A07-8AD8-356579BF3A40}'
$progid = 'optimal2dx.Optimization2DX'
$dllPath = 'E:\Cut2dx\optimal2dx.dll'

# Register in HKCU\Software\Classes
$clsidKey = "HKCU:\Software\Classes\CLSID\$clsid"
New-Item -Path $clsidKey -Force | Out-Null
Set-ItemProperty -Path $clsidKey -Name '(Default)' -Value 'Optimization2DX Class'
$inprocKey = "$clsidKey\InprocServer32"
New-Item -Path $inprocKey -Force | Out-Null
Set-ItemProperty -Path $inprocKey -Name '(Default)' -Value $dllPath
Set-ItemProperty -Path $inprocKey -Name 'ThreadingModel' -Value 'Apartment'

$progIdSubKey = "$clsidKey\ProgID"
New-Item -Path $progIdSubKey -Force | Out-Null
Set-ItemProperty -Path $progIdSubKey -Name '(Default)' -Value $progid

$progKey = "HKCU:\Software\Classes\$progid"
New-Item -Path $progKey -Force | Out-Null
Set-ItemProperty -Path $progKey -Name '(Default)' -Value 'Optimization2DX Class'
$progClsidKey = "$progKey\CLSID"
New-Item -Path $progClsidKey -Force | Out-Null
Set-ItemProperty -Path $progClsidKey -Name '(Default)' -Value $clsid

Write-Output 'Registered in HKCU successfully!'
