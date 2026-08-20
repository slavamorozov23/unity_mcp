param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $BridgeArguments
)

$directory = Get-Item -LiteralPath (Get-Location).Path
while ($null -ne $directory) {
    if ((Test-Path -LiteralPath (Join-Path $directory.FullName 'Assets')) -and
        (Test-Path -LiteralPath (Join-Path $directory.FullName 'ProjectSettings'))) {
        $project = $directory.FullName
        break
    }
    $directory = $directory.Parent
}

if ([string]::IsNullOrEmpty($project)) {
    throw 'The current directory is not inside a Unity project.'
}

$python = Join-Path $project 'Library\UnityAgentBridge\Runtime\venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    throw 'Unity Agent Bridge is not installed. Start its server in Unity.'
}

$projectClient = Join-Path $project 'Assets\UnityAgentBridge\CodexPlugin~\unity-agent-bridge\skills\unity-agent-bridge\scripts\bridge_client.py'
if (-not (Test-Path -LiteralPath $projectClient)) {
    throw "Unity Agent Bridge client is missing: $projectClient"
}

$pluginRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
if (Test-Path -LiteralPath (Join-Path $pluginRoot '.claude-plugin\plugin.json')) {
    $env:UNITY_AGENT_BRIDGE_CLIENT = 'claude'
} elseif (Test-Path -LiteralPath (Join-Path $pluginRoot '.codex-plugin\plugin.json')) {
    $env:UNITY_AGENT_BRIDGE_CLIENT = 'codex'
} else {
    Remove-Item Env:UNITY_AGENT_BRIDGE_CLIENT -ErrorAction SilentlyContinue
}
& $python $projectClient @BridgeArguments
exit $LASTEXITCODE
