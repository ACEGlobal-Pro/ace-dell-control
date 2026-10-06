Unicode true
RequestExecutionLevel admin
Name "ACE Dell Control Technical Preview"
OutFile "ACE-Dell-Control-Preview-Setup.exe"
; Fixed location (H4): the app runs elevated and loads a kernel driver and DLLs from its
; folder, so it is always installed under Program Files and there is no directory page.
InstallDir "$PROGRAMFILES64\ACE Dell Control Preview"
ShowInstDetails show
ShowUninstDetails show

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

; Same name as the app's single-instance mutex (M6/M7); seen across all sessions and users.
!macro AbortIfAppRunning
  System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Global\ACE.DellControl") i .r3'
  ${If} $3 <> 0
    System::Call 'kernel32::CloseHandle(i r3)'
    MessageBox MB_ICONSTOP "ACE Dell Control is running (possibly for another user). Close it first; it restores Dell automatic fan control on exit." /SD IDOK
    Abort
  ${EndIf}
!macroend

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "REVIEW-NOTICE.txt"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "This preview requires 64-bit Windows."
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "This preview requires Windows 10 or later."
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 461808
    MessageBox MB_ICONSTOP ".NET Framework 4.7.2 or later is required. Setup did not change your system."
    Abort
  ${EndIf}
  ; Ignore any /D= or registry location: always Program Files (H4).
  StrCpy $INSTDIR "$PROGRAMFILES64\ACE Dell Control Preview"
  !insertmacro AbortIfAppRunning
FunctionEnd

Section "ACE Dell Control" MainSection
  SetRegView 64
  SetShellVarContext all
  SetOutPath "$INSTDIR"
  File "staging\ACE Dell Control.exe"
  File "staging\*.cs"
  File "staging\app.manifest"
  File "staging\README-INSTALLER.txt"
  File "staging\REVIEW-NOTICE.txt"
  File "staging\THIRD-PARTY-NOTICES.txt"
  File "staging\DEPENDENCY-MANIFEST.txt"
  SetOutPath "$INSTDIR\tools\DellFanCmd"
  File /r "staging\tools\DellFanCmd\*.*"
  SetOutPath "$INSTDIR\tools\DellSetThermalSetting"
  File /r "staging\tools\DellSetThermalSetting\*.*"
  SetOutPath "$INSTDIR\tools\LibreHardwareMonitor"
  File /r "staging\tools\LibreHardwareMonitor\*.*"
  SetOutPath "$INSTDIR\source"
  File "staging\source\DellFanManagement-2.1.1-source.zip"
  File "staging\source\LibreHardwareMonitor-v0.9.6-source.zip"
  File "staging\source\bzh-windrv-dell-smm-io-67786c69-source.zip"
  File "staging\source\PawnIO.Modules-0.2.2-source.zip"
  File "staging\source\ACE-Dell-Control-Preview.nsi"
  File "staging\source\BUILDING.txt"
  SetOutPath "$INSTDIR\licenses"
  File /r "staging\licenses\*.*"

  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\ACE\DellControlPreview" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "DisplayName" "ACE Dell Control Technical Preview"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "DisplayVersion" "0.1.0-preview"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "Publisher" "ACE (technical preview)"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "DisplayIcon" "$INSTDIR\ACE Dell Control.exe"
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\ACE Dell Control Preview"
  CreateShortCut "$SMPROGRAMS\ACE Dell Control Preview\ACE Dell Control Preview.lnk" "$INSTDIR\ACE Dell Control.exe"
  CreateShortCut "$SMPROGRAMS\ACE Dell Control Preview\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortCut "$DESKTOP\ACE Dell Control Preview.lnk" "$INSTDIR\ACE Dell Control.exe"
SectionEnd

Section "Uninstall"
  SetRegView 64
  SetShellVarContext all
  !insertmacro AbortIfAppRunning
  ; The app's --restore mode applies the app's own model gate, verifies the restore from
  ; DellFanCmd's output (not its exit code) and is time-bounded. Exit 0 = confirmed or N/A.
  ReadRegStr $0 HKLM "HARDWARE\DESCRIPTION\System\BIOS" "SystemManufacturer"
  ReadRegStr $4 HKLM "HARDWARE\DESCRIPTION\System\BIOS" "SystemProductName"
  IfFileExists "$INSTDIR\ACE Dell Control.exe" 0 noApp
    ExecWait '"$INSTDIR\ACE Dell Control.exe" --restore' $1
    ${If} $1 != 0
      Goto unsafeToRemove
    ${EndIf}
    Goto restored
noApp:
  ${If} $0 == "Dell Inc."
  ${AndIf} $4 == "Latitude 7400"
    Goto unsafeToRemove
  ${EndIf}
restored:
  ${If} $0 == "Dell Inc."
  ${AndIf} $4 == "Latitude 7400"
    ; Remove a driver service left behind by a killed DellFanCmd so its files can be deleted.
    nsExec::Exec '"$SYSDIR\sc.exe" stop BZHDELLSMMIO'
    Pop $5
    nsExec::Exec '"$SYSDIR\sc.exe" delete BZHDELLSMMIO'
    Pop $5
  ${EndIf}
  Goto removeFiles
unsafeToRemove:
  MessageBox MB_ICONSTOP "Dell automatic fan control could not be confirmed. The app was not removed. Restart the laptop, then try again." /SD IDOK
  Abort
removeFiles:
  Delete "$DESKTOP\ACE Dell Control Preview.lnk"
  Delete "$SMPROGRAMS\ACE Dell Control Preview\ACE Dell Control Preview.lnk"
  Delete "$SMPROGRAMS\ACE Dell Control Preview\Uninstall.lnk"
  RMDir "$SMPROGRAMS\ACE Dell Control Preview"
  Delete "$INSTDIR\ACE Dell Control.exe"
  Delete "$INSTDIR\*.cs"
  Delete "$INSTDIR\app.manifest"
  Delete "$INSTDIR\README-INSTALLER.txt"
  Delete "$INSTDIR\REVIEW-NOTICE.txt"
  Delete "$INSTDIR\THIRD-PARTY-NOTICES.txt"
  Delete "$INSTDIR\DEPENDENCY-MANIFEST.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR\tools\DellFanCmd"
  RMDir /r "$INSTDIR\tools\DellSetThermalSetting"
  RMDir /r "$INSTDIR\tools\LibreHardwareMonitor"
  RMDir "$INSTDIR\tools"
  Delete "$INSTDIR\source\DellFanManagement-2.1.1-source.zip"
  Delete "$INSTDIR\source\LibreHardwareMonitor-v0.9.6-source.zip"
  Delete "$INSTDIR\source\bzh-windrv-dell-smm-io-67786c69-source.zip"
  Delete "$INSTDIR\source\PawnIO.Modules-0.2.2-source.zip"
  Delete "$INSTDIR\source\PawnIO.Modules-0.1.6-source.zip"
  Delete "$INSTDIR\source\ACE-Dell-Control-Preview.nsi"
  Delete "$INSTDIR\source\BUILDING.txt"
  RMDir "$INSTDIR\source"
  RMDir /r "$INSTDIR\licenses"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ACEDellControlPreview"
  DeleteRegKey HKLM "Software\ACE\DellControlPreview"
SectionEnd
