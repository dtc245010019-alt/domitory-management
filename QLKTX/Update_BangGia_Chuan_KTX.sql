-- ====================================================================================
-- SCRIPT SQL: CHUẨN HÓA BẢNG GIÁ TIỀN PHÒNG & BIỂU PHÍ DỊCH VỤ KÝ TÚC XÁ
-- Áp dụng cho: CSDL Quản lý Ký Túc Xá Sinh Viên (QLKTX)
-- Mô hình: KTX Trường Đại Học hiện đại (Biểu phí tính theo đầu Sinh viên / Tháng)
-- ====================================================================================

BEGIN TRANSACTION;

-- 1. CẬP NHẬT BIỂU PHÍ PHÒNG LƯU TRÚ (VNĐ / Sinh viên / Tháng)
-- ------------------------------------------------------------------------------------
-- A. Phòng thường (quạt trần, 6-8 người): 350.000 VNĐ / SV / tháng (1.750.000 VNĐ / kỳ 5 tháng)
UPDATE Phongs
SET GiaPhong = 350000
WHERE LoaiPhong LIKE N'%thường%';

-- B. Phòng có điều hoà (4-6 người): 650.000 VNĐ / SV / tháng (3.250.000 VNĐ / kỳ 5 tháng)
UPDATE Phongs
SET GiaPhong = 650000
WHERE LoaiPhong LIKE N'%điều hoà%' AND LoaiPhong NOT LIKE N'%nóng lạnh%';

-- C. Phòng điều hoà + nóng lạnh (4 người): 950.000 VNĐ / SV / tháng (4.750.000 VNĐ / kỳ 5 tháng)
UPDATE Phongs
SET GiaPhong = 950000,
    SoLuongGiuong = 4
WHERE LoaiPhong LIKE N'%nóng lạnh%';

-- 2. CẬP NHẬT MỨC TIỀN ĐẶT CỌC HỢP ĐỒNG LƯU TRÚ (Tiêu chuẩn: 1.000.000 VNĐ / hợp đồng)
-- ------------------------------------------------------------------------------------
UPDATE HopDongs
SET TienDatCoc = 1000000;

-- 3. CẬP NHẬT HÓA ĐƠN ĐIỆN NƯỚC THEO ĐƠN GIÁ ĐIỆN MỚI (2.500 VNĐ / kWh)
-- Công thức chuẩn: TongTien = (ChiSoMoi - ChiSoCu) * DonGia
-- ------------------------------------------------------------------------------------
UPDATE HoaDons
SET DonGia = 2500,
    TongTien = (ChiSoMoi - ChiSoCu) * 2500
WHERE LoaiHoaDon = N'Điện nước'
  AND ChiSoCu IS NOT NULL
  AND ChiSoMoi IS NOT NULL
  AND (DonGia = 3500 OR DonGia = 2000 OR TongTien != (ChiSoMoi - ChiSoCu) * 2500);

-- 4. CẬP NHẬT HÓA ĐƠN TIỀN PHÒNG ĐỒNG BỘ VỚI GIÁ PHÒNG MỚI THEO ĐẦU NGƯỜI
-- Tránh trường hợp hóa đơn lưu giá gộp của cả phòng (> 5.000.000 VNĐ)
-- ------------------------------------------------------------------------------------
UPDATE h
SET h.DonGia = p.GiaPhong,
    h.TongTien = p.GiaPhong
FROM HoaDons h
JOIN Phongs p ON h.MaPhong = p.MaPhong
WHERE h.LoaiHoaDon = N'Tiền phòng'
  AND (h.TongTien > 5000000 OR h.TongTien = 1500000);

COMMIT TRANSACTION;

-- ====================================================================================
-- KIỂM TRA LẠI DỮ LIỆU SAU KHI CẬP NHẬT
-- ====================================================================================
SELECT MaPhong, LoaiPhong, SoLuongGiuong, GiaPhong AS GiaPhong_VND_Thang FROM Phongs ORDER BY MaPhong;
SELECT TOP 5 MaHopDong, MaSV, MaPhong, TienDatCoc FROM HopDongs;
SELECT TOP 10 MaHoaDon, LoaiHoaDon, ChiSoCu, ChiSoMoi, DonGia, TongTien FROM HoaDons ORDER BY NgayLap DESC;

