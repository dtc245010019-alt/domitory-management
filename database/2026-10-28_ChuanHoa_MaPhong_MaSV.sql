/* ==========================================================================================
   QLKTX - SCRIPT CHUẨN HÓA DỮ LIỆU TẠI CHỖ (IN-PLACE UPDATE)
   Ngày     : 2026-10-28
   Phạm vi  :
     (1) Chuẩn hóa MaPhong -> P.[TÒA]-[SỐ_PHÒNG]  (mã cũ chỉ có số => mặc định Tòa A1)
         "101" | "P101" | "P.101" | "p-101" | "Phòng 101"  -> "P.A1-101"   (ký tự đầu = tầng)
         "A1-01" | "B1-03"                                  -> "P.A1-01" | "P.B1-03" (giữ tòa + số)
         "P.A1-101" (đã chuẩn)                              -> giữ nguyên  (script idempotent)
         Đồng bộ: Phongs, SinhViens, HopDongs, HoaDons, BaoCaoSuCos, CoSoVatChats.
     (2) Rà soát MaSV: TRIM, bỏ ký tự ẩn (TAB, CR, LF, NBSP, zero-width), UPPER.
         Nếu sau chuẩn hóa bị trùng => bản ghi thứ 2, 3... nhận hậu tố _1, _2... (bỏ qua
         hậu tố đã tồn tại). Đồng bộ: HopDongs, HoaDons, BaoCaoSuCos, ViPhams, TaiKhoans.
     (3) KHÔNG đụng tới cột Lop; KHÔNG thay đổi việc phân phòng của sinh viên
         (chỉ đổi "tên" mã phòng, quan hệ SV-Phòng giữ nguyên 1:1 như cũ).
   An toàn  :
     - Không DROP / TRUNCATE / DELETE. Chỉ UPDATE.
     - 1 TRANSACTION + TRY...CATCH + XACT_ABORT ON => lỗi bất kỳ là ROLLBACK toàn bộ.
     - Tạm NOCHECK các FK trỏ tới Phongs/SinhViens, sau đó bật lại bằng WITH CHECK CHECK
       (kiểm tra lại toàn bộ dữ liệu + FK được "trusted" trở lại).
     - Kiểm tra trước khi ghi: mã không parse được, mã mới bị đụng nhau, dữ liệu mồ côi,
       vượt độ dài 20 ký tự => dừng và ROLLBACK, không ghi gì.
     - Đếm số dòng mọi bảng trước/sau: lệch 1 dòng là ROLLBACK.
     - @DryRun = 1 : chạy thử toàn bộ, xuất báo cáo rồi ROLLBACK.
   Yêu cầu  : SQL Server 2017+ (STRING_AGG). LocalDB của dự án là bản 15.x (2019) -> OK.
   Khuyến nghị: DỪNG ứng dụng QLKTX trong lúc chạy (script khóa độc quyền các bảng).
   ========================================================================================== */

USE [QLKTX];
GO

-- (Khuyến nghị) Sao lưu trước khi chạy thật:
-- BACKUP DATABASE [QLKTX] TO DISK = N'D:\Backup\QLKTX_truoc_chuan_hoa_20261028.bak' WITH INIT, COPY_ONLY;
-- GO

