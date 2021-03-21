using System;
using System.Security.Cryptography;

namespace TopoAlign
{
    public static class Cypher
    {
        public static string EncryptData(string Message, string passphrase)
        {
            byte[] Results;
            var UTF8 = new System.Text.UTF8Encoding();
            var HashProvider = new MD5CryptoServiceProvider();
            var TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(passphrase));
            var TDESAlgorithm = new TripleDESCryptoServiceProvider();
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;
            var DataToEncrypt = UTF8.GetBytes(Message);
            try
            {
                var Encryptor = TDESAlgorithm.CreateEncryptor();
                Results = Encryptor.TransformFinalBlock(DataToEncrypt, 0, DataToEncrypt.Length);
            }
            finally
            {
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            return Convert.ToBase64String(Results);
        }

        public static string DecryptString(string Message, string passphrase)
        {
            byte[] Results;
            var UTF8 = new System.Text.UTF8Encoding();
            var HashProvider = new MD5CryptoServiceProvider();
            var TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(passphrase));
            var TDESAlgorithm = new TripleDESCryptoServiceProvider();
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;
            var DataToDecrypt = Convert.FromBase64String(Message);
            try
            {
                var Decryptor = TDESAlgorithm.CreateDecryptor();
                Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
            }
            finally
            {
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            return UTF8.GetString(Results);
        }
    }
}
