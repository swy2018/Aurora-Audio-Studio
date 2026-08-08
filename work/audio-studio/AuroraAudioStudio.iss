#define MyAppName "Aurora Audio Studio"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishFolder
  #define PublishFolder "Aurora-Audio-Studio-1.0.0"
#endif
#ifndef InstallerFolder
  #define InstallerFolder "Aurora-Audio-Studio-1.0.0-installer"
#endif
#ifndef InstallerBaseName
  #define InstallerBaseName "Aurora-Audio-Studio-1.0.0-Setup-x64"
#endif
#define MyAppPublisher "Aurora Contributors"
#define MyAppURL "https://github.com/swy2018/Aurora-Audio-Studio"
#define MyAppExeName "Aurora Audio Studio.exe"
#define MyAppCopyright "Copyright (C) 2026 Aurora Contributors"

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
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription=Aurora Audio Studio installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

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
english.desktopicon=Create a desktop shortcut
chinesesimplified.desktopicon=创建桌面快捷方式
chinesetraditional.desktopicon=建立桌面捷徑
japanese.desktopicon=デスクトップにショートカットを作成する
english.RemovePersonalDataPrompt=Remove Aurora personal settings from this Windows account?%n%nThis deletes Aurora settings, logs, task history, and update cache. AI models, projects, source media, and generated outputs are always kept.%n%nChoose No to keep your settings for a future installation.
chinesesimplified.RemovePersonalDataPrompt=是否删除此 Windows 账户中的 Aurora 个人配置？%n%n这将删除 Aurora 的设置、日志、任务记录和更新缓存。AI 模型、项目、源素材与生成成品始终保留。%n%n选择“否”可保留配置，便于以后重新安装。
chinesetraditional.RemovePersonalDataPrompt=是否刪除此 Windows 帳戶中的 Aurora 個人設定？%n%n這將刪除 Aurora 的設定、記錄、任務記錄和更新快取。AI 模型、專案、來源素材與生成成品始終保留。%n%n選擇「否」可保留設定，方便日後重新安裝。
japanese.RemovePersonalDataPrompt=この Windows アカウントの Aurora 個人設定を削除しますか？%n%nAurora の設定、ログ、タスク履歴、更新キャッシュが削除されます。AI モデル、プロジェクト、素材、生成ファイルは常に保持されます。%n%n再インストール用に設定を残す場合は「いいえ」を選択してください。
english.UpdaterWindowTitle=Aurora Updater
chinesesimplified.UpdaterWindowTitle=Aurora 更新程序
chinesetraditional.UpdaterWindowTitle=Aurora 更新程式
japanese.UpdaterWindowTitle=Aurora アップデーター
english.UpdaterEyebrow=AURORA AUDIO STUDIO
chinesesimplified.UpdaterEyebrow=AURORA AUDIO STUDIO
chinesetraditional.UpdaterEyebrow=AURORA AUDIO STUDIO
japanese.UpdaterEyebrow=AURORA AUDIO STUDIO
english.UpdaterTitle=Installing Aurora {#MyAppVersion}
chinesesimplified.UpdaterTitle=正在安装 Aurora {#MyAppVersion}
chinesetraditional.UpdaterTitle=正在安裝 Aurora {#MyAppVersion}
japanese.UpdaterTitle=Aurora {#MyAppVersion} をインストール中
english.UpdaterDescription=Aurora is updating in the background. Your settings, models, projects, and outputs will be preserved.
chinesesimplified.UpdaterDescription=Aurora 正在后台完成更新。你的设置、模型、项目和成品都会保留。
chinesetraditional.UpdaterDescription=Aurora 正在背景完成更新。你的設定、模型、專案和成品都會保留。
japanese.UpdaterDescription=Aurora をバックグラウンドで更新しています。設定、モデル、プロジェクト、生成ファイルは保持されます。
english.UpdaterPreparing=Preparing the update
chinesesimplified.UpdaterPreparing=正在准备更新
chinesetraditional.UpdaterPreparing=正在準備更新
japanese.UpdaterPreparing=更新を準備しています
english.UpdaterInstalling=Installing application files
chinesesimplified.UpdaterInstalling=正在安装应用文件
chinesetraditional.UpdaterInstalling=正在安裝應用程式檔案
japanese.UpdaterInstalling=アプリケーションファイルをインストールしています
english.UpdaterFinishing=Finishing installation
chinesesimplified.UpdaterFinishing=正在完成安装
chinesetraditional.UpdaterFinishing=正在完成安裝
japanese.UpdaterFinishing=インストールを完了しています
english.UpdaterRestarting=Update complete. Restarting Aurora…
chinesesimplified.UpdaterRestarting=更新完成，正在重新启动 Aurora…
chinesetraditional.UpdaterRestarting=更新完成，正在重新啟動 Aurora…
japanese.UpdaterRestarting=更新が完了しました。Aurora を再起動しています…
[Code]
var
  DeletePersonalData: Boolean;
  UpdateForm: TSetupForm;
  UpdateProgressBar: TNewProgressBar;
  UpdateStatusLabel: TNewStaticText;
  UpdatePercentLabel: TNewStaticText;

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

procedure InitializeWizard();
var
  EyebrowLabel: TNewStaticText;
  TitleLabel: TNewStaticText;
  DescriptionLabel: TNewStaticText;
  Divider: TBevel;
begin
  if not IsAutomaticUpdate() then
    Exit;

  UpdateForm := CreateCustomForm(ScaleX(560), ScaleY(260), False, False);
  UpdateForm.Caption := CustomMessage('UpdaterWindowTitle');
  UpdateForm.ClientWidth := ScaleX(560);
  UpdateForm.ClientHeight := ScaleY(260);
  UpdateForm.Position := poScreenCenter;
  UpdateForm.BorderStyle := bsSingle;
  UpdateForm.BorderIcons := [];
  UpdateForm.Color := $00F8FBF9;

  EyebrowLabel := TNewStaticText.Create(UpdateForm);
  EyebrowLabel.Parent := UpdateForm;
  EyebrowLabel.Left := ScaleX(32);
  EyebrowLabel.Top := ScaleY(28);
  EyebrowLabel.Caption := CustomMessage('UpdaterEyebrow');
  EyebrowLabel.Font.Name := 'Segoe UI';
  EyebrowLabel.Font.Size := 9;
  EyebrowLabel.Font.Style := [fsBold];
  EyebrowLabel.Font.Color := $006B7D75;

  TitleLabel := TNewStaticText.Create(UpdateForm);
  TitleLabel.Parent := UpdateForm;
  TitleLabel.Left := ScaleX(32);
  TitleLabel.Top := ScaleY(55);
  TitleLabel.Caption := CustomMessage('UpdaterTitle');
  TitleLabel.Font.Name := 'Segoe UI';
  TitleLabel.Font.Size := 18;
  TitleLabel.Font.Style := [fsBold];
  TitleLabel.Font.Color := $0020312B;

  DescriptionLabel := TNewStaticText.Create(UpdateForm);
  DescriptionLabel.Parent := UpdateForm;
  DescriptionLabel.Left := ScaleX(32);
  DescriptionLabel.Top := ScaleY(92);
  DescriptionLabel.Width := ScaleX(496);
  DescriptionLabel.Height := ScaleY(42);
  DescriptionLabel.AutoSize := False;
  DescriptionLabel.WordWrap := True;
  DescriptionLabel.Caption := CustomMessage('UpdaterDescription');
  DescriptionLabel.Font.Name := 'Segoe UI';
  DescriptionLabel.Font.Size := 10;
  DescriptionLabel.Font.Color := $005D6F68;

  Divider := TBevel.Create(UpdateForm);
  Divider.Parent := UpdateForm;
  Divider.Left := ScaleX(32);
  Divider.Top := ScaleY(145);
  Divider.Width := ScaleX(496);
  Divider.Height := ScaleY(1);
  Divider.Shape := bsTopLine;

  UpdateStatusLabel := TNewStaticText.Create(UpdateForm);
  UpdateStatusLabel.Parent := UpdateForm;
  UpdateStatusLabel.Left := ScaleX(32);
  UpdateStatusLabel.Top := ScaleY(166);
  UpdateStatusLabel.Caption := CustomMessage('UpdaterPreparing');
  UpdateStatusLabel.Font.Name := 'Segoe UI';
  UpdateStatusLabel.Font.Size := 10;
  UpdateStatusLabel.Font.Color := $0020312B;

  UpdatePercentLabel := TNewStaticText.Create(UpdateForm);
  UpdatePercentLabel.Parent := UpdateForm;
  UpdatePercentLabel.Left := ScaleX(490);
  UpdatePercentLabel.Top := ScaleY(166);
  UpdatePercentLabel.Width := ScaleX(38);
  UpdatePercentLabel.Alignment := taRightJustify;
  UpdatePercentLabel.Caption := '0%';
  UpdatePercentLabel.Font.Name := 'Segoe UI';
  UpdatePercentLabel.Font.Size := 10;
  UpdatePercentLabel.Font.Style := [fsBold];
  UpdatePercentLabel.Font.Color := $002A826C;

  UpdateProgressBar := TNewProgressBar.Create(UpdateForm);
  UpdateProgressBar.Parent := UpdateForm;
  UpdateProgressBar.Left := ScaleX(32);
  UpdateProgressBar.Top := ScaleY(194);
  UpdateProgressBar.Width := ScaleX(496);
  UpdateProgressBar.Height := ScaleY(14);
  UpdateProgressBar.Min := 0;
  UpdateProgressBar.Max := 100;
  UpdateProgressBar.Position := 0;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if IsAutomaticUpdate() and (CurPageID = wpInstalling) then
  begin
    WizardForm.Hide();
    UpdateStatusLabel.Caption := CustomMessage('UpdaterInstalling');
    UpdateForm.Show();
    UpdateForm.BringToFront();
  end;
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
var
  Percent: Integer;
begin
  if not IsAutomaticUpdate() or (MaxProgress <= 0) then
    Exit;
  if MaxProgress >= 100 then
    Percent := ((CurProgress div 100) * 100) div (MaxProgress div 100)
  else
    Percent := (CurProgress * 100) div MaxProgress;
  if Percent > 100 then
    Percent := 100;
  UpdateProgressBar.Position := Percent;
  UpdatePercentLabel.Caption := IntToStr(Percent) + '%';
  if Percent >= 92 then
    UpdateStatusLabel.Caption := CustomMessage('UpdaterFinishing')
  else
    UpdateStatusLabel.Caption := CustomMessage('UpdaterInstalling');
  UpdateForm.Update();
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
    begin
      Log('Aurora Audio Studio installation completed successfully.');
      if IsAutomaticUpdate() then
      begin
        UpdateProgressBar.Position := 100;
        UpdatePercentLabel.Caption := '100%';
        UpdateStatusLabel.Caption := CustomMessage('UpdaterRestarting');
        UpdateForm.Update();
        Sleep(650);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
    begin
      ConfigurePersonalDataRemoval();
      Log('Aurora Audio Studio uninstall started. Models, projects, source media, and outputs will be preserved.');
    end;
    usPostUninstall:
    begin
      if DeletePersonalData then
      begin
        Log('Removing Aurora settings, logs, task history, model metadata, and update cache for the current Windows account.');
        DelTree(ExpandConstant('{localappdata}\Aurora Audio Studio'), True, True, True);
      end;
      Log('Aurora Audio Studio uninstall completed. Models, projects, source media, and outputs were preserved.');
    end;
  end;
end;