/* ==========================================================================================
   PHẦN A - CẬP NHẬT DỮ LIỆU
   ========================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun     BIT          = 1;      -- 1 = CHẠY THỬ (ROLLBACK) | 0 = GHI THẬT (COMMIT)
DECLARE @ToaMacDinh NVARCHAR(10) = N'A1';  -- Tòa gán cho mã phòng cũ chỉ có số

DECLARE @MapPhong TABLE (
    MaPhongCu  NVARCHAR(20) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    PhanThan   NVARCHAR(50) COLLATE DATABASE_DEFAULT NULL,   -- phần còn lại sau khi bóc tiền tố P/P./Phòng
    Toa        NVARCHAR(50) COLLATE DATABASE_DEFAULT NULL,
    SoPhong    NVARCHAR(50) COLLATE DATABASE_DEFAULT NULL,
    MaPhongMoi NVARCHAR(120) COLLATE DATABASE_DEFAULT NULL,
    CanDoi     BIT NOT NULL DEFAULT 0
);
DECLARE @MapSV TABLE (
    MaSVCu    NVARCHAR(20) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    MaSVChuan NVARCHAR(20) COLLATE DATABASE_DEFAULT NOT NULL,
    ThuTu     INT NOT NULL,                                   -- 1 = bản ghi giữ mã gốc
    MaSVMoi   NVARCHAR(40) COLLATE DATABASE_DEFAULT NULL,
    CanDoi    BIT NOT NULL DEFAULT 0
);
DECLARE @FK     TABLE (BangCon NVARCHAR(300), TenFK NVARCHAR(300), DangTatSan BIT);
DECLARE @SoDong TABLE (Bang SYSNAME PRIMARY KEY, Truoc INT NOT NULL, Sau INT NULL);
DECLARE @Log    TABLE (Id INT IDENTITY(1,1), Buoc NVARCHAR(200), SoDong INT);

DECLARE @sql NVARCHAR(MAX), @ds NVARCHAR(MAX), @msg NVARCHAR(2048), @n INT;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---------- A0. Khóa bảng + đếm số dòng TRƯỚC khi cập nhật ---------- */
    INSERT @SoDong (Bang, Truoc)
    SELECT N'Phongs',       COUNT(*) FROM dbo.Phongs       WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'SinhViens',    COUNT(*) FROM dbo.SinhViens    WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'HopDongs',     COUNT(*) FROM dbo.HopDongs     WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'HoaDons',      COUNT(*) FROM dbo.HoaDons      WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'BaoCaoSuCos',  COUNT(*) FROM dbo.BaoCaoSuCos  WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'CoSoVatChats', COUNT(*) FROM dbo.CoSoVatChats WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'ViPhams',      COUNT(*) FROM dbo.ViPhams      WITH (TABLOCKX, HOLDLOCK) UNION ALL
    SELECT N'TaiKhoans',    COUNT(*) FROM dbo.TaiKhoans    WITH (TABLOCKX, HOLDLOCK);

    /* ---------- A1. Kiểm tra dữ liệu mồ côi (MaPhong ở bảng con không có trong Phongs) ---------- */
    SELECT @ds = STRING_AGG(CAST(o.Bang + N':' + o.MaPhong AS NVARCHAR(MAX)), N', ')
    FROM (
        SELECT DISTINCT N'SinhViens' AS Bang, x.MaPhong FROM dbo.SinhViens x
         WHERE x.MaPhong IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
        UNION ALL
        SELECT DISTINCT N'HopDongs', x.MaPhong FROM dbo.HopDongs x
         WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
        UNION ALL
        SELECT DISTINCT N'HoaDons', x.MaPhong FROM dbo.HoaDons x
         WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
        UNION ALL
        SELECT DISTINCT N'BaoCaoSuCos', x.MaPhong FROM dbo.BaoCaoSuCos x
         WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
        UNION ALL
        SELECT DISTINCT N'CoSoVatChats', x.MaPhong FROM dbo.CoSoVatChats x
         WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
    ) o;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A1] Có MaPhong mồ côi (không tồn tại trong Phongs): ' + @ds, 2000);
        THROW 50001, @msg, 1;
    END;

    /* ---------- A2. Xây bảng ánh xạ MaPhong cũ -> mới ---------- */
    INSERT @MapPhong (MaPhongCu, PhanThan, Toa, SoPhong)
    SELECT p.MaPhong,
           d.s3,
           CASE WHEN e.pos = 0 AND d.s3 NOT LIKE N'%[^0-9]%' THEN @ToaMacDinh   -- chỉ có số => tòa mặc định
                WHEN e.pos > 1                                THEN LEFT(d.s3, e.pos - 1)
           END,
           CASE WHEN e.pos = 0 THEN d.s3 ELSE SUBSTRING(d.s3, e.pos + 1, 50) END
    FROM dbo.Phongs p
    -- bỏ khoảng trắng / TAB / NBSP, viết hoa
    CROSS APPLY (SELECT s0 = UPPER(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(p.MaPhong)), N' ', N''), NCHAR(9), N''), NCHAR(160), N''))) a
    -- bóc tiền tố "PHÒNG" / "PHONG"
    CROSS APPLY (SELECT s1 = CASE WHEN a.s0 LIKE N'PHÒNG%' OR a.s0 LIKE N'PHONG%' THEN STUFF(a.s0, 1, 5, N'') ELSE a.s0 END) b
    -- bóc tiền tố "P." / "P-" / "P_" / "P"
    CROSS APPLY (SELECT s2 = CASE WHEN b.s1 LIKE N'P[._-]%' THEN STUFF(b.s1, 1, 2, N'')
                                  WHEN b.s1 LIKE N'P%'      THEN STUFF(b.s1, 1, 1, N'')
                                  ELSE b.s1 END) c
    -- đồng nhất dấu phân cách tòa/số về "-"
    CROSS APPLY (SELECT s3 = REPLACE(REPLACE(c.s2, N'.', N'-'), N'_', N'-')) d
    CROSS APPLY (SELECT pos = CHARINDEX(N'-', d.s3)) e;

    UPDATE @MapPhong
       SET MaPhongMoi = N'P.' + Toa + N'-' + SoPhong
     WHERE Toa LIKE N'[A-Z]%' AND Toa NOT LIKE N'%[^A-Z0-9]%'          -- tòa: chữ cái đầu, chỉ gồm chữ/số
       AND SoPhong LIKE N'[0-9][0-9]%' AND SoPhong NOT LIKE N'%[^0-9]%' -- số phòng: >= 2 chữ số
       AND (    CHARINDEX(N'-', PhanThan) > 0                           -- mã đã có tòa: giữ nguyên số phòng
             OR (LEN(SoPhong) = 3 AND SoPhong LIKE N'[1-9]%') );        -- mã chỉ có số: 3 chữ số, ký tự đầu = tầng

    UPDATE @MapPhong
       SET CanDoi = CASE WHEN MaPhongCu COLLATE Latin1_General_BIN2 <> MaPhongMoi COLLATE Latin1_General_BIN2
                         THEN 1 ELSE 0 END
     WHERE MaPhongMoi IS NOT NULL;

    /* ---------- A3. Kiểm tra ánh xạ MaPhong ---------- */
    -- (a) Mã không suy ra được định dạng chuẩn
    SELECT @ds = STRING_AGG(CAST(N'"' + MaPhongCu + N'"' AS NVARCHAR(MAX)), N', ')
      FROM @MapPhong WHERE MaPhongMoi IS NULL;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A3a] Không chuẩn hóa được các mã phòng sau (cần xử lý thủ công): ' + @ds, 2000);
        THROW 50002, @msg, 1;
    END;

    -- (b) Vượt độ dài cột nvarchar(20)
    SELECT @ds = STRING_AGG(CAST(MaPhongMoi AS NVARCHAR(MAX)), N', ')
      FROM @MapPhong WHERE LEN(MaPhongMoi) > 20;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A3b] Mã phòng mới vượt 20 ký tự: ' + @ds, 2000);
        THROW 50003, @msg, 1;
    END;

    -- (c) Nhiều mã cũ cùng quy về 1 mã mới (vd: "101" và "P101" cùng tồn tại) => đụng khóa chính
    SELECT @ds = STRING_AGG(CAST(x.MaPhongMoi + N' <= {' + x.DanhSach + N'}' AS NVARCHAR(MAX)), N'; ')
    FROM (SELECT MaPhongMoi, DanhSach = STRING_AGG(CAST(MaPhongCu AS NVARCHAR(MAX)), N', ')
            FROM @MapPhong GROUP BY MaPhongMoi HAVING COUNT(*) > 1) x;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A3c] Các mã phòng cũ bị quy về cùng một mã mới (cần gộp phòng thủ công trước): ' + @ds, 2000);
        THROW 50004, @msg, 1;
    END;

    /* ---------- A4. Xây bảng ánh xạ MaSV cũ -> mới ---------- */
    INSERT @MapSV (MaSVCu, MaSVChuan, ThuTu)
    SELECT s.MaSV,
           c.Chuan,
           ROW_NUMBER() OVER (PARTITION BY c.Chuan
                              ORDER BY CASE WHEN s.MaSV COLLATE Latin1_General_BIN2 = c.Chuan COLLATE Latin1_General_BIN2
                                            THEN 0 ELSE 1 END,   -- ưu tiên bản ghi đã đúng chuẩn giữ mã gốc
                                       s.MaSV)
    FROM dbo.SinhViens s
    CROSS APPLY (SELECT Chuan = UPPER(LTRIM(RTRIM(
                     REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(s.MaSV,
                         NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''), NCHAR(160), N''), NCHAR(8203), N'')))) ) c;

    UPDATE @MapSV SET MaSVMoi = MaSVChuan WHERE ThuTu = 1;

    -- Bản ghi trùng (ThuTu >= 2): cấp hậu tố _n nhỏ nhất CHƯA bị chiếm (vd: DTC001_1, DTC001_2 ...)
    ;WITH So AS (
        SELECT TOP (5000) n = ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
        FROM sys.all_objects o1 CROSS JOIN sys.all_objects o2
    ),
    UngVien AS (
        SELECT g.MaSVChuan, So.n,
               k = ROW_NUMBER() OVER (PARTITION BY g.MaSVChuan ORDER BY So.n)
        FROM (SELECT DISTINCT MaSVChuan FROM @MapSV WHERE ThuTu > 1) g
        CROSS JOIN So
        WHERE NOT EXISTS (SELECT 1 FROM @MapSV x
                           WHERE x.ThuTu = 1
                             AND x.MaSVMoi = g.MaSVChuan + N'_' + CAST(So.n AS NVARCHAR(10)))
    )
    UPDATE m
       SET m.MaSVMoi = m.MaSVChuan + N'_' + CAST(u.n AS NVARCHAR(10))
      FROM @MapSV m
      JOIN UngVien u ON u.MaSVChuan = m.MaSVChuan AND u.k = m.ThuTu - 1
     WHERE m.ThuTu > 1;

    UPDATE @MapSV
       SET CanDoi = CASE WHEN MaSVCu COLLATE Latin1_General_BIN2 <> MaSVMoi COLLATE Latin1_General_BIN2
                         THEN 1 ELSE 0 END;

    /* ---------- A5. Kiểm tra ánh xạ MaSV ---------- */
    SELECT @ds = STRING_AGG(CAST(N'"' + MaSVCu + N'" -> "' + ISNULL(MaSVMoi, N'NULL') + N'"' AS NVARCHAR(MAX)), N', ')
      FROM @MapSV WHERE MaSVMoi IS NULL OR MaSVMoi = N'' OR LEN(MaSVMoi) > 20;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A5a] MaSV mới rỗng hoặc vượt 20 ký tự: ' + @ds, 2000);
        THROW 50005, @msg, 1;
    END;

    SELECT @ds = STRING_AGG(CAST(MaSVMoi AS NVARCHAR(MAX)), N', ')
    FROM (SELECT MaSVMoi FROM @MapSV GROUP BY MaSVMoi HAVING COUNT(*) > 1) x;
    IF @ds IS NOT NULL
    BEGIN
        SET @msg = LEFT(N'[A5b] MaSV mới vẫn còn trùng: ' + @ds, 2000);
        THROW 50006, @msg, 1;
    END;

    /* ---------- A6. Tạm vô hiệu hóa các FK trỏ tới Phongs / SinhViens (NOCHECK) ---------- */
    INSERT @FK (BangCon, TenFK, DangTatSan)
    SELECT QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)),
           QUOTENAME(fk.name),
           fk.is_disabled
    FROM sys.foreign_keys fk
    WHERE fk.referenced_object_id IN (OBJECT_ID(N'dbo.Phongs'), OBJECT_ID(N'dbo.SinhViens'));

    SET @sql = NULL;
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE ' + BangCon + N' NOCHECK CONSTRAINT ' + TenFK + N';' AS NVARCHAR(MAX)), NCHAR(10))
      FROM @FK WHERE DangTatSan = 0;            -- chỉ đụng tới FK đang bật
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;
    INSERT @Log (Buoc, SoDong) SELECT N'NOCHECK FK (Phongs/SinhViens)', COUNT(*) FROM @FK WHERE DangTatSan = 0;

    /* ---------- A7. Cập nhật MaPhong: bảng cha trước, sau đó toàn bộ bảng con ---------- */
    UPDATE p SET p.MaPhong = m.MaPhongMoi
      FROM dbo.Phongs p JOIN @MapPhong m ON m.MaPhongCu = p.MaPhong
     WHERE m.CanDoi = 1;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'Phongs.MaPhong', @n);

    UPDATE x SET x.MaPhong = m.MaPhongMoi
      FROM dbo.SinhViens x JOIN @MapPhong m ON m.MaPhongCu = x.MaPhong
     WHERE x.MaPhong COLLATE Latin1_General_BIN2 <> m.MaPhongMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'SinhViens.MaPhong', @n);

    UPDATE x SET x.MaPhong = m.MaPhongMoi
      FROM dbo.HopDongs x JOIN @MapPhong m ON m.MaPhongCu = x.MaPhong
     WHERE x.MaPhong COLLATE Latin1_General_BIN2 <> m.MaPhongMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'HopDongs.MaPhong', @n);

    UPDATE x SET x.MaPhong = m.MaPhongMoi
      FROM dbo.HoaDons x JOIN @MapPhong m ON m.MaPhongCu = x.MaPhong
     WHERE x.MaPhong COLLATE Latin1_General_BIN2 <> m.MaPhongMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'HoaDons.MaPhong', @n);

    UPDATE x SET x.MaPhong = m.MaPhongMoi
      FROM dbo.BaoCaoSuCos x JOIN @MapPhong m ON m.MaPhongCu = x.MaPhong
     WHERE x.MaPhong COLLATE Latin1_General_BIN2 <> m.MaPhongMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'BaoCaoSuCos.MaPhong', @n);

    UPDATE x SET x.MaPhong = m.MaPhongMoi
      FROM dbo.CoSoVatChats x JOIN @MapPhong m ON m.MaPhongCu = x.MaPhong
     WHERE x.MaPhong COLLATE Latin1_General_BIN2 <> m.MaPhongMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'CoSoVatChats.MaPhong', @n);

    /* ---------- A8. Cập nhật MaSV: bảng cha trước, sau đó toàn bộ bảng con ---------- */
    UPDATE s SET s.MaSV = m.MaSVMoi
      FROM dbo.SinhViens s JOIN @MapSV m ON m.MaSVCu = s.MaSV
     WHERE m.CanDoi = 1;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'SinhViens.MaSV', @n);

    UPDATE x SET x.MaSV = m.MaSVMoi
      FROM dbo.HopDongs x JOIN @MapSV m ON m.MaSVCu = x.MaSV
     WHERE x.MaSV COLLATE Latin1_General_BIN2 <> m.MaSVMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'HopDongs.MaSV', @n);

    UPDATE x SET x.MaSV = m.MaSVMoi
      FROM dbo.HoaDons x JOIN @MapSV m ON m.MaSVCu = x.MaSV
     WHERE x.MaSV COLLATE Latin1_General_BIN2 <> m.MaSVMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'HoaDons.MaSV', @n);

    UPDATE x SET x.MaSV = m.MaSVMoi
      FROM dbo.BaoCaoSuCos x JOIN @MapSV m ON m.MaSVCu = x.MaSV
     WHERE x.MaSV COLLATE Latin1_General_BIN2 <> m.MaSVMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'BaoCaoSuCos.MaSV', @n);

    UPDATE x SET x.MaSV = m.MaSVMoi
      FROM dbo.ViPhams x JOIN @MapSV m ON m.MaSVCu = x.MaSV
     WHERE x.MaSV COLLATE Latin1_General_BIN2 <> m.MaSVMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'ViPhams.MaSV', @n);

    -- TaiKhoans không có FK nhưng lưu MaSV + quy ước TenDangNhap = MaSV
    UPDATE t SET t.MaSV = m.MaSVMoi
      FROM dbo.TaiKhoans t JOIN @MapSV m ON m.MaSVCu = t.MaSV
     WHERE t.MaSV COLLATE Latin1_General_BIN2 <> m.MaSVMoi COLLATE Latin1_General_BIN2;
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'TaiKhoans.MaSV', @n);

    UPDATE t SET t.TenDangNhap = m.MaSVMoi
      FROM dbo.TaiKhoans t JOIN @MapSV m ON m.MaSVCu = t.TenDangNhap
     WHERE m.CanDoi = 1
       AND NOT EXISTS (SELECT 1 FROM dbo.TaiKhoans t2           -- không đè lên tài khoản khác
                        WHERE t2.TenDangNhap = m.MaSVMoi
                          AND NOT EXISTS (SELECT 1 FROM @MapSV m2
                                           WHERE m2.CanDoi = 1 AND m2.MaSVCu = t2.TenDangNhap));
    SET @n = @@ROWCOUNT; INSERT @Log VALUES (N'TaiKhoans.TenDangNhap', @n);

    /* ---------- A9. Bật lại FK + kiểm tra lại toàn bộ dữ liệu (WITH CHECK CHECK CONSTRAINT) ---------- */
    SET @sql = NULL;
    SELECT @sql = STRING_AGG(CAST(N'ALTER TABLE ' + BangCon + N' WITH CHECK CHECK CONSTRAINT ' + TenFK + N';' AS NVARCHAR(MAX)), NCHAR(10))
      FROM @FK WHERE DangTatSan = 0;
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;
    INSERT @Log (Buoc, SoDong) SELECT N'WITH CHECK CHECK FK (Phongs/SinhViens)', COUNT(*) FROM @FK WHERE DangTatSan = 0;

    /* ---------- A10. Hậu kiểm: số dòng không đổi, mã phòng đúng chuẩn ---------- */
    UPDATE d SET d.Sau = c.SoDong
    FROM @SoDong d
    JOIN (SELECT N'Phongs' AS Bang, COUNT(*) AS SoDong FROM dbo.Phongs UNION ALL
          SELECT N'SinhViens',    COUNT(*) FROM dbo.SinhViens    UNION ALL
          SELECT N'HopDongs',     COUNT(*) FROM dbo.HopDongs     UNION ALL
          SELECT N'HoaDons',      COUNT(*) FROM dbo.HoaDons      UNION ALL
          SELECT N'BaoCaoSuCos',  COUNT(*) FROM dbo.BaoCaoSuCos  UNION ALL
          SELECT N'CoSoVatChats', COUNT(*) FROM dbo.CoSoVatChats UNION ALL
          SELECT N'ViPhams',      COUNT(*) FROM dbo.ViPhams      UNION ALL
          SELECT N'TaiKhoans',    COUNT(*) FROM dbo.TaiKhoans) c ON c.Bang = d.Bang;

    IF EXISTS (SELECT 1 FROM @SoDong WHERE Sau IS NULL OR Sau <> Truoc)
        THROW 50007, N'[A10] Số dòng của ít nhất một bảng đã thay đổi - hủy toàn bộ.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Phongs
                WHERE MaPhong NOT LIKE N'P.[A-Z]%-[0-9][0-9]%'
                   OR MaPhong COLLATE Latin1_General_BIN2 <> UPPER(MaPhong) COLLATE Latin1_General_BIN2)
        THROW 50008, N'[A10] Vẫn còn MaPhong chưa đúng định dạng P.[TÒA]-[SỐ] - hủy toàn bộ.', 1;

    /* ---------- A11. Chốt giao dịch ---------- */
    IF @DryRun = 1
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'>>> DRY RUN: mọi thay đổi đã được ROLLBACK. Kiểm tra báo cáo, sau đó đặt @DryRun = 0 để ghi thật.';
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        PRINT N'>>> ĐÃ COMMIT: chuẩn hóa dữ liệu thành công.';
    END;

    /* ---------- Báo cáo (table variable vẫn giữ dữ liệu sau ROLLBACK) ---------- */
    SELECT N'1. Ánh xạ mã phòng (chỉ dòng thay đổi)' AS BaoCao, MaPhongCu, MaPhongMoi,
           Toa, SoPhong, Tang = CASE WHEN LEN(SoPhong) >= 3 THEN LEFT(SoPhong, 1) END
      FROM @MapPhong WHERE CanDoi = 1 ORDER BY Toa, SoPhong;

    SELECT N'2. Ánh xạ MaSV (chỉ dòng thay đổi)' AS BaoCao, MaSVCu = N'[' + MaSVCu + N']', MaSVMoi,
           LyDo = CASE WHEN ThuTu > 1 THEN N'Trùng mã - thêm hậu tố' ELSE N'Chuẩn hóa khoảng trắng/ký tự ẩn/chữ hoa' END
      FROM @MapSV WHERE CanDoi = 1 ORDER BY MaSVChuan, ThuTu;

    SELECT N'3. Số dòng cập nhật' AS BaoCao, Buoc, SoDong FROM @Log ORDER BY Id;
    SELECT N'4. Số dòng trước/sau' AS BaoCao, Bang, Truoc, Sau,
           KetQua = CASE WHEN Truoc = Sau THEN N'OK' ELSE N'LỆCH' END
      FROM @SoDong;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT N'>>> LỖI - ĐÃ ROLLBACK TOÀN BỘ, CSDL KHÔNG THAY ĐỔI.';
    PRINT N'    Số lỗi : ' + CAST(ERROR_NUMBER() AS NVARCHAR(20)) + N' | Dòng: ' + CAST(ERROR_LINE() AS NVARCHAR(20));
    PRINT N'    Chi tiết: ' + ERROR_MESSAGE();

    -- Hỗ trợ chẩn đoán
    SELECT N'Mã phòng không chuẩn hóa được / bị đụng' AS ChanDoan, m.MaPhongCu, m.MaPhongMoi
      FROM @MapPhong m
     WHERE m.MaPhongMoi IS NULL
        OR m.MaPhongMoi IN (SELECT MaPhongMoi FROM @MapPhong GROUP BY MaPhongMoi HAVING COUNT(*) > 1);

    THROW;
