#define MyAppName "Aurora Audio Studio"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.2-beta.1"
#endif
#ifndef PublishFolder
  #define PublishFolder "Aurora-Audio-Studio-" + MyAppVersion
#endif
#ifndef InstallerFolder
  #define InstallerFolder "Aurora-Audio-Studio-" + MyAppVersion + "-installer"
#endif
#ifndef InstallerBaseName
  #define InstallerBaseName "Aurora-Audio-Studio-" + MyAppVersion + "-Setup-x64"
#endif
#define MyAppPublisher "Aurora Contributors"
#define MyAppURL "https://github.com/swy2018/Aurora-Audio-Studio"
#define MyAppExeName "Aurora Audio Studio.exe"
#define MyAppCopyright "Copyright (C) 2026 Aurora Contributors"
#define VCRuntimeVersion GetVersionNumbersString("AuroraAudioStudio\Runtime\prerequisites\vc_redist.x64.exe")

[Setup]
AppId={{B8D7DD9A-AFCB-4E3B-96EA-95F67743578A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright={#MyAppCopyright}
AppComments=Local AI audio production workspace for Windows
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyPage=no
DisableFinishedPage=no
AlwaysShowDirOnReadyPage=yes
AlwaysShowGroupOnReadyPage=yes
AllowNoIcons=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousLanguage=yes
UsePreviousTasks=yes
LicenseFile=..\..\LICENSE
InfoBeforeFile=installer\INSTALL-NOTES.txt
InfoAfterFile=installer\INSTALL-COMPLETE.txt
OutputDir=..\..\publish\{#InstallerFolder}
OutputBaseFilename={#InstallerBaseName}
SetupIconFile=AuroraAudioStudio\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon-{#MyAppVersion}.ico
UninstallDisplayName={#MyAppName} {#MyAppVersion}
Uninstallable=yes
UninstallLogging=yes
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
DefaultDialogFontName=Segoe UI
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
SetupMutex=AuroraAudioStudioInstaller
MinVersion=10.0.17763
VersionInfoVersion=2.0.2.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription=Aurora Audio Studio installer
VersionInfoProductName={#MyAppName}
VersionInfoProductTextVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "..\..\LICENSE"
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"; LicenseFile: "installer\GPL-3.0-zh-CN.txt"
Name: "chinesetraditional"; MessagesFile: "languages\ChineseTraditional.isl"; LicenseFile: "installer\GPL-3.0-zh-TW.txt"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; LicenseFile: "..\..\LICENSE"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\publish\{#PublishFolder}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "AuroraAudioStudio\Assets\AppIcon.ico"; DestDir: "{app}\Assets"; DestName: "AppIcon-{#MyAppVersion}.ico"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "README-给音乐人的使用说明.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "AuroraAudioStudio\Runtime\prerequisites\vc_redist.x64.exe"; Flags: dontcopy
Source: "AuroraAudioStudio\Runtime\prerequisites\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon-{#MyAppVersion}.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\AppIcon-{#MyAppVersion}.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; Check: not IsAutomaticUpdate
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runascurrentuser; Check: IsAutomaticUpdate

[Messages]
english.ConfirmUninstall=Do you want to uninstall Aurora Audio Studio?%n%nApplication files and shortcuts will be removed. Your AI models, generated outputs, and personal media will be kept.
english.UninstalledAll=Aurora Audio Studio was removed successfully.%n%nYour AI models, generated outputs, and personal media were kept.
chinesesimplified.ConfirmUninstall=是否卸载 Aurora Audio Studio？%n%n卸载程序将移除应用文件和快捷方式。AI 模型、生成成品和个人素材会继续保留。
chinesesimplified.UninstalledAll=Aurora Audio Studio 已成功卸载。%n%nAI 模型、生成成品和个人素材已保留。
chinesetraditional.ConfirmUninstall=是否解除安裝 Aurora Audio Studio？%n%n解除安裝程式將移除應用程式檔案和捷徑。AI 模型、生成成品和個人素材會繼續保留。
chinesetraditional.UninstalledAll=Aurora Audio Studio 已成功解除安裝。%n%nAI 模型、生成成品和個人素材已保留。
japanese.ConfirmUninstall=Aurora Audio Studio をアンインストールしますか？%n%nアプリとショートカットのみ削除します。AI モデル、生成ファイル、個人素材は保持されます。
japanese.UninstalledAll=Aurora Audio Studio をアンインストールしました。%n%nAI モデル、生成ファイル、個人素材は保持されています。

[CustomMessages]
english.RuntimeFailed=Microsoft Visual C++ runtime setup did not complete. Aurora has not been replaced. Check the setup log and retry. Error code:
chinesesimplified.RuntimeFailed=Microsoft Visual C++ 运行组件未安装完成，Aurora 尚未被替换。请检查安装日志后重试。错误码：
chinesetraditional.RuntimeFailed=Microsoft Visual C++ 執行元件未安裝完成，Aurora 尚未被取代。請檢查安裝記錄後重試。錯誤碼：
japanese.RuntimeFailed=Microsoft Visual C++ ランタイムを準備できませんでした。Aurora は変更されていません。ログを確認して再試行してください。エラーコード:
english.WebViewFailed=The Microsoft WebView2 workbench component could not be installed. Aurora has not been replaced. Check the setup log and retry. Error code:
chinesesimplified.WebViewFailed=Microsoft WebView2 工作台组件未安装完成，Aurora 尚未被替换。请检查安装日志后重试。错误码：
chinesetraditional.WebViewFailed=Microsoft WebView2 工作台元件未安裝完成，Aurora 尚未被取代。請檢查安裝記錄後重試。錯誤碼：
japanese.WebViewFailed=Microsoft WebView2 ワークベンチコンポーネントを準備できませんでした。Aurora は変更されていません。ログを確認して再試行してください。エラーコード:
english.RuntimeRestart=The Microsoft runtime requires a Windows restart. Restart Windows, then run the Aurora installer again. Aurora has not been replaced.
chinesesimplified.RuntimeRestart=Microsoft 运行组件需要重启 Windows。请重启后再次运行 Aurora 安装程序；当前 Aurora 尚未被替换。
chinesetraditional.RuntimeRestart=Microsoft 執行元件需要重新啟動 Windows。請重新啟動後再次執行 Aurora 安裝程式；目前的 Aurora 尚未被取代。
japanese.RuntimeRestart=Microsoft ランタイムの準備には Windows の再起動が必要です。再起動後に Aurora のインストールを再実行してください。現在の Aurora は変更されていません。
english.desktopicon=Create a desktop shortcut
chinesesimplified.desktopicon=创建桌面快捷方式
chinesetraditional.desktopicon=建立桌面捷徑
japanese.desktopicon=デスクトップにショートカットを作成する
english.RemovePersonalDataPrompt=Clear Aurora settings and task history for this Windows account?%n%nOnly application settings, window state, saved processing options, and task history are cleared. Models, processing records, source media, outputs, logs, and other files are kept, including files inside the settings folder.%n%nChoose No to keep your settings for a future installation.
chinesesimplified.RemovePersonalDataPrompt=是否清除此 Windows 账户的 Aurora 设置与任务历史？%n%n仅清除应用设置、窗口状态、已保存的处理选项和任务历史。模型、处理记录、素材、成品、日志及其他文件均保留，包括存放在设置目录内的文件。%n%n选择“否”可保留设置，便于以后重新安装。
chinesetraditional.RemovePersonalDataPrompt=是否清除此 Windows 帳戶的 Aurora 設定與任務歷史？%n%n僅清除應用程式設定、視窗狀態、已儲存的處理選項和任務歷史。模型、處理記錄、素材、成果、記錄檔及其他檔案均保留，包括放在設定目錄內的檔案。%n%n選擇「否」可保留設定，方便日後重新安裝。
japanese.RemovePersonalDataPrompt=この Windows アカウントの Aurora 設定とタスク履歴を消去しますか？%n%nアプリ設定、ウィンドウ状態、保存済み処理設定、タスク履歴のみを消去します。モデル、処理記録、素材、成果、ログ、その他のファイルは、設定フォルダー内のものも含めて保持されます。%n%n再インストール用に設定を残す場合は「いいえ」を選択してください。
[Code]
var
  DeletePersonalData: Boolean;

function HasVCRuntime(): Boolean;
var
  Installed: Cardinal;
  Key, VersionText: String;
  InstalledVersion, RequiredVersion: Int64;
begin
  { Microsoft documents this key and recommends skipping newer installed runtimes. }
  Key := 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
  Result := False;
  if not RegQueryDWordValue(HKLM64, Key, 'Installed', Installed) or (Installed <> 1) then Exit;
  if not RegQueryStringValue(HKLM64, Key, 'Version', VersionText) then Exit;
  if Copy(VersionText, 1, 1) = 'v' then Delete(VersionText, 1, 1);
  if not StrToVersion(VersionText, InstalledVersion) then Exit;
  if not StrToVersion('{#VCRuntimeVersion}', RequiredVersion) then Exit;
  Result := ComparePackedVersion(InstalledVersion, RequiredVersion) >= 0;
end;

function PrepareVCRuntime(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if HasVCRuntime() then Exit;
  ExtractTemporaryFile('vc_redist.x64.exe');
  Log('Preparing the bundled Microsoft Visual C++ x64 runtime.');
  if not Exec(ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart /log "' + ExpandConstant('{tmp}\Aurora-vcredist.log') + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) then
  begin
    Result := CustomMessage('RuntimeFailed') + ' ' + IntToStr(Code);
    Exit;
  end;
  Log('Visual C++ runtime exit code: ' + IntToStr(Code));
  if Code = 3010 then
  begin
    NeedsRestart := True;
    Result := CustomMessage('RuntimeRestart');
  end
  else if ((Code <> 0) and (Code <> 1638)) or not HasVCRuntime() then
    Result := CustomMessage('RuntimeFailed') + ' ' + IntToStr(Code);
end;

function HasWebView2Runtime(): Boolean;
var
  Key, VersionText: String;
  Version: Int64;
begin
  { Microsoft recommends checking both machine and user registrations, not Edge browser presence. }
  Key := 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := False;
  if RegQueryStringValue(HKLM32, Key, 'pv', VersionText) then
    if StrToVersion(VersionText, Version) and (Version > 0) then Result := True;
  if not Result and RegQueryStringValue(HKCU, Key, 'pv', VersionText) then
    if StrToVersion(VersionText, Version) and (Version > 0) then Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := PrepareVCRuntime(NeedsRestart);
  if Result <> '' then Exit;
  if HasWebView2Runtime() then Exit;
  { https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution#offline-deployment }
  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  Log('Preparing the bundled offline Microsoft WebView2 runtime.');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := CustomMessage('WebViewFailed') + ' ' + IntToStr(Code)
  else if Code = 3010 then
  begin
    NeedsRestart := True;
    Result := CustomMessage('RuntimeRestart');
  end
  else if (Code <> 0) or not HasWebView2Runtime() then
    Result := CustomMessage('WebViewFailed') + ' ' + IntToStr(Code);
  Log('WebView2 runtime exit code: ' + IntToStr(Code));
end;

function HasCommandLineParam(const Value: String): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
    if CompareText(ParamStr(Index), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function IsAutomaticUpdate(): Boolean;
begin
  Result := HasCommandLineParam('/UPDATE');
end;

procedure ConfigurePersonalDataRemoval();
begin
  DeletePersonalData := False;

  if HasCommandLineParam('/REMOVEUSERDATA') then
    DeletePersonalData := True
  else if HasCommandLineParam('/KEEPUSERDATA') or HasCommandLineParam('/SILENT') or HasCommandLineParam('/VERYSILENT') then
    DeletePersonalData := False
  else
    DeletePersonalData := SuppressibleMsgBox(
      CustomMessage('RemovePersonalDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:
      Log('Aurora Audio Studio installation started.');
    ssPostInstall:
      Log('Aurora Audio Studio installation completed successfully.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
    begin
      ConfigurePersonalDataRemoval();
      Log('Aurora Audio Studio uninstall started. Models, processing records, source media, and outputs will be preserved.');
    end;
    usPostUninstall:
    begin
      if DeletePersonalData then
      begin
        { Never recurse: users may keep models or outputs inside this directory. }
        Log('Removing only the four known Aurora preference/history files; all directories and other files are retained.');
        DeleteFile(ExpandConstant('{localappdata}\Aurora Audio Studio\settings.json'));
        DeleteFile(ExpandConstant('{localappdata}\Aurora Audio Studio\tasks.json'));
        DeleteFile(ExpandConstant('{localappdata}\Aurora Audio Studio\utility-drafts.json'));
        DeleteFile(ExpandConstant('{localappdata}\Aurora Audio Studio\window-state.json'));
      end;
      Log('Aurora Audio Studio uninstall completed. Models, processing records, source media, and outputs were preserved.');
    end;
  end;
end;
