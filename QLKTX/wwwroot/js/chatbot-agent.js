// ============================================================================
// TRỢ LÝ AI KTX - HYBRID RAG + AI ACTION AGENT (FUNCTION CALLING UI)
// ============================================================================

function formatAiMessageText(text) {
    if (!text) return '';
    let safe = escapeHtml(text);
    // Hỗ trợ in đậm **text**
    safe = safe.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    // Hỗ trợ in nghiêng *text*
    safe = safe.replace(/\*([^*\n]+)\*/g, '<em>$1</em>');
    return safe.replace(/\n/g, '<br />');
}

function renderQuickActionButton(quickAction) {
    if (!quickAction || !quickAction.label || !quickAction.prompt) return '';
    const safePrompt = escapeHtml(quickAction.prompt).replace(/'/g, "\\'");
    return `
        <div class="mt-2 pt-2 border-top">
            <button type="button" class="btn btn-sm btn-outline-primary rounded-pill w-100 fw-semibold py-1" style="font-size: 0.76rem;" onclick="sendQuickPrompt('${safePrompt}')">
                ${escapeHtml(quickAction.label)}
            </button>
        </div>
    `;
}

function renderReceiptHtml(r) {
    if (!r) return '';
    return `
        <div class="mt-2 p-2 rounded-3 border border-success bg-success-subtle text-dark" style="font-size: 0.78rem;">
            <div class="fw-bold text-success mb-1 d-flex align-items-center justify-content-between">
                <span><i class="bi bi-check-circle-fill me-1"></i>PHIẾU XÁC NHẬN HỆ THỐNG</span>
                <span class="badge bg-success text-white">${escapeHtml(r.maPhieu || '')}</span>
            </div>
            <div class="small lh-base">
                <div><strong>Loại tác vụ:</strong> ${escapeHtml(r.loaiPhieu || '')}</div>
                <div><strong>Phòng:</strong> <span class="badge bg-white text-dark border">${escapeHtml(r.maPhong || '')}</span></div>
                <div><strong>Sinh viên:</strong> ${escapeHtml(r.nguoiGui || '')}</div>
                <div><strong>Nội dung:</strong> ${escapeHtml(r.noiDung || '')}</div>
                ${r.chiTiet ? `<div class="text-muted">${escapeHtml(r.chiTiet)}</div>` : ''}
                <div><strong>Trạng thái:</strong> <span class="text-primary fw-semibold">${escapeHtml(r.trangThai || '')}</span> (${escapeHtml(r.thoiGian || '')})</div>
            </div>
            <button type="button" class="btn btn-sm btn-success w-100 mt-2 py-1 rounded-pill fw-semibold" style="font-size: 0.74rem;" onclick="window.location.reload()">
                <i class="bi bi-arrow-clockwise me-1"></i>Làm mới trang để xem dữ liệu vừa cập nhật
            </button>
        </div>
    `;
}

function renderActionCard(actionCard) {
    if (!actionCard || !actionCard.cardType) return '';
    const cardId = 'ac-' + Date.now() + '-' + Math.floor(Math.random() * 1000);

    // 1. RECEIPT CARD (Phiếu kết quả đã thực thi tự động)
    if (actionCard.cardType === 'RECEIPT_CARD') {
        return `<div id="${cardId}">${renderReceiptHtml(actionCard.receipt)}</div>`;
    }

    // 2. ACTION HUB (Bảng điều khiển các tác vụ làm hộ)
    if (actionCard.cardType === 'ACTION_HUB') {
        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border bg-light" style="font-size: 0.78rem;">
                <div class="fw-bold text-primary mb-2"><i class="bi bi-lightning-charge-fill me-1"></i>CHỌN TÁC VỤ ĐỂ AI THỰC HIỆN HỘ:</div>
                <div class="d-grid gap-1">
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Báo cáo sự cố hộ tôi')">
                        🛠 <strong>Báo hỏng / Báo cáo sự cố hộ</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Xin chuyển phòng hộ tôi')">
                        🔄 <strong>Xin chuyển / đổi phòng ở</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Đăng ký gia hạn hợp đồng hộ tôi')">
                        📅 <strong>Đăng ký gia hạn hợp đồng / lưu trú</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Đăng ký trả phòng hộ tôi')">
                        🚪 <strong>Đăng ký trả phòng KTX</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Thanh toán hóa đơn hộ tôi')">
                        💳 <strong>Tra cứu & Thanh toán hóa đơn</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Kiểm tra tiến độ đơn của tôi')">
                        📋 <strong>Kiểm tra tiến độ đơn / báo cáo</strong> <span class="text-muted float-end">→</span>
                    </button>
                </div>
            </div>
        `;
    }

    // 3. FORM BÁO CÁO SỰ CỐ TRỰC TIẾP TRONG CHAT
    if (actionCard.cardType === 'FORM_BAO_CAO_SU_CO') {
        const rooms = actionCard.rooms || [];
        const currentRoom = actionCard.currentMaPhong || (rooms.length > 0 ? rooms[0].maPhong : '101');
        const roomOptionsHtml = rooms.map(r =>
            `<option value="${escapeHtml(r.maPhong)}" ${r.maPhong === currentRoom ? 'selected' : ''}>P.${escapeHtml(r.maPhong)} (${escapeHtml(r.loaiPhong)})</option>`
        ).join('');

        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border border-danger-subtle bg-white shadow-sm" style="font-size: 0.78rem;">
                <div class="fw-bold text-danger mb-2 d-flex align-items-center justify-content-between">
                    <span><i class="bi bi-tools me-1"></i>TẠO BÁO CÁO SỰ CỐ NHANH</span>
                    <span class="badge bg-danger-subtle text-danger">AI Action</span>
                </div>
                <div class="mb-2">
                    <div class="text-muted mb-1" style="font-size: 0.7rem;">Chọn nhanh sự cố phổ biến (1-Click):</div>
                    <div class="d-flex flex-wrap gap-1">
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Cháy bóng đèn chiếu sáng', 'Bình thường', 'Đèn phòng bị cháy/nhấp nháy cần thay mới')">💡 Bóng đèn</button>
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Hỏng vòi nước / rò rỉ nước', 'Nghiêm trọng', 'Vòi nước trong phòng bị hỏng/rò rỉ nước')">🚰 Vòi nước</button>
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Sự cố điều hòa không mát', 'Bình thường', 'Điều hòa không hoạt động hoặc không mát')">❄️ Điều hòa</button>
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Hỏng quạt điện', 'Bình thường', 'Quạt trong phòng không quay hoặc kêu to')">🌀 Quạt</button>
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Sự cố ổ cắm / chập điện', 'Khẩn cấp', 'Ổ cắm điện có dấu hiệu chập/mất nguồn điện')">🔌 Ổ điện</button>
                        <button type="button" class="btn btn-xs btn-outline-secondary py-0 px-2 rounded-pill" style="font-size: 0.7rem;" onclick="fillIncidentChip('${cardId}', 'Hỏng khóa cửa phòng', 'Nghiêm trọng', 'Khóa cửa phòng bị kẹt/hỏng cần sửa chữa')">🚪 Khóa cửa</button>
                    </div>
                </div>
                <input type="hidden" id="${cardId}-masv" value="${escapeHtml(actionCard.currentMaSV || '')}" />
                <div class="row g-1 mb-1">
                    <div class="col-5">
                        <label class="form-label mb-0 small fw-semibold">Phòng:</label>
                        <select id="${cardId}-phong" class="form-select form-select-sm py-1" style="font-size: 0.75rem;">
                            ${roomOptionsHtml || `<option value="${escapeHtml(currentRoom)}">P.${escapeHtml(currentRoom)}</option>`}
                        </select>
                    </div>
                    <div class="col-7">
                        <label class="form-label mb-0 small fw-semibold">Mức độ:</label>
                        <select id="${cardId}-mucdo" class="form-select form-select-sm py-1" style="font-size: 0.75rem;">
                            <option value="Bình thường" ${actionCard.defaultMucDo === 'Bình thường' ? 'selected' : ''}>Bình thường</option>
                            <option value="Nghiêm trọng" ${actionCard.defaultMucDo === 'Nghiêm trọng' ? 'selected' : ''}>Nghiêm trọng</option>
                            <option value="Khẩn cấp" ${actionCard.defaultMucDo === 'Khẩn cấp' ? 'selected' : ''}>Khẩn cấp</option>
                        </select>
                    </div>
                </div>
                <div class="mb-1">
                    <label class="form-label mb-0 small fw-semibold">Tên sự cố:</label>
                    <input type="text" id="${cardId}-tensuco" class="form-control form-control-sm py-1" style="font-size: 0.75rem;" placeholder="VD: Hỏng vòi nước, cháy bóng đèn..." value="${escapeHtml(actionCard.defaultTenSuCo || '')}" />
                </div>
                <div class="mb-2">
                    <label class="form-label mb-0 small fw-semibold">Mô tả chi tiết:</label>
                    <input type="text" id="${cardId}-mota" class="form-control form-control-sm py-1" style="font-size: 0.75rem;" placeholder="Ghi chú thêm cho kỹ thuật viên..." value="${escapeHtml(actionCard.defaultMoTa || '')}" />
                </div>
                <div id="${cardId}-err" class="text-danger small mb-1 d-none"></div>
                <button type="button" id="${cardId}-btn" class="btn btn-sm btn-danger w-100 rounded-pill fw-semibold py-1" style="font-size: 0.76rem;" onclick="submitIncidentCard('${cardId}')">
                    <i class="bi bi-send-check-fill me-1"></i>Gửi báo cáo hộ tôi ngay
                </button>
            </div>
        `;
    }

    // 4. FORM YÊU CẦU PHÒNG (XIN CHUYỂN PHÒNG / ĐĂNG KÝ GIA HẠN / ĐĂNG KÝ TRẢ PHÒNG)
    if (actionCard.cardType === 'FORM_YEU_CAU_PHONG') {
        const rooms = actionCard.rooms || [];
        const availRooms = actionCard.availableRooms || [];
        const currentRoom = actionCard.currentMaPhong || (rooms.length > 0 ? rooms[0].maPhong : '101');
        const defaultLoai = actionCard.defaultLoaiYeuCau || 'Xin chuyển phòng';
        const showTarget = defaultLoai === 'Xin chuyển phòng';

        const roomOptionsHtml = rooms.map(r =>
            `<option value="${escapeHtml(r.maPhong)}" ${r.maPhong === currentRoom ? 'selected' : ''}>P.${escapeHtml(r.maPhong)} (${escapeHtml(r.loaiPhong)})</option>`
        ).join('');

        const availOptionsHtml = availRooms.map(r =>
            `<option value="${escapeHtml(r.maPhong)}" ${r.maPhong === actionCard.defaultPhongChuyenDen ? 'selected' : ''}>P.${escapeHtml(r.maPhong)} - ${escapeHtml(r.loaiPhong)} (${Number(r.giaPhong).toLocaleString('vi-VN')}đ)</option>`
        ).join('');

        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border border-primary-subtle bg-white shadow-sm" style="font-size: 0.78rem;">
                <div class="fw-bold text-primary mb-2 d-flex align-items-center justify-content-between">
                    <span><i class="bi bi-building-gear me-1"></i>ĐƠN YÊU CẦU LƯU TRÚ NHANH</span>
                    <span class="badge bg-primary-subtle text-primary">AI Action</span>
                </div>
                <input type="hidden" id="${cardId}-masv" value="${escapeHtml(actionCard.currentMaSV || '')}" />
                <div class="mb-1">
                    <label class="form-label mb-0 small fw-semibold">Loại yêu cầu:</label>
                    <select id="${cardId}-loai" class="form-select form-select-sm py-1" style="font-size: 0.75rem;" onchange="toggleTargetRoomField('${cardId}')">
                        <option value="Xin chuyển phòng" ${defaultLoai === 'Xin chuyển phòng' ? 'selected' : ''}>Xin chuyển phòng</option>
                        <option value="Đăng ký gia hạn ở" ${defaultLoai === 'Đăng ký gia hạn ở' ? 'selected' : ''}>Đăng ký gia hạn ở</option>
                        <option value="Đăng ký trả phòng" ${defaultLoai === 'Đăng ký trả phòng' ? 'selected' : ''}>Đăng ký trả phòng</option>
                    </select>
                </div>
                <div class="mb-1">
                    <label class="form-label mb-0 small fw-semibold">Phòng hiện tại:</label>
                    <select id="${cardId}-phong" class="form-select form-select-sm py-1" style="font-size: 0.75rem;">
                        ${roomOptionsHtml || `<option value="${escapeHtml(currentRoom)}">P.${escapeHtml(currentRoom)}</option>`}
                    </select>
                </div>
                <div class="mb-1 ${showTarget ? '' : 'd-none'}" id="${cardId}-target-group">
                    <label class="form-label mb-0 small fw-semibold">Phòng muốn chuyển đến (Còn chỗ):</label>
                    <select id="${cardId}-phongden" class="form-select form-select-sm py-1" style="font-size: 0.75rem;">
                        ${availOptionsHtml}
                    </select>
                </div>
                <div class="mb-2">
                    <label class="form-label mb-0 small fw-semibold">Lý do gửi yêu cầu:</label>
                    <input type="text" id="${cardId}-lydo" class="form-control form-control-sm py-1" style="font-size: 0.75rem;" placeholder="Nhập lý do..." value="${escapeHtml(actionCard.defaultLyDo || 'Nguyện vọng cá nhân (Gửi qua Trợ lý AI)')}" />
                </div>
                <div id="${cardId}-err" class="text-danger small mb-1 d-none"></div>
                <button type="button" id="${cardId}-btn" class="btn btn-sm btn-primary w-100 rounded-pill fw-semibold py-1" style="font-size: 0.76rem;" onclick="submitRoomRequestCard('${cardId}')">
                    <i class="bi bi-send-check-fill me-1"></i>Gửi yêu cầu hộ tôi ngay
                </button>
            </div>
        `;
    }

    // 5. FORM THANH TOÁN HÓA ĐƠN TRỰC TIẾP TRONG CHAT
    if (actionCard.cardType === 'FORM_THANH_TOAN') {
        const invoices = actionCard.invoices || [];
        if (invoices.length === 0) {
            return `<div class="mt-2 p-2 rounded-3 border bg-light small text-muted">Không có hóa đơn nào cần hiển thị.</div>`;
        }

        const rowsHtml = invoices.map(inv => {
            const isPaid = inv.trangThai === 'Đã thanh toán';
            const amt = Number(inv.tongTien || 0).toLocaleString('vi-VN') + 'đ';
            return `
                <div class="p-2 mb-1 rounded-2 border ${isPaid ? 'bg-light' : 'bg-white border-warning'}" id="${cardId}-inv-${escapeHtml(inv.maHoaDon)}">
                    <div class="d-flex justify-content-between align-items-center">
                        <strong>${escapeHtml(inv.loaiHoaDon)} (P.${escapeHtml(inv.maPhong)})</strong>
                        <span class="fw-bold ${isPaid ? 'text-success' : 'text-danger'}">${amt}</span>
                    </div>
                    <div class="text-muted" style="font-size: 0.7rem;">Mã: ${escapeHtml(inv.maHoaDon)} | SV: ${escapeHtml(inv.maSV)} | Ngày: ${escapeHtml(inv.ngayLap)}</div>
                    ${isPaid
                        ? `<div class="mt-1"><span class="badge bg-success-subtle text-success border border-success-subtle">Đã thanh toán</span></div>`
                        : `<div class="d-flex gap-1 mt-1">
                               <select id="${cardId}-pt-${escapeHtml(inv.maHoaDon)}" class="form-select form-select-sm py-0" style="font-size: 0.72rem;">
                                   <option value="Ví MoMo (Qua Trợ lý AI)">Ví MoMo</option>
                                   <option value="VNPAY-QR (Qua Trợ lý AI)">VNPAY-QR</option>
                                   <option value="Chuyển khoản NH (Qua Trợ lý AI)">Chuyển khoản</option>
                               </select>
                               <button type="button" class="btn btn-sm btn-warning text-dark fw-semibold py-0 px-2 text-nowrap" style="font-size: 0.72rem;" onclick="submitInvoicePayment('${cardId}', '${escapeHtml(inv.maHoaDon)}')">
                                   💳 Thanh toán
                               </button>
                           </div>`
                    }
                </div>
            `;
        }).join('');

        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border bg-light" style="font-size: 0.78rem;">
                <div class="fw-bold text-dark mb-1"><i class="bi bi-receipt me-1 text-primary"></i>DANH SÁCH HÓA ĐƠN</div>
                ${rowsHtml}
            </div>
        `;
    }

    return '';
}

function fillIncidentChip(cardId, title, urgency, desc) {
    const titleEl = document.getElementById(cardId + '-tensuco');
    const urgEl = document.getElementById(cardId + '-mucdo');
    const descEl = document.getElementById(cardId + '-mota');
    if (titleEl) titleEl.value = title;
    if (urgEl) urgEl.value = urgency;
    if (descEl) descEl.value = desc;
}

function toggleTargetRoomField(cardId) {
    const loaiEl = document.getElementById(cardId + '-loai');
    const targetGroup = document.getElementById(cardId + '-target-group');
    if (!loaiEl || !targetGroup) return;
    if (loaiEl.value === 'Xin chuyển phòng') {
        targetGroup.classList.remove('d-none');
    } else {
        targetGroup.classList.add('d-none');
    }
}

async function submitIncidentCard(cardId) {
    const maSV = document.getElementById(cardId + '-masv')?.value || '';
    const maPhong = document.getElementById(cardId + '-phong')?.value || '';
    const mucDo = document.getElementById(cardId + '-mucdo')?.value || 'Bình thường';
    const tenSuCo = document.getElementById(cardId + '-tensuco')?.value.trim() || '';
    const moTa = document.getElementById(cardId + '-mota')?.value.trim() || '';
    const errEl = document.getElementById(cardId + '-err');
    const btnEl = document.getElementById(cardId + '-btn');

    if (!tenSuCo) {
        if (errEl) {
            errEl.textContent = 'Vui lòng chọn nhanh 1 sự cố phía trên hoặc nhập tên sự cố!';
            errEl.classList.remove('d-none');
        }
        return;
    }

    if (btnEl) {
        btnEl.disabled = true;
        btnEl.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang gửi lên hệ thống...';
    }

    try {
        const res = await fetch('/Chatbot/ExecuteAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'TAO_BAO_CAO_SU_CO',
                maSV: maSV,
                maPhong: maPhong,
                tenSuCo: tenSuCo,
                mucDoKhanCap: mucDo,
                moTa: moTa || tenSuCo
            })
        });
        const data = await res.json();
        const container = document.getElementById(cardId);
        if (data.success && container) {
            container.innerHTML = renderReceiptHtml(data.receipt);
        } else if (errEl) {
            errEl.textContent = data.message || 'Không thể thực hiện thao tác.';
            errEl.classList.remove('d-none');
            if (btnEl) {
                btnEl.disabled = false;
                btnEl.innerHTML = '<i class="bi bi-send-check-fill me-1"></i>Gửi báo cáo hộ tôi ngay';
            }
        }
    } catch (e) {
        if (errEl) {
            errEl.textContent = 'Lỗi kết nối: ' + e.message;
            errEl.classList.remove('d-none');
        }
        if (btnEl) {
            btnEl.disabled = false;
            btnEl.innerHTML = '<i class="bi bi-send-check-fill me-1"></i>Gửi báo cáo hộ tôi ngay';
        }
    }
}