END CATCH;
GO


/* ==========================================================================================
   PHẦN B - CÂU LỆNH KIỂM TRA SAU CẬP NHẬT (chạy riêng, chỉ đọc)
   ========================================================================================== */

-- B1. Mã phòng SAI định dạng P.[TÒA]-[SỐ]  => KỲ VỌNG: 0 dòng
SELECT N'B1 - Sai định dạng' AS KiemTra, p.MaPhong
FROM dbo.Phongs p
CROSS APPLY (SELECT pos = CHARINDEX(N'-', p.MaPhong)) x
WHERE x.pos < 4
   OR p.MaPhong COLLATE Latin1_General_BIN2 <> UPPER(p.MaPhong) COLLATE Latin1_General_BIN2   -- phải viết hoa
   OR p.MaPhong NOT LIKE N'P.[A-Z]%'
   OR SUBSTRING(p.MaPhong, 3, CASE WHEN x.pos > 3 THEN x.pos - 3 ELSE 0 END) LIKE N'%[^A-Z0-9]%'
   OR SUBSTRING(p.MaPhong, x.pos + 1, 20) NOT LIKE N'[0-9][0-9]%'
   OR SUBSTRING(p.MaPhong, x.pos + 1, 20) LIKE N'%[^0-9]%';

-- B2. Toàn cảnh phòng: tòa, tầng, số lượng dữ liệu liên kết (lịch sử được giữ nguyên)
SELECT p.MaPhong,
       Toa       = SUBSTRING(p.MaPhong, 3, CHARINDEX(N'-', p.MaPhong) - 3),
       SoPhong   = SUBSTRING(p.MaPhong, CHARINDEX(N'-', p.MaPhong) + 1, 20),
       Tang      = CASE WHEN LEN(SUBSTRING(p.MaPhong, CHARINDEX(N'-', p.MaPhong) + 1, 20)) >= 3
                        THEN LEFT(SUBSTRING(p.MaPhong, CHARINDEX(N'-', p.MaPhong) + 1, 20), 1) END,
       p.LoaiPhong, p.SoLuongGiuong, p.TinhTrang,
       SoSV      = (SELECT COUNT(*) FROM dbo.SinhViens    x WHERE x.MaPhong = p.MaPhong),
       SoHopDong = (SELECT COUNT(*) FROM dbo.HopDongs     x WHERE x.MaPhong = p.MaPhong),
       SoHoaDon  = (SELECT COUNT(*) FROM dbo.HoaDons      x WHERE x.MaPhong = p.MaPhong),
       SoSuCo    = (SELECT COUNT(*) FROM dbo.BaoCaoSuCos  x WHERE x.MaPhong = p.MaPhong),
       SoThietBi = (SELECT COUNT(*) FROM dbo.CoSoVatChats x WHERE x.MaPhong = p.MaPhong)
