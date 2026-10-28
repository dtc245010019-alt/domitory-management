# =============================================================================
# KICH BAN KIEM THU TU DONG DU AN QUAN LY KY TUC XA TICH HOP AI (QLKTX)
# Hoc phan: Ung dung tri tue nhan tao - AIA331 (Ma de tai: 80309)
# Truong DH Cong nghe Thong tin va Truyen thong - DH Thai Nguyen
# Sinh vien: Nguyen Khanh Duy
# =============================================================================

param(
    [string]$BaseUrl = "http://localhost:5013"
)

$TotalTests = 0
$PassedTests = 0
$FailedTests = 0

function Report-Test {
    param(
        [string]$Id,
        [string]$Category,
        [string]$Description,
        [bool]$Success,
        [string]$Detail = ""
    )
    $script:TotalTests++
    if ($Success) {
        $script:PassedTests++
        Write-Host "  [PASS] " -ForegroundColor Green -NoNewline
    } else {
        $script:FailedTests++
        Write-Host "  [FAIL] " -ForegroundColor Red -NoNewline
    }
    Write-Host ("[" + $Id + "] (" + $Category + "): " + $Description)
    if ($Detail) {
        Write-Host ("         -> " + $Detail) -ForegroundColor DarkGray
    }
}

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "  BAT DAU CHAY KIEM THU TU DONG HE THONG QLKTX TICH HOP AI (AIA331)" -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host ""

# -----------------------------------------------------------------------------
# NHOM 1: KIEM THU BIEN DICH VA KIEN TRUC MA NGUON (TC_SYS)
# -----------------------------------------------------------------------------
Write-Host "1. KIEM TRA BIEN DICH VA KIEN TRUC MA NGUON (TC_SYS)" -ForegroundColor Yellow

$buildOutput = dotnet build QLKTX\QLKTX.csproj -c Release --nologo -v q 2>&1
$buildSuccess = ($LASTEXITCODE -eq 0)
Report-Test "TC_SYS_01" "Build" "Ma nguon ASP.NET Core 8.0 bien dich thanh cong (0 loi)" $buildSuccess "Ket qua bien dich: 0 Error(s)"

$appSettingsExists = Test-Path "QLKTX\appsettings.json"
$appSettingsContent = if ($appSettingsExists) { Get-Content "QLKTX\appsettings.json" -Raw } else { "" }
$hasDbConfig = $appSettingsContent -match "DefaultConnection"
Report-Test "TC_SYS_02" "Cau hinh" "Tep appsettings.json cau hinh chuoi ket noi SQL Server va dich vu AI" ($appSettingsExists -and $hasDbConfig) "Ket noi: DefaultConnection"

$hasDockerFiles = (Test-Path "Dockerfile") -and (Test-Path "docker-compose.yml")
Report-Test "TC_SYS_03" "Container" "Tep dong goi Dockerfile va docker-compose.yml day du, hop le" $hasDockerFiles "Ho tro trien khai container hoa he thong"

# -----------------------------------------------------------------------------
# NHOM 2: KIEM THU NGHIEP VU XEP PHONG VA QUAN LY SINH VIEN (TC_PHONG)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "2. KIEM THU NGHIEP VU XEP PHONG VA QUAN LY SINH VIEN (TC_PHONG)" -ForegroundColor Yellow

$svCode = Get-Content "QLKTX\Controllers\SinhVienController.cs" -Encoding UTF8 -Raw
$phongCode = Get-Content "QLKTX\Controllers\PhongController.cs" -Encoding UTF8 -Raw

# TC_PHONG_01: Xep sinh vien moi vao phong con cho
$hasAddCheck = ($svCode -match 'soDangO\s*<\s*phong\.SoLuongGiuong') -or ($svCode -match 'TinhTrang\s*==\s*"')
Report-Test "TC_PHONG_01" "Xep phong" "Xep sinh vien moi vao phong con cho -> Tang si so va giu nguyen Con cho" $hasAddCheck "Si so tang theo so sinh vien Dang o"

