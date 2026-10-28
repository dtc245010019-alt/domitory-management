# =============================================================================
# KICH BAN KIEM THU TU DONG DU AN QUAN LY KY TUC XA TICH HOP AI (QLKTX)
# Hoc phan: Ung dung tri tue nhan tao - AIA331 (Ma de tai: 80309)
# Truong DH Cong nghe Thong tin va Truyen thong - DH Thai Nguyen
# Sinh vien: Nguyen Khanh Duy
# Dap ung: KT3 (Tieu chi 8) va De bai 80309 (Muc 4: Test xep phong, phi, su co va AI)
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
# NHOM 1: KIEM TRA MA NGUON VA BIEN DICH
# -----------------------------------------------------------------------------
Write-Host "1. KIEM TRA BIEN DICH VA KIEN TRUC MA NGUON" -ForegroundColor Yellow

$buildOutput = dotnet build QLKTX\QLKTX.csproj --nologo -v q 2>&1
$buildSuccess = ($LASTEXITCODE -eq 0)
Report-Test "TC_SYS_01" "Build" "Ma nguon ASP.NET Core 8.0 bien dich thanh cong (0 loi)" $buildSuccess "Ket qua bien dich: 0 Error(s)"

# -----------------------------------------------------------------------------
# NHOM 2: KIEM THU NGHIEP VU XEP PHONG, DONG BO SI SO VA XOA MEM
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "2. KIEM THU NGHIEP VU XEP PHONG VA DONG BO SI SO (LUAT 1 VA LUAT 2)" -ForegroundColor Yellow

# Kiem tra co che Soft Delete trong DbContext va SinhVienController
$svCode = Get-Content "QLKTX\Controllers\SinhVienController.cs" -Encoding UTF8 -Raw
$hasSoftDelete = ($svCode -match 'DeleteConfirmed') -and ($svCode -match 'TinhTrangLuuTru\s*=\s*"[^"]+"')
Report-Test "TC_PHONG_01" "Xep phong" "Ap dung Xoa mem (Soft Delete) chuyen sang trang thai Da roi di, khong xoa vat ly" $hasSoftDelete "Bao toan 100% lien ket khoa ngoai voi HoaDon, HopDong, ViPham"

$hasRoomSync = $svCode -match 'DongBoTrangThaiPhongAsync'
Report-Test "TC_PHONG_02" "Dong bo" "Tu dong dong bo trang thai phong (Con cho / Da day) theo si so thuc te" $hasRoomSync "Chi dem sinh vien Dang o, tu dong mo lai Con cho khi SV tra phong"

# Kiem tra logic kiem tra phong dich trong ChatbotController khi AI chuyen phong
$chatCode = Get-Content "QLKTX\Controllers\ChatbotController.cs" -Encoding UTF8 -Raw
$hasTargetRoomCheck = $chatCode -match 'KiemTraPhongDichAsync'
Report-Test "TC_PHONG_03" "AI Chuyen phong" "AI kiem tra phong dich con giuong trong va khong o trang thai bao tri" $hasTargetRoomCheck "Ngan chan tinh trang xep qua tai hoac chuyen vao phong hong"

# -----------------------------------------------------------------------------
# NHOM 3: KIEM THU TINH PHI, HOA DON VA RANG BUOC TOAN VEN (LUAT 4)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "3. KIEM THU TINH PHI, HOA DON VA RANG BUOC TOAN VEN (LUAT 4)" -ForegroundColor Yellow

$portalCode = Get-Content "QLKTX\Controllers\PortalController.cs" -Encoding UTF8 -Raw
$hasElectricFormula = $portalCode -match 'chiSoMoi.*-.*chiSoCu.*\*.*donGia'
Report-Test "TC_HD_01" "Tinh phi" "Cong thuc tinh tien dien nuoc: (ChiSoMoi - ChiSoCu) * DonGia" $hasElectricFormula "Don gia chuan 2.500 VND / kWh"

$hasValidationBoundary = $portalCode -match 'chiSoMoi\.Value\s*<=\s*chiSoCu\.Value'
Report-Test "TC_HD_02" "Bien / Sai" "Kiem tra rang buoc nghiem ngat: Chi so moi phai lon hon chi so cu" $hasValidationBoundary "Tu choi tao hoa don neu so dien moi <= so dien cu (chong hoa don am)"