FROM dbo.Phongs p
WHERE p.MaPhong LIKE N'P.%-%'
ORDER BY Toa, SoPhong;

-- B3. Dữ liệu mồ côi sau cập nhật  => KỲ VỌNG: tất cả cột SoDongMoCoi = 0
SELECT N'SinhViens.MaPhong'    AS Cot, COUNT(*) AS SoDongMoCoi FROM dbo.SinhViens x    WHERE x.MaPhong IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Phongs p WHERE p.MaPhong = x.MaPhong)
UNION ALL SELECT N'HopDongs.MaPhong',     COUNT(*) FROM dbo.HopDongs x     WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p    WHERE p.MaPhong = x.MaPhong)
UNION ALL SELECT N'HoaDons.MaPhong',      COUNT(*) FROM dbo.HoaDons x      WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p    WHERE p.MaPhong = x.MaPhong)
UNION ALL SELECT N'BaoCaoSuCos.MaPhong',  COUNT(*) FROM dbo.BaoCaoSuCos x  WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p    WHERE p.MaPhong = x.MaPhong)
UNION ALL SELECT N'CoSoVatChats.MaPhong', COUNT(*) FROM dbo.CoSoVatChats x WHERE NOT EXISTS (SELECT 1 FROM dbo.Phongs p    WHERE p.MaPhong = x.MaPhong)
UNION ALL SELECT N'HopDongs.MaSV',        COUNT(*) FROM dbo.HopDongs x     WHERE NOT EXISTS (SELECT 1 FROM dbo.SinhViens s WHERE s.MaSV = x.MaSV)
UNION ALL SELECT N'HoaDons.MaSV',         COUNT(*) FROM dbo.HoaDons x      WHERE NOT EXISTS (SELECT 1 FROM dbo.SinhViens s WHERE s.MaSV = x.MaSV)
UNION ALL SELECT N'BaoCaoSuCos.MaSV',     COUNT(*) FROM dbo.BaoCaoSuCos x  WHERE NOT EXISTS (SELECT 1 FROM dbo.SinhViens s WHERE s.MaSV = x.MaSV)
UNION ALL SELECT N'ViPhams.MaSV',         COUNT(*) FROM dbo.ViPhams x      WHERE NOT EXISTS (SELECT 1 FROM dbo.SinhViens s WHERE s.MaSV = x.MaSV)
UNION ALL SELECT N'TaiKhoans.MaSV',       COUNT(*) FROM dbo.TaiKhoans x    WHERE x.MaSV IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.SinhViens s WHERE s.MaSV = x.MaSV);