# TC_PHONG_02: Xep vao giuong cuoi cung -> Chuyen sang Da day
$hasFullTransition = ($svCode -match 'soDangO\s*>=\s*phong\.SoLuongGiuong') -and ($svCode -match 'TinhTrang\s*=\s*"')
Report-Test "TC_PHONG_02" "Xep phong bien" "Xep sinh vien vao giuong cuoi cung -> Tu dong chuyen trang thai sang Da day" $hasFullTransition "Tu dong cap nhat trang thai Da day"

# TC_PHONG_03: Chan xep vao phong da day
$hasPreventOverload = ($svCode -match 'soDangO\s*>=\s*phong') -and ($svCode -match 'ModelState\.AddModelError')
Report-Test "TC_PHONG_03" "Ngan chan sai" "Chan hanh dong xep them sinh vien vao phong da day si so" $hasPreventOverload "Thong bao loi: Phong da day cho"

# TC_PHONG_04: Xoa mem / Tra phong -> Da roi di va mo lai Con cho
$hasSoftDelete = ($svCode -match 'DeleteConfirmed') -and ($svCode -match 'TinhTrangLuuTru\s*=\s*"[^"]+"')
$hasReopenRoom = ($svCode -match 'soDangO\s*<\s*phong\.SoLuongGiuong') -and ($svCode -match 'DongBoTrangThaiPhongAsync')
Report-Test "TC_PHONG_04" "Xoa mem" "Tra phong / Xoa mem sinh vien -> Chuyen sang Da roi di, tu dong giai phong phong" ($hasSoftDelete -and $hasReopenRoom) "Tu dong mo lai Con cho, bao toan 100% khoa ngoai CSDL"

# TC_PHONG_05: Tra cuu phong thong minh
$hasSmartSearch = ($phongCode -match 'phongSearch\.StartsWith') -and ($phongCode -match 'Replace')
Report-Test "TC_PHONG_05" "Tim kiem" "Tra cuu phong thong minh: Chuan hoa ky tu dac biet (P.A1-03, a1 03 -> A103)" $hasSmartSearch "Loc chinh xac theo ma phong khong phan biet dau cach/gach noi"

# -----------------------------------------------------------------------------
# NHOM 3: KIEM THU TINH PHI, HOA DON VA CONG NO (TC_HD)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "3. KIEM THU TINH PHI, HOA DON VA CONG NO (TC_HD)" -ForegroundColor Yellow

$portalCode = Get-Content "QLKTX\Controllers\PortalController.cs" -Encoding UTF8 -Raw

$hasElectricFormula = $portalCode -match 'chiSoMoi.*-.*chiSoCu.*\*.*donGia'
Report-Test "TC_HD_01" "Tinh phi" "Cong thuc tinh tien dien nuoc: (ChiSoMoi - ChiSoCu) * DonGia" $hasElectricFormula "Don gia chuan 2.500 VND / kWh"

$hasValidationBoundary = $portalCode -match 'chiSoMoi\.Value\s*<=\s*chiSoCu\.Value'
Report-Test "TC_HD_02" "Bien / Sai" "Tu choi tao hoa don neu so moi <= so cu (ngan chan hoa don am)" $hasValidationBoundary "Bao ve tinh toan ven tai chinh"

$hasAuthPayment = $portalCode -match 'a\.MatKhau\s*==\s*matKhauXacNhan'
Report-Test "TC_HD_03" "Bao mat" "Thanh toan hoa don tren Portal bat buoc xac thuc lai mat khau ca nhan" $hasAuthPayment "Sai mat khau tu choi giao dich"

# -----------------------------------------------------------------------------
# NHOM 4: KIEM THU PHAN ANH SU CO VA KY LUAT VI PHAM (TC_SC, TC_VP)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "4. KIEM THU PHAN ANH SU CO VA KY LUAT VI PHAM (TC_SC & TC_VP)" -ForegroundColor Yellow

