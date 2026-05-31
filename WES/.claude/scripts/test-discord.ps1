# test-discord.ps1
# Discord webhook 연결 테스트.
# 실행:
#   powershell -ExecutionPolicy Bypass -File .claude\scripts\test-discord.ps1
#
# settings.local.json의 WES_DISCORD_WEBHOOK_URL이 제대로 설정되었는지 확인하고
# 테스트 메시지를 보낸다.

$ErrorActionPreference = "Stop"

$webhook = $env:WES_DISCORD_WEBHOOK_URL

if ([string]::IsNullOrEmpty($webhook)) {
    Write-Host "❌ WES_DISCORD_WEBHOOK_URL 환경변수가 설정되지 않았습니다." -ForegroundColor Red
    Write-Host "   .claude/settings.local.json 의 env.WES_DISCORD_WEBHOOK_URL 에 webhook URL을 넣고 Claude Code를 재시작하세요."
    exit 1
}

if ($webhook -like "*PLACEHOLDER*") {
    Write-Host "⚠️  WES_DISCORD_WEBHOOK_URL이 아직 PLACEHOLDER입니다." -ForegroundColor Yellow
    Write-Host "   .claude/settings.local.json 에 실제 webhook URL을 넣어주세요."
    exit 1
}

Write-Host "✓ Webhook URL 감지됨" -ForegroundColor Green

# 테스트 메시지 전송
$embed = @{
    color       = 3447003
    title       = "🧪 WES Agent Mirror 테스트"
    description = "Discord webhook이 정상 동작합니다.`n`n이 메시지가 보이면 셋업 완료."
    timestamp   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    footer      = @{ text = "test-discord.ps1" }
}

$body = @{
    embeds      = @($embed)
    thread_name = "test-thread"
} | ConvertTo-Json -Depth 10 -Compress

try {
    $resp = Invoke-RestMethod -Uri ($webhook + "?wait=true") -Method Post -Body $body -ContentType "application/json"
    Write-Host "✓ 메시지 전송 성공" -ForegroundColor Green
    if ($resp.channel_id) {
        Write-Host "  스레드 ID: $($resp.channel_id)"
    }
    Write-Host ""
    Write-Host "Discord 채널에서 'test-thread' 스레드를 확인하세요."
} catch {
    Write-Host "❌ 메시지 전송 실패: $_" -ForegroundColor Red
    exit 1
}
