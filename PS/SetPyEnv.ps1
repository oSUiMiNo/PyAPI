# スクリプト自身のパスを取得
$scriptPath = $MyInvocation.MyCommand.Path
# スクリプトが存在するディレクトリを取得
$scriptDir = Split-Path -Parent $scriptPath

# カレントディレクトリをスクリプトのディレクトリに変更
Set-Location -Path $scriptDir


# .python-versionファイルが存在するか確認
if (Test-Path -Path ".python-version") {
  # .python-versionファイルからバージョンを取得
  $pythonVersion = Get-Content -Path ".python-version" | Where-Object { $_ -match '\S' }

  # pyenvでインストール済みのバージョンを取得
  $installedVersions = pyenv versions --bare
  # Write-Host "AAA"
  # Write-Error "PPP"
  # バージョンがインストール済みか確認
  if ($installedVersions -notcontains $pythonVersion) {
    Write-Host "Python ${pythonVersion} is not installed. Installing..."

    # Pythonをインストール (エラーが発生した場合はキャッチ)
    try {
        Start-Process -FilePath "pyenv" -ArgumentList "install", $pythonVersion -Wait
        if ($LASTEXITCODE -ne 0) {
            Write-Error "pyenv install exited with code $($LASTEXITCODE)"
            return
        }
    }
    catch {
        Write-Error "Failed to install Python ${pythonVersion}: $_"
        return
    }
    Write-Host "Python ${pythonVersion} successfully installed."
  } else {
    Write-Host "Python ${pythonVersion} is already installed."
  }

 
  # pyenv localを設定
  try {
      Start-Process -FilePath "pyenv" -ArgumentList "local", $pythonVersion -Wait
          if ($LASTEXITCODE -ne 0) {
          Write-Error "pyenv local exited with code $($LASTEXITCODE)"
          return
      }
  }
  catch {
      Write-Error "Failed to set pyenv local to ${pythonVersion}: $_"
  }
}
else {
  Write-Warning ".python-version file not found in script's directory."
}

# Unityで扱いやすいように改行コードを統一
"`r`n" | Out-String | ForEach-Object {$_.TrimEnd()} | Out-Null