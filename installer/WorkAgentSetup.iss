; Inno Setup Script for Enterprise Work Agent
; Generates a lightweight, single-file installer for company laptops

#define MyAppName "Enterprise Work Agent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Enterprise Solutions Corp"
#define MyAppURL "https://yourcompany.com"
#define MyAppExeName "WorkAgent.exe"

[Setup]
AppId={{5A8E40B7-91B1-4A59-866B-C298285514E9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\WorkAgent
DisableProgramGroupPage=yes
; Per-user install: lowest privileges (no admin UAC prompt needed)
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=WorkAgentSetup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Distributable published files from dotnet publish
Source: "..\windows-agent\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
