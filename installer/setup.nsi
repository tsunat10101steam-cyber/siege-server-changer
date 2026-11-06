; Siege Server Changer installer (per-user, no admin rights needed)
; Build: powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1

Unicode true
ManifestDPIAware true
SetCompressor /SOLID lzma

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!define APPNAME     "레식 서버 변경기"
!define APPID       "SiegeServerChanger"
!define EXENAME     "SiegeServerChanger.exe"
!define PUBLISHDIR  "..\publish"
!define UNINSTKEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPID}"

Name "${APPNAME}"
!ifndef OUTFILE
  !define OUTFILE "${PUBLISHDIR}\${APPID}-Setup-${VERSION}.exe"
!endif
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\${APPID}"
InstallDirRegKey HKCU "Software\${APPID}" "InstallDir"
RequestExecutionLevel user

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Siege Server Changer"
VIAddVersionKey "FileDescription" "Siege Server Changer Setup"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" ""

!include "MUI2.nsh"

!define MUI_ICON   "..\src\SiegeServerChanger.App\app.ico"
!define MUI_UNICON "..\src\SiegeServerChanger.App\app.ico"
!define MUI_ABORTWARNING

!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXENAME}"
!define MUI_FINISHPAGE_RUN_TEXT "지금 실행"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Korean"

; The app must not be running while its exe is replaced/removed.
; SKIP_CLOSE is only for local test builds.
!macro CloseRunningApp
  !ifndef SKIP_CLOSE
    nsExec::Exec 'taskkill /IM "${EXENAME}" /F'
    Pop $0
    Sleep 500
  !endif
!macroend

Section "프로그램 (필수)" SecMain
  SectionIn RO
  !insertmacro CloseRunningApp

  SetOutPath "$INSTDIR"
  File "${PUBLISHDIR}\${EXENAME}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"

  WriteRegStr HKCU "Software\${APPID}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${EXENAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "Siege Server Changer"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" 65000
SectionEnd

Section "바탕화면 바로가기" SecDesktop
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "레식 서버 변경기 프로그램과 시작 메뉴 바로가기"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "바탕화면에 바로가기를 만듭니다"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  !insertmacro CloseRunningApp

  Delete "$INSTDIR\${EXENAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  Delete "$DESKTOP\${APPNAME}.lnk"

  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\${APPID}"

  ; Favorites/recent. The game's GameSettings.ini and its .bak are never touched.
  IfSilent keep
  MessageBox MB_YESNO|MB_ICONQUESTION "즐겨찾기, 최근 서버 같은 프로그램 설정도 지울까요?$\n(게임 설정 파일은 건드리지 않습니다)" IDNO keep
    RMDir /r "$APPDATA\${APPID}"
  keep:
SectionEnd