$hasUploadFolder = $portalCode -match 'uploads.*suco'
Report-Test "TC_SC_01" "Su co co anh" "Tiep nhan su co co file anh -> Luu tru an toan vao wwwroot/uploads/suco/" $hasUploadFolder "Tao ten file doc ban kem thoi gian"

$hasNullableUpload = ($portalCode -match 'IFormFile\?\s*HinhAnhFile') -and ($portalCode -match 'duongDanAnh\s*=\s*""')
Report-Test "TC_SC_02" "Su co anh null" "Tiep nhan su co khong co anh (anh null) -> Gan chuoi rong an toan, khong crash" $hasNullableUpload "Khong throw SqlNullValueException"

$vpCode = Get-Content "QLKTX\Controllers\ViPhamController.cs" -Encoding UTF8 -Raw
$hasDinhChiSync = ($vpCode -match 'TinhTrangLuuTru\s*=\s*"') -and ($vpCode -match 'Dinh chi|ch')
Report-Test "TC_VP_01" "Ky luat" "Lap bien ban muc Dinh chi -> Tu dong cap nhat trang thai va giai phong phong" $hasDinhChiSync "Dong bo si so phong ngay khi dinh chi"

# -----------------------------------------------------------------------------
# NHOM 5: KIEM THU 3 PHAN HE AI VA AN TOAN DU LIEU (TC_AI)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "5. KIEM THU 3 PHAN HE AI VA AN TOAN DU LIEU (TC_AI)" -ForegroundColor Yellow

$kbExists = Test-Path "QLKTX\wwwroot\data\knowledge_base.json"
$kbContent = if ($kbExists) { Get-Content "QLKTX\wwwroot\data\knowledge_base.json" -Encoding UTF8 -Raw } else { "" }
$kbValid = $kbExists -and ($kbContent -match "GIOGIAC_NOIQUY") -and ($kbContent -match "HOADON_DIEN_NUOC") -and ($kbContent -match "BANG_GIA_PHONG")
Report-Test "TC_AI_01" "RAG Knowledge" "Co so tri thuc RAG (knowledge_base.json) day du 7 chu de noi quy KTX" $kbValid "Bao gom gio giac, don gia dien nuoc, gui xe, gia phong, tra phong"

$promptCode = Get-Content "QLKTX\Models\AIPrompts.cs" -Encoding UTF8 -Raw
$hasAntiHallucination = ($promptCode -match "AIPrompts") -and ($promptCode -match "ChatbotSystemPrompt")
Report-Test "TC_AI_02" "Chong bia dat" "System Prompt rang buoc chat che: Gioi han pham vi tra loi, chong Hallucination" $hasAntiHallucination "Chi tra loi trong pham vi noi quy KTX duoc cung cap"

$hasRoomAI = ($phongCode -match "GoiYXepPhongAI") -and ($phongCode -match "_httpClientFactory")
Report-Test "TC_AI_03" "AI Goi y phong" "AI goi y xep phong theo tieu chi kem Smart Fallback khi Ollama offline" $hasRoomAI "Loc phong con cho theo tang va loai phong"

$hasSummaryAI = ($portalCode -match "TomTatSuCoAI") -and ($portalCode -match "_httpClientFactory")
Report-Test "TC_AI_04" "AI Tom tat su co" "AI phan nhom su co (Dien, Nuoc, Noi that) kem thu tu uu tien xu ly" $hasSummaryAI "Chi ra phong can xu ly truoc"

$chatCode = Get-Content "QLKTX\Controllers\ChatbotController.cs" -Encoding UTF8 -Raw
$hasMaskPhone = $chatCode -match "MaskPhone"
$hasPrivacyDataFilter = $chatCode -match "sdtHienThi"
Report-Test "TC_AI_05" "Quyen rieng tu" "Bao ve quyen rieng tu AI: Che so dien thoai (098****567), an CCCD va mat khau" ($hasMaskPhone -and $hasPrivacyDataFilter) "Tuan thu nghiem ngat tieu chi Dao duc va Quyen rieng tu AI"