-- B4. Trạng thái khóa ngoại  => KỲ VỌNG: DaTat = 0 và KhongTinCay = 0 cho mọi FK
SELECT OBJECT_NAME(fk.parent_object_id)     AS BangCon,
       fk.name                              AS TenFK,
       OBJECT_NAME(fk.referenced_object_id) AS BangCha,
       fk.is_disabled                       AS DaTat,
       fk.is_not_trusted                    AS KhongTinCay
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id IN (OBJECT_ID(N'dbo.Phongs'), OBJECT_ID(N'dbo.SinhViens'))
ORDER BY BangCha, BangCon;

-- B5. MaSV trùng (kể cả trùng "ngầm" do khoảng trắng / ký tự ẩn / hoa-thường)  => KỲ VỌNG: 0 dòng
SELECT N'B5 - MaSV trùng' AS KiemTra, c.Chuan, COUNT(*) AS SoBanGhi,
       STRING_AGG(CAST(N'[' + s.MaSV + N']' AS NVARCHAR(MAX)), N', ') AS DanhSach
FROM dbo.SinhViens s
CROSS APPLY (SELECT Chuan = UPPER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(s.MaSV,
                 NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''), NCHAR(160), N''), NCHAR(8203), N''))))) c
GROUP BY c.Chuan
HAVING COUNT(*) > 1;