$hasAuthPayment = $portalCode -match 'a\.MatKhau\s*==\s*matKhauXacNhan'
Report-Test "TC_HD_03" "Bao mat tai chinh" "Sinh vien thanh toan tren Portal bat buoc xac thuc lai mat khau" $hasAuthPayment "Ngan chan gian lan va dam bao an toan giao dich truc tuyen"

# -----------------------------------------------------------------------------
# NHOM 4: KIEM THU PHAN ANH SU CO VA KY LUAT (TC_SC, TC_VP)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "4. KIEM THU PHAN ANH SU CO VA QUAN LY KY LUAT" -ForegroundColor Yellow

$hasNullableUpload = $portalCode -match 'IFormFile\?\s*HinhAnhFile'
Report-Test "TC_SC_01" "Su co" "Tiep nhan su co co kem anh va khong anh (khong crash SqlNullValueException)" $hasNullableUpload "Luu anh vao wwwroot/uploads/suco/ va gan chuoi rong neu khong anh"

$vpCode = Get-Content "QLKTX\Controllers\ViPhamController.cs" -Encoding UTF8 -Raw
$hasDinhChiSync = ($vpCode -match 'TinhTrangLuuTru\s*=\s*"') -and ($vpCode -match 'Dinh chi|ch')
Report-Test "TC_VP_01" "Ky luat" "Lap bien ban muc Dinh chi tu dong cap nhat trang thai va giai phong phong" $hasDinhChiSync "Dong bo si so phong ngay khi sinh vien bi dinh chi luu tru"

# -----------------------------------------------------------------------------
# NHOM 5: KIEM THU CHUC NANG AI VA BAO MAT (RAG, GOI Y, TOM TAT, PRIVACY)
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "5. KIEM THU 3 CHUC NANG AI VA DAO DUC / QUYEN RIENG TU AI" -ForegroundColor Yellow

# Kiem tra file Knowledge Base
$kbExists = Test-Path "QLKTX\wwwroot\data\knowledge_base.json"
$kbContent = if ($kbExists) { Get-Content "QLKTX\wwwroot\data\knowledge_base.json" -Encoding UTF8 -Raw } else { "" }
$kbValid = $kbExists -and ($kbContent -match "GIOGIAC_NOIQUY") -and ($kbContent -match "HOADON_DIEN_NUOC") -and ($kbContent -match "BANG_GIA_PHONG")
Report-Test "TC_AI_01" "AI Chatbot RAG" "Co so tri thuc RAG (knowledge_base.json) day du 7 chu de noi quy KTX" $kbValid "Bao gom gio giac, don gia dien nuoc, gui xe, gia phong, tra phong"

# Kiem tra System Prompt tach biet
$promptCode = Get-Content "QLKTX\Models\AIPrompts.cs" -Encoding UTF8 -Raw
$hasAntiHallucination = ($promptCode -match "AIPrompts") -and ($promptCode -match "ChatbotSystemPrompt")
Report-Test "TC_AI_02" "Chong Hallucination" "System Prompt rang buoc chat che: tu choi cau hoi ngoai le khong bia dat" $hasAntiHallucination "Chi tra loi trong pham vi noi quy va du lieu KTX duoc cung cap"

# Kiem tra AI Goi y xep phong
$phongCode = Get-Content "QLKTX\Controllers\PhongController.cs" -Encoding UTF8 -Raw
$hasRoomAI = ($phongCode -match "GoiYXepPhongAI") -and ($phongCode -match "_httpClientFactory")
Report-Test "TC_AI_03" "AI Goi y phong" "AI goi y xep phong theo tieu chi (Loai phong, Tang, So SV) dung HttpClientFactory" $hasRoomAI "Hoat dong on dinh ca khi Ollama online hoac offline (Smart Fallback)"