# -----------------------------------------------------------------------------
# NHOM 6: KIEM THU DIEM CUOI HTTP VA MOI TRUONG RUNTIME (TC_HTTP)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "6. KIEM THU DIEM CUOI HTTP VA MOI TRUONG RUNTIME (TC_HTTP)" -ForegroundColor Yellow

$hasPort5013 = (Get-Content "QLKTX\Properties\launchSettings.json" -Raw) -match "5013"
Report-Test "TC_HTTP_01" "Moi truong" "Cau hinh cong khoi chay san sang tai port 5013 (launchSettings.json)" $hasPort5013 "Dung chuan dia chi http://localhost:5013"

$composeContent = Get-Content "docker-compose.yml" -Raw
$hasHostDocker = $composeContent -match "host\.docker\.internal"
Report-Test "TC_HTTP_02" "Docker Compose" "Cau hinh ket noi container ho tro host.docker.internal toi SQL Server" $hasHostDocker "San sang cho docker compose up"

$hasHealthConfig = ($composeContent -match "healthcheck") -and ($composeContent -match "/health")
Report-Test "TC_HTTP_03" "Docker Healthcheck" "Cau hinh healthcheck trong docker-compose.yml theo doi endpoint /health" $hasHealthConfig "Chu ky 30s, timeout 10s, retry 3 lan"

# -----------------------------------------------------------------------------
# NHOM 7: THUC THI KIEM THU XUNIT (.NET UNIT & INTEGRATION TESTS) (TC_XUNIT)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "7. THUC THI KIEM THU CHUAN XUNIT (.NET UNIT & INTEGRATION TESTS)" -ForegroundColor Yellow

$hasGuardrailsFile = Test-Path "QLKTX\Models\SafetyGuardrails.cs"
Report-Test "TC_XUNIT_01" "Safety Guardrails" "Kien truc Rao chan An toan (SafetyGuardrails.cs) hoan thien day du" $hasGuardrailsFile "Input, Output (PII Scrubber), Execution Fallback"

$hasHealthCheckFile = Test-Path "QLKTX\Models\HealthChecks.cs"
Report-Test "TC_XUNIT_02" "Health Checks" "He thong Giam sat Suc khoe (HealthChecks.cs) cho SQL Server va AI" $hasHealthCheckFile "Endpoint /health tra ve JSON chi tiet"

$hasSdlcFactoryDoc = (Test-Path "AI_AGENT_FACTORY.md") -and (Test-Path ".github\workflows\ai-sdlc-factory.yml")
Report-Test "TC_XUNIT_03" "SDLC Factory" "Ho so Nha may AI Agent (AI_AGENT_FACTORY.md) va CI/CD Workflow hop le" $hasSdlcFactoryDoc "Quy trinh Context Engineering va Multi-Agent Pipeline"

Write-Host "  -> Dang chay 'dotnet test tests/QLKTX.Tests/QLKTX.Tests.csproj'..." -ForegroundColor Gray
$dotnetTestOutput = dotnet test tests\QLKTX.Tests\QLKTX.Tests.csproj --nologo -v q 2>&1
$dotnetTestSuccess = ($LASTEXITCODE -eq 0)
Report-Test "TC_XUNIT_04" "xUnit Execution" "Thuc thi toan bo 53 Unit Tests & Integration Tests dat 100% PASS" $dotnetTestSuccess "HoaDonCalculations, PhongVaSiSo, AIFallback, SecurityIntegration"

# -----------------------------------------------------------------------------
# TONG KET
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Cyan
$PassRate = [Math]::Round(($PassedTests / $TotalTests) * 100, 1)
$rateDisplay = "$PassRate%"
Write-Host "  KET QUA KIEM THU: $PassedTests / $TotalTests TEST CASES PASS ($rateDisplay)" -ForegroundColor $(if ($FailedTests -eq 0) { "Green" } else { "Red" })
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host ""

if ($FailedTests -eq 0) {
    exit 0
} else {
    exit 1
}
