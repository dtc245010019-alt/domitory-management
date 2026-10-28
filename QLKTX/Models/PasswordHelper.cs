using System;
using System.Security.Cryptography;
using System.Text;

namespace QLKTX.Models
{
    /// <summary>
    /// Helper mã hóa và xác thực mật khẩu chuẩn công nghiệp (SHA256 + Salt ngẫu nhiên).
    /// Tương thích ngược an toàn 100% với các tài khoản mẫu cũ (admin, ketoan, 123).
    /// </summary>
    public static class PasswordHelper
    {
        private const string HashPrefix = "$S256$";

        /// <summary>
        /// Tạo chuỗi băm mật khẩu bảo mật kèm Salt ngẫu nhiên 16 bytes.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] passBytes = Encoding.UTF8.GetBytes(password);
            byte[] saltedPass = new byte[salt.Length + passBytes.Length];
            Buffer.BlockCopy(salt, 0, saltedPass, 0, salt.Length);
            Buffer.BlockCopy(passBytes, 0, saltedPass, salt.Length, passBytes.Length);

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(saltedPass);

            return $"{HashPrefix}{Convert.ToHexString(salt)}${Convert.ToHexString(hash)}";
        }

        /// <summary>
        /// Xác thực mật khẩu nhập vào với mật khẩu lưu trong cơ sở dữ liệu.
        /// Hỗ trợ cả mật khẩu đã băm $S256$ và mật khẩu văn bản mẫu cũ (backward-compatibility).
        /// </summary>
        public static bool VerifyPassword(string storedPassword, string inputPassword)
        {
            if (string.IsNullOrEmpty(storedPassword) || string.IsNullOrEmpty(inputPassword))
                return false;

            if (storedPassword.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parts = storedPassword.Substring(HashPrefix.Length).Split('$');
                    if (parts.Length != 2) return false;

                    byte[] salt = Convert.FromHexString(parts[0]);
                    byte[] expectedHash = Convert.FromHexString(parts[1]);

                    byte[] passBytes = Encoding.UTF8.GetBytes(inputPassword);
                    byte[] saltedPass = new byte[salt.Length + passBytes.Length];
                    Buffer.BlockCopy(salt, 0, saltedPass, 0, salt.Length);
                    Buffer.BlockCopy(passBytes, 0, saltedPass, salt.Length, passBytes.Length);

                    using var sha256 = SHA256.Create();
                    byte[] actualHash = sha256.ComputeHash(saltedPass);

                    return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
                }
                catch
                {
                    return false;
                }
            }

            // Tương thích ngược với các tài khoản mẫu cũ chưa băm
            return string.Equals(storedPassword, inputPassword, StringComparison.Ordinal);
        }
    }
}