-- B6. MaSV còn chứa khoảng trắng / ký tự ẩn / chữ thường  => KỲ VỌNG: 0 dòng
SELECT N'B6 - MaSV chưa chuẩn' AS KiemTra, N'[' + MaSV + N']' AS MaSV
FROM dbo.SinhViens
WHERE MaSV COLLATE Latin1_General_BIN2 <> UPPER(LTRIM(RTRIM(MaSV))) COLLATE Latin1_General_BIN2
   OR MaSV LIKE N'%[' + NCHAR(9) + NCHAR(10) + NCHAR(13) + NCHAR(160) + NCHAR(8203) + N' ]%';

-- B7. Lớp có nhiều sinh viên  => ĐƯỢC PHÉP (giữ nguyên)
SELECT Lop, COUNT(*) AS SoSinhVien
FROM dbo.SinhViens
GROUP BY Lop
HAVING COUNT(*) > 1
ORDER BY SoSinhVien DESC, Lop;

-- B8. Phòng có nhiều sinh viên đang ở  => ĐƯỢC PHÉP (giữ nguyên danh sách)
SELECT s.MaPhong, COUNT(*) AS SoSinhVien,
       STRING_AGG(CAST(s.MaSV AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY s.MaSV) AS DanhSachSV
FROM dbo.SinhViens s
WHERE s.MaPhong IS NOT NULL
GROUP BY s.MaPhong
HAVING COUNT(*) > 1
ORDER BY s.MaPhong;

-- B9. Tổng số dòng từng bảng (so sánh với báo cáo "4. Số dòng trước/sau" ở Phần A)
SELECT N'Phongs' AS Bang, COUNT(*) AS SoDong FROM dbo.Phongs
UNION ALL SELECT N'SinhViens',    COUNT(*) FROM dbo.SinhViens
UNION ALL SELECT N'HopDongs',     COUNT(*) FROM dbo.HopDongs
UNION ALL SELECT N'HoaDons',      COUNT(*) FROM dbo.HoaDons
UNION ALL SELECT N'BaoCaoSuCos',  COUNT(*) FROM dbo.BaoCaoSuCos
UNION ALL SELECT N'CoSoVatChats', COUNT(*) FROM dbo.CoSoVatChats
UNION ALL SELECT N'ViPhams',      COUNT(*) FROM dbo.ViPhams
UNION ALL SELECT N'TaiKhoans',    COUNT(*) FROM dbo.TaiKhoans;
GO
