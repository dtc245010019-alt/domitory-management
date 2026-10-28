// ============================================================================
// TRỢ LÝ AI KTX - 2 CHẾ ĐỘ: NHỜ AI LÀM HỘ (ACTION) & HỎI ĐÁP TRA CỨU (QA)
// ============================================================================

window.currentChatMode = 'QA'; // Mặc định hoặc chuyển đổi linh hoạt giữa 'ACTION' và 'QA'

function switchChatMode(mode, showGuideBubble = true) {
    const normalizedMode = (mode === 'ACTION') ? 'ACTION' : 'QA';
    window.currentChatMode = normalizedMode;
    const isAdmin = (window.chatUserRole === 'Admin');

    const btnAction = document.getElementById('mode-btn-action');
    const btnQa = document.getElementById('mode-btn-qa');
    const banner = document.getElementById('chat-mode-banner');
    const chipsContainer = document.getElementById('chat-quick-chips');
    const chatInput = document.getElementById('chat-input');
    const chatMessages = document.getElementById('chat-messages');

    if (normalizedMode === 'ACTION') {
        if (btnAction) {
            btnAction.className = 'btn btn-sm flex-fill fw-bold py-1 px-2 rounded-pill shadow-sm text-white';
            btnAction.style.background = isAdmin
                ? 'linear-gradient(135deg, #dc2626, #b91c1c)'
                : 'linear-gradient(135deg, #4f46e5, #7c3aed)';
            btnAction.style.fontSize = '0.76rem';
        }
        if (btnQa) {
            btnQa.className = 'btn btn-sm flex-fill fw-semibold py-1 px-2 rounded-pill text-secondary bg-white border';
            btnQa.style.background = '';
            btnQa.style.fontSize = '0.76rem';
        }
        if (banner) {
            banner.className = 'px-3 py-1 border-bottom d-flex align-items-center justify-content-between';
            banner.style.background = isAdmin ? '#fef2f2' : '#f5f3ff';
            banner.style.color = isAdmin ? '#991b1b' : '#5b21b6';
            banner.style.fontSize = '0.72rem';
            banner.innerHTML = isAdmin
                ? `<span>⚡ <strong>Chế độ Quản trị Thực thi:</strong> Nhờ AI hỗ trợ duyệt sự cố, xếp phòng, nhắc nợ...</span>`
                : `<span>⚡ <strong>Chế độ Nhờ AI làm hộ:</strong> Gõ lệnh để AI tự thực thi (VD: <em>"gia hạn phòng cho tôi"</em>)</span>`;
        }
        if (chatInput) {
            chatInput.placeholder = isAdmin
                ? 'Gõ thao tác quản trị (VD: duyệt sự cố SC_..., nhắc nợ phòng)...'
                : 'Gõ việc nhờ AI làm hộ (VD: gia hạn phòng cho tôi)...';
        }
        if (chipsContainer) {
            chipsContainer.innerHTML = isAdmin ? `
                <button type="button" class="btn btn-sm btn-outline-danger py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Duyệt sự cố kỹ thuật đang chờ xử lý', 'ACTION')">🚨 Duyệt sự cố hỏng hóc</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Gợi ý xếp phòng còn chỗ trống', 'ACTION')">🚪 Gợi ý xếp phòng</button>
                <button type="button" class="btn btn-sm btn-outline-warning py-1 px-2 text-nowrap fw-semibold text-dark" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Lập thông báo nhắc nợ tiền KTX', 'ACTION')">🧾 Nhắc nộp tiền KTX</button>
                <button type="button" class="btn btn-sm btn-outline-success py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Xuất tóm tắt báo cáo KTX', 'ACTION')">📊 Tóm tắt điều hành KTX</button>
            ` : `
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Gia hạn phòng cho tôi', 'ACTION')">📅 Gia hạn phòng cho tôi</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Tôi còn hóa đơn nào chưa thanh toán?', 'QA')">🧾 Hóa đơn chưa trả?</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Báo hỏng bóng đèn phòng tôi', 'ACTION')">🛠 Báo hỏng bóng đèn</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Xin chuyển phòng cho tôi', 'ACTION')">🔄 Xin chuyển phòng</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Đăng ký trả phòng cho tôi', 'ACTION')">🚪 Đăng ký trả phòng</button>
                <button type="button" class="btn btn-sm btn-outline-primary py-1 px-2 text-nowrap fw-semibold" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Xin cấp thêm ghế cho phòng tôi', 'ACTION')">🛋 Yêu cầu thiết bị</button>
            `;
        }
        if (showGuideBubble && chatMessages) {
            chatMessages.innerHTML += `
                <div class="msg-row-ai">
                    <div class="msg-bubble-ai border-start border-4 ${isAdmin ? 'border-danger' : 'border-primary'}">
                        ⚡ <strong>${isAdmin ? 'Đã bật chế độ: Thao tác Quản trị (Admin Action Agent)!' : 'Đã bật chế độ: Nhờ AI làm hộ!'}</strong><br />
                        ${isAdmin
                            ? 'Bạn có thể yêu cầu AI hỗ trợ xử lý nhanh các tác vụ quản trị:<br />• <em>"Duyệt sự cố kỹ thuật"</em> → Xem và cập nhật trạng thái sự cố sang Đã xử lý<br />• <em>"Gợi ý xếp phòng còn trống"</em> → Tìm và tư vấn phòng tối ưu<br />• <em>"Lập thông báo nhắc nợ tiền KTX"</em> → Liệt kê công nợ và soạn mẫu nhắc nhở<br />• <em>"Xuất tóm tắt báo cáo KTX"</em> → Tổng hợp số liệu vận hành'
                            : 'Bạn chỉ cần gõ yêu cầu hoặc bấm gợi ý bên dưới, tôi sẽ <strong>tự động thực hiện ngay trên hệ thống</strong>:<br />• <em>"Gia hạn phòng cho tôi"</em> → Tự động gia hạn hợp đồng<br />• <em>"Báo hỏng điều hòa phòng tôi"</em> → Tự động lập phiếu sửa chữa<br />• <em>"Chuyển sang phòng 102 cho tôi"</em> / <em>"Đăng ký trả phòng"</em><br />• <em>"Tôi còn hóa đơn nào chưa trả?"</em> → Tra cứu hóa đơn & hướng dẫn tự thanh toán an toàn'
                        }
                    </div>
                </div>
            `;
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
    } else {
        if (btnQa) {
            btnQa.className = 'btn btn-sm flex-fill fw-bold py-1 px-2 rounded-pill shadow-sm text-white';
            btnQa.style.background = 'linear-gradient(135deg, #0284c7, #2563eb)';
            btnQa.style.fontSize = '0.76rem';
        }
        if (btnAction) {
            btnAction.className = 'btn btn-sm flex-fill fw-semibold py-1 px-2 rounded-pill text-secondary bg-white border';
            btnAction.style.background = '';
            btnAction.style.fontSize = '0.76rem';
        }
        if (banner) {
            banner.className = 'px-3 py-1 border-bottom d-flex align-items-center justify-content-between';
            banner.style.background = '#eff6ff';
            banner.style.color = '#1d4ed8';
            banner.style.fontSize = '0.72rem';
            banner.innerHTML = isAdmin
                ? `<span>💬 <strong>Chế độ Hỏi đáp Quản trị:</strong> Đặt câu hỏi để AI tra cứu &amp; thống kê số liệu KTX</span>`
                : `<span>💬 <strong>Chế độ Hỏi đáp:</strong> Đặt câu hỏi để AI tra cứu &amp; liệt kê chi tiết</span>`;
        }
        if (chatInput) {
            chatInput.placeholder = isAdmin
                ? 'Nhập câu hỏi quản trị (VD: thống kê phòng còn trống, công nợ hóa đơn)...'
                : 'Nhập câu hỏi (VD: tôi còn hóa đơn nào chưa thanh toán?)...';
        }
        if (chipsContainer) {
            chipsContainer.innerHTML = isAdmin ? `
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Báo cáo thống kê tổng quan KTX', 'QA')">📊 Thống kê KTX</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Tình trạng phòng và các phòng còn chỗ trống?', 'QA')">🚪 Phòng còn trống?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Danh sách hóa đơn chưa thanh toán và tổng công nợ?', 'QA')">🧾 Công nợ & Hóa đơn tồn?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Danh sách sự cố hư hỏng đang chờ xử lý?', 'QA')">🚨 Sự cố chờ xử lý?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Danh sách hợp đồng sắp hết hạn trong 30 ngày?', 'QA')">📄 Hợp đồng sắp hết hạn?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Danh sách sinh viên vi phạm kỷ luật?', 'QA')">⚠️ Kỷ luật sinh viên?</button>
            ` : `
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Tôi còn hóa đơn nào chưa thanh toán?', 'QA')">🧾 Hóa đơn chưa thanh toán?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Hợp đồng của tôi khi nào hết hạn?', 'QA')">📄 Hạn hợp đồng của tôi?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Phòng tôi hiện có những ai?', 'QA')">👥 Bạn cùng phòng?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Sự cố của tôi đã được xử lý chưa?', 'QA')">🔧 Tiến độ sửa sự cố?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Phòng tôi có những cơ sở vật chất nào?', 'QA')">🛋 Thiết bị phòng tôi?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Tôi có bị vi phạm kỷ luật nào không?', 'QA')">⚠️ Kiểm tra vi phạm?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Còn phòng nào trống không?', 'QA')">🏢 Phòng còn trống?</button>
                <button type="button" class="btn btn-sm btn-light border py-1 px-2 text-nowrap" style="font-size: 0.71rem; border-radius: 20px;" onclick="sendQuickPrompt('Quy định giờ giấc ký túc xá?', 'QA')">⏰ Giờ giới nghiêm?</button>
            `;
        }
        if (showGuideBubble && chatMessages) {
            chatMessages.innerHTML += `
                <div class="msg-row-ai">
                    <div class="msg-bubble-ai border-start border-4 border-info">
                        💬 <strong>${isAdmin ? 'Đã bật chế độ: Hỏi đáp & Thống kê Quản trị KTX!' : 'Đã bật chế độ: Hỏi đáp & Tra cứu dữ liệu!'}</strong><br />
                        ${isAdmin
                            ? 'Bạn có thể hỏi bất kỳ thông tin điều hành nào, AI sẽ tra cứu CSDL và <strong>báo cáo chi tiết</strong>:<br />• <em>"Báo cáo thống kê tổng quan KTX"</em><br />• <em>"Những phòng nào còn chỗ trống?"</em><br />• <em>"Danh sách hóa đơn chưa thanh toán và tổng công nợ?"</em><br />• <em>"Hợp đồng sắp hết hạn trong 30 ngày?"</em>'
                            : 'Bạn có thể hỏi bất kỳ thông tin nào, tôi sẽ tra cứu CSDL và <strong>liệt kê chi tiết</strong> cho bạn:<br />• <em>"Tôi còn hóa đơn nào chưa thanh toán?"</em><br />• <em>"Hợp đồng của tôi khi nào hết hạn?"</em><br />• <em>"Phòng tôi có những ai?"</em> / <em>"Phòng nào còn trống?"</em><br />• <em>"Phòng tôi có thiết bị gì?"</em> / <em>"Tôi có vi phạm nào không?"</em>'
                        }
                    </div>
                </div>
            `;
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
    }

    if (chatInput && showGuideBubble) {
        setTimeout(() => chatInput.focus(), 80);
    }
}

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
    const targetMode = quickAction.mode || 'ACTION';
    return `
        <div class="mt-2 pt-2 border-top">
            <button type="button" class="btn btn-sm btn-outline-primary rounded-pill w-100 fw-semibold py-1" style="font-size: 0.76rem;" onclick="sendQuickPrompt('${safePrompt}', '${targetMode}')">
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

    // 2. ACTION HUB (Bảng điều khiển các tác vụ làm hộ Sinh viên)
    if (actionCard.cardType === 'ACTION_HUB') {
        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border bg-light" style="font-size: 0.78rem;">
                <div class="fw-bold text-primary mb-2"><i class="bi bi-lightning-charge-fill me-1"></i>CHỌN TÁC VỤ ĐỂ AI THỰC HIỆN HỘ:</div>
                <div class="d-grid gap-1">
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Gia hạn phòng cho tôi', 'ACTION')">
                        📅 <strong>Tự động gia hạn phòng / hợp đồng (+6 tháng)</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Tôi còn hóa đơn nào chưa thanh toán?', 'QA')">
                        🧾 <strong>Tra cứu hóa đơn &amp; Tự thanh toán an toàn</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Báo cáo sự cố hộ tôi', 'ACTION')">
                        🛠 <strong>Báo hỏng / Báo cáo sự cố thiết bị</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Xin chuyển phòng cho tôi', 'ACTION')">
                        🔄 <strong>Xin chuyển / đổi phòng ở</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Đăng ký trả phòng cho tôi', 'ACTION')">
                        🚪 <strong>Đăng ký trả phòng KTX</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-white" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Xin cấp thêm ghế cho phòng tôi', 'ACTION')">
                        🛋 <strong>Yêu cầu cấp phát / bổ sung thiết bị</strong> <span class="text-muted float-end">→</span>
                    </button>
                </div>
            </div>
        `;
    }

    // 2B. ADMIN ACTION HUB (Bảng điều hành các tác vụ Quản trị KTX)
    if (actionCard.cardType === 'ADMIN_ACTION_HUB') {
        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border border-danger-subtle bg-white shadow-sm" style="font-size: 0.78rem;">
                <div class="fw-bold text-danger mb-2 d-flex align-items-center justify-content-between">
                    <span><i class="bi bi-shield-lock-fill me-1"></i>BẢNG ĐIỀU HÀNH TÁC VỤ QUẢN TRỊ (ADMIN)</span>
                    <span class="badge bg-danger text-white">BQL KTX</span>
                </div>
                <div class="d-grid gap-1">
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-light" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Duyệt sự cố kỹ thuật đang chờ xử lý', 'ACTION')">
                        🚨 <strong>Phê duyệt &amp; cập nhật sự cố thiết bị</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-light" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Gợi ý xếp phòng còn chỗ trống', 'ACTION')">
                        🚪 <strong>Kiểm tra &amp; Gợi ý xếp phòng trống tối ưu</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-light" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Lập thông báo nhắc nợ tiền KTX', 'ACTION')">
                        🧾 <strong>Rà soát công nợ &amp; Soạn mẫu nhắc nộp tiền</strong> <span class="text-muted float-end">→</span>
                    </button>
                    <button type="button" class="btn btn-sm btn-white border text-start py-1 px-2 rounded-2 shadow-sm bg-light" style="font-size: 0.76rem;" onclick="sendQuickPrompt('Xuất tóm tắt báo cáo KTX', 'ACTION')">
                        📊 <strong>Xuất báo cáo tổng hợp tình hình vận hành KTX</strong> <span class="text-muted float-end">→</span>
                    </button>
                </div>
            </div>
        `;
    }

    // 2C. ADMIN SU CO LIST (Duyệt cập nhật sự cố trực tiếp cho Admin)
    if (actionCard.cardType === 'ADMIN_SU_CO_LIST') {
        const incidents = actionCard.incidents || [];
        if (incidents.length === 0) {
            return `<div class="mt-2 p-2 rounded-3 border bg-light text-muted small">Hiện không có sự cố nào đang chờ xử lý.</div>`;
        }
        const rowsHtml = incidents.map(sc => {
            const isResolved = sc.trangThai === 'Đã xử lý';
            return `
                <div class="p-2 mb-1 rounded-2 border ${isResolved ? 'bg-light' : 'bg-white border-danger-subtle'}" id="${cardId}-sc-${escapeHtml(sc.maSuCo)}">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <strong class="text-danger">${escapeHtml(sc.tenSuCo)} (P.${escapeHtml(sc.maPhong)})</strong>
                        <span class="badge ${sc.mucDoKhanCap === 'Khẩn cấp' ? 'bg-danger' : (sc.mucDoKhanCap === 'Nghiêm trọng' ? 'bg-warning text-dark' : 'bg-secondary')}">${escapeHtml(sc.mucDoKhanCap)}</span>
                    </div>
                    <div class="text-muted mb-1" style="font-size: 0.7rem;">Mã: <strong>${escapeHtml(sc.maSuCo)}</strong> | Báo cáo lúc: ${escapeHtml(sc.ngayBaoCao)} | Trạng thái: <em>${escapeHtml(sc.trangThai)}</em></div>
                    ${sc.moTa ? `<div class="small text-secondary mb-1">Mô tả: ${escapeHtml(sc.moTa)}</div>` : ''}
                    ${isResolved
                        ? `<span class="badge bg-success-subtle text-success border">Đã xử lý xong</span>`
                        : `<div class="d-flex gap-1 mt-1">
                               <button type="button" class="btn btn-sm btn-success py-0 px-2 fw-semibold" style="font-size: 0.72rem;" onclick="submitAdminUpdateIncident('${cardId}', '${escapeHtml(sc.maSuCo)}', 'Đã xử lý')">
                                   ✅ Đánh dấu Đã xử lý
                               </button>
                               <button type="button" class="btn btn-sm btn-outline-warning text-dark py-0 px-2 fw-semibold" style="font-size: 0.72rem;" onclick="submitAdminUpdateIncident('${cardId}', '${escapeHtml(sc.maSuCo)}', 'Đang xử lý')">
                                   ⏳ Đang sửa chữa
                               </button>
                           </div>`
                    }
                </div>
            `;
        }).join('');

        return `
            <div id="${cardId}" class="mt-2 p-2 rounded-3 border border-danger-subtle bg-light" style="font-size: 0.78rem;">
                <div class="fw-bold text-danger mb-2 d-flex justify-content-between align-items-center">
                    <span><i class="bi bi-tools me-1"></i>DUYỆT TIẾN ĐỘ SỰ CỐ KỸ THUẬT (1-CLICK)</span>
                    <span class="badge bg-danger text-white">${incidents.length} sự cố</span>
                </div>
                ${rowsHtml}
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

        const isAdmin = (window.chatUserRole === 'Admin');

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
                        : (isAdmin
                            ? `<div class="d-flex gap-1 mt-1">
                                   <select id="${cardId}-pt-${escapeHtml(inv.maHoaDon)}" class="form-select form-select-sm py-0" style="font-size: 0.72rem;">
                                       <option value="Tiền mặt (Admin thu trực tiếp)">Tiền mặt (Admin thu trực tiếp)</option>
                                       <option value="Chuyển khoản (Admin xác nhận)">Chuyển khoản (Admin xác nhận)</option>
                                   </select>
                                   <button type="button" class="btn btn-sm btn-danger text-white fw-semibold py-0 px-2 text-nowrap" style="font-size: 0.72rem;" onclick="submitInvoicePayment('${cardId}', '${escapeHtml(inv.maHoaDon)}')">
                                       ✅ Admin xác nhận thu
                                   </button>
                               </div>`
                            : `<div class="mt-2 p-2 bg-light rounded border border-warning-subtle text-dark" style="font-size:0.72rem;">
                                   <div class="text-secondary mb-1"><i class="bi bi-shield-lock-fill text-warning me-1"></i><strong>Quy tắc bảo mật tài chính:</strong> Trợ lý AI không được phép tự động trừ tiền/gạch nợ. Sinh viên vui lòng tự thanh toán tại Cổng Sinh viên (yêu cầu mật khẩu xác nhận).</div>
                                   <a href="/Portal/HoaDon" class="btn btn-sm btn-primary py-1 px-2 fw-semibold w-100" style="font-size:0.74rem;">
                                       <i class="bi bi-wallet2 me-1"></i>Đến Cổng "Hóa đơn của tôi" để thanh toán
                                   </a>
                               </div>`
                          )
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
    const isAdmin = (window.chatUserRole === 'Admin');
    if (!isAdmin) {
        alert("Vì lý do an toàn tài chính, AI không được phép tự động thanh toán hóa đơn. Vui lòng tự thực hiện thanh toán tại trang 'Hóa đơn của tôi' (yêu cầu mật khẩu xác thực) hoặc đến gặp trực tiếp Ban Quản lý để được Admin xác nhận thu tiền.");
        window.location.href = '/Portal/HoaDon';
        return;
    }

    const ptEl = document.getElementById(`${cardId}-pt-${maHoaDon}`);
    const phuongThuc = ptEl ? ptEl.value : 'Tiền mặt (Admin thu trực tiếp)';
    const invRow = document.getElementById(`${cardId}-inv-${maHoaDon}`);

    try {
        const res = await fetch('/Chatbot/ExecuteAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'THANH_TOAN_HOA_DON',
                maHoaDon: maHoaDon,
                phuongThuc: phuongThuc,
                role: 'Admin'
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

async function submitAdminUpdateIncident(cardId, maSuCo, trangThaiMoi) {
    const scRow = document.getElementById(`${cardId}-sc-${maSuCo}`);
    try {
        const res = await fetch('/Chatbot/ExecuteAction', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                actionType: 'CAP_NHAT_TRANG_THAI_SU_CO',
                maSuCo: maSuCo,
                trangThaiMoi: trangThaiMoi
            })
        });
        const data = await res.json();
        if (data.success && scRow) {
            scRow.innerHTML = renderReceiptHtml(data.receipt);
        } else {
            alert(data.message || 'Cập nhật thất bại.');
        }
    } catch (e) {
        alert('Lỗi kết nối: ' + e.message);
    }
}