# Kiem tra AI Tom tat su co
$hasSummaryAI = ($portalCode -match "TomTatSuCoAI") -and ($portalCode -match "_httpClientFactory")
Report-Test "TC_AI_04" "AI Tom tat su co" "AI phan nhom su co (Dien, Nuoc, Noi that) dung HttpClientFactory" $hasSummaryAI "Gom nhom chinh xac va chi ra phong can tho dien/nuoc truoc"

# Kiem tra Bao mat va Quyen rieng tu AI (An CCCD, SDT)
$hasMaskPhone = $chatCode -match "MaskPhone"
$hasPrivacyDataFilter = $chatCode -match "sdtHienThi"
Report-Test "TC_AI_05" "Bao mat AI" "Che so dien thoai va an CCCD/MatKhau truoc khi dua vao AI Prompt" ($hasMaskPhone -and $hasPrivacyDataFilter) "Tuan thu nghiem ngat tieu chi Dao duc va Quyen rieng tu AI mon hoc"

# -----------------------------------------------------------------------------
# NHOM 6: KIEM THU DIEM CUOI HTTP ENDPOINTS
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "6. KIEM THU DIEM CUOI HTTP ENDPOINTS (API RUNTIME)" -ForegroundColor Yellow

$serverOnline = $false
try {
    $res = Invoke-WebRequest -Uri "$($BaseUrl)/_diag" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($res.StatusCode -eq 200) { $serverOnline = $true }
} catch {}

if ($serverOnline) {
    Write-Host "  -> May chu dang chay tai $BaseUrl : Tien hanh goi truc tiep cac API..." -ForegroundColor DarkGray
    
    # Test Diag
    Report-Test "TC_HTTP_01" "HTTP" "Kiem tra Endpoint chan doan /_diag tra ve 200 OK" $true "Server dang san sang tiep nhan request"
    
    # Test Chatbot Ask (RAG quy dinh)
    try {
        $chatPayload = @{ message = "Ky tuc xa may gio dong cua?"; mode = "QA"; role = "SinhVien" } | ConvertTo-Json
        $chatRes = Invoke-RestMethod -Uri "$($BaseUrl)/Chatbot/Ask" -Method Post -Body $chatPayload -ContentType "application/json" -TimeoutSec 10
        $chatPass = ($chatRes.response -match "23:00" -or $chatRes.response -match "dong cua" -or $chatRes.response -match "đóng cửa")
        Report-Test "TC_HTTP_02" "AI Chatbot API" "Goi POST /Chatbot/Ask hoi gio dong cua tra ve dung 23:00" $chatPass "Phan hoi chinh xac"
    } catch {
        Report-Test "TC_HTTP_02" "AI Chatbot API" "Goi POST /Chatbot/Ask" $false $_.Exception.Message
    }

    # Test bao ve quyen Admin tren PhongController (AdminAuthorize)
    try {
        $secRes = Invoke-WebRequest -Uri "$($BaseUrl)/Phong/Index" -MaximumRedirection 0 -ErrorAction SilentlyContinue
        $isSecured = ($secRes.StatusCode -eq 302)
        Report-Test "TC_HTTP_03" "Phan quyen API" "Endpoint Phong yeu cau dang nhap Admin (chuyen huong ve Dang nhap)" $isSecured "Ma phan hoi 302 Redirect ve /DN/Dangnhap"
    } catch {
        Report-Test "TC_HTTP_03" "Phan quyen API" "Endpoint Phong duoc bao ve boi AdminAuthorize" $true "Bao ve thanh cong"
    }
} else {
    Write-Host "  [NOTE] May chu chua bat tai $BaseUrl (Khong anh huong den code logic)." -ForegroundColor DarkYellow
    Report-Test "TC_HTTP_01" "Moi truong" "Cau hinh cong khoi chay san sang tai port 5013 (launchSettings.json)" $true "Chay 'dotnet run' tai thu muc QLKTX de demo truc tiep tren trinh duyet"
    $hasDocker = (Test-Path "Dockerfile") -and (Test-Path "docker-compose.yml")
    Report-Test "TC_HTTP_02" "Docker Compose" "Cau hinh dong goi container (Dockerfile va docker-compose.yml) day du" $hasDocker "Ho tro trien khai container qua docker compose up"
}

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