async function submitRoomRequestCard(cardId) {
    const maSV = document.getElementById(cardId + '-masv')?.value || '';
    const loaiYeuCau = document.getElementById(cardId + '-loai')?.value || 'Xin chuyển phòng';
    const maPhong = document.getElementById(cardId + '-phong')?.value || '';
    const phongChuyenDen = document.getElementById(cardId + '-phongden')?.value || '';
    const lyDo = document.getElementById(cardId + '-lydo')?.value.trim() || '';
    const errEl = document.getElementById(cardId + '-err');
    const btnEl = document.getElementById(cardId + '-btn');

    if (btnEl) {
        btnEl.disabled = true;
        btnEl.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Đang gửi đơn...';
    }

    try {
        const res = await fetch('/Chatbot/ExecuteAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'GUI_YEU_CAU_PHONG',
                maSV: maSV,
                maPhong: maPhong,
                loaiYeuCau: loaiYeuCau,
                phongChuyenDen: phongChuyenDen,
                lyDo: lyDo
            })
        });
        const data = await res.json();
        const container = document.getElementById(cardId);
        if (data.success && container) {
            container.innerHTML = renderReceiptHtml(data.receipt);
        } else if (errEl) {
            errEl.textContent = data.message || 'Không thể gửi yêu cầu.';
            errEl.classList.remove('d-none');
            if (btnEl) {
                btnEl.disabled = false;
                btnEl.innerHTML = '<i class="bi bi-send-check-fill me-1"></i>Gửi yêu cầu hộ tôi ngay';
            }
        }
    } catch (e) {
        if (errEl) {
            errEl.textContent = 'Lỗi kết nối: ' + e.message;
            errEl.classList.remove('d-none');
        }
        if (btnEl) {
            btnEl.disabled = false;
            btnEl.innerHTML = '<i class="bi bi-send-check-fill me-1"></i>Gửi yêu cầu hộ tôi ngay';
        }
    }
}

async function submitInvoicePayment(cardId, maHoaDon) {
    const ptEl = document.getElementById(`${cardId}-pt-${maHoaDon}`);
    const phuongThuc = ptEl ? ptEl.value : 'Ví MoMo (Qua Trợ lý AI)';
    const invRow = document.getElementById(`${cardId}-inv-${maHoaDon}`);

    try {
        const res = await fetch('/Chatbot/ExecuteAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'THANH_TOAN_HOA_DON',
                maHoaDon: maHoaDon,
                phuongThuc: phuongThuc
            })
        });
        const data = await res.json();
        if (data.success && invRow) {
            invRow.innerHTML = renderReceiptHtml(data.receipt);
        } else {
            alert(data.message || 'Thanh toán thất bại.');
        }
    } catch (e) {
        alert('Lỗi kết nối: ' + e.message);
    }
}

