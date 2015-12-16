ECHO parameter=%1
CD %1
COPY DVDRipBatch.exe temp.exe
..\..\Build\ILMerge.exe /targetplatform:v4,C:\Windows\Microsoft.NET\Framework\v4.0.30319 /out:DVDRipBatch.exe temp.exe Microsoft.WindowsAPICodePack.Shell.dll Microsoft.WindowsAPICodePack.dll HandBrake.Framework.dll HandBrake.ApplicationServices.dll Growl.CoreLibrary.dll Growl.Connector.dll
DEL temp.exe
DEL Microsoft.WindowsAPICodePack.Shell.dll
DEL Microsoft.WindowsAPICodePack.dll
DEL HandBrake.Framework.dll
DEL HandBrake.ApplicationServices.dll
DEL Growl.CoreLibrary.dll
DEL Growl.Connector.dll
COPY DVDRipBatch.exe "D:\Filmes DivX\AutoRips\DVDRipBatch.exe" /Y