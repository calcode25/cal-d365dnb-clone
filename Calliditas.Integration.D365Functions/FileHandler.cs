using System.Reflection;
using Azure.Storage;
using Azure.Storage.Files.Shares;
using Renci.SshNet;
using PgpCore;
using System.Text;

namespace Calliditas.Integration.D365Functions
{
    public abstract class FileHandler : IDisposable
    {
        public abstract void Connect();

        public abstract void Dispose();

        public abstract Task UploadFile(MemoryStream memStream, string name, bool overwrite);

        public abstract Task<List<string>> GetItems(string path);

        public virtual async Task<byte[]> ProcessFile(byte[] bytes, string inputFileName, string outputFileName)
        {
            return await Task.FromResult(bytes);
        }

        public virtual string GetName(string attachmentName)
        {
            return string.Empty;
        }

        public abstract string GetPath();
    }

    public class AzureFileShareHandler : FileHandler
    {
        public String ShareName { get; set; }

        public AzureFileShareHandler(string shareName)
        {
            ShareName = shareName;
        }

        public override void Connect()
        {
        }

        public override void Dispose()
        {
        }

        public override async Task UploadFile(MemoryStream memStream, string name, bool overwrite)
        {
            var azureFileShareUrl = Environment.GetEnvironmentVariable("AzureFileShareBaseUrl");
            var StorageSharedKeyCredentialAccountName = Environment.GetEnvironmentVariable("StorageSharedKeyCredentialAccountName");
            var StorageSharedKeyCredentialAccountKey = Environment.GetEnvironmentVariable("StorageSharedKeyCredentialAccountKey");

            var client = new ShareFileClient(new Uri(
                    $"{azureFileShareUrl}{ShareName}/Send/{Path.GetFileName(name)}"),
                new StorageSharedKeyCredential(StorageSharedKeyCredentialAccountName,
                    StorageSharedKeyCredentialAccountKey));

            memStream.Position = 0;
            var exists = await client.ExistsAsync();
            if (exists.Value)
            {
                await client.DeleteAsync();
            }

            await client.CreateAsync(memStream.Length);
            await client.UploadAsync(memStream);
        }

        public override string GetName(string attachmentName)
        {
            return string.Empty;
        }

        public override Task<List<string>> GetItems(string path)
        {
            return null;
        }

        public override string GetPath()
        {
            return string.Empty;
        }
    }

    public class JPMorganSftpHandler : SftpHandler
    {
        public static string JPMorganPgPEncryptionKeyName = Environment.GetEnvironmentVariable("JPMorganPgPEncryptionKeyName");
        public static string JPMorganPgPSignatureKeyName = Environment.GetEnvironmentVariable("JPMorganPgPSignatureKeyName");
        public static string JPMorganPgPSignatureKeyPassword = Environment.GetEnvironmentVariable("JPMorganPgPSignatureKeyPassword");
        public static string JPMorganPgpEncryptionPubKeyPath => Path.Combine(D365Handler.BasePath, JPMorganPgPEncryptionKeyName);
        public static string JPMorganCounterFile = Environment.GetEnvironmentVariable("JPMorganCounterFile");
        public static string JPMorganPgpSignatureKeyPath => Path.Combine(D365Handler.BasePath, JPMorganPgPSignatureKeyName);

        public JPMorganSftpHandler(string username, string keyFile, string host, int port) : base(username, keyFile, host, port)
        {
        }

        public override string GetName(string attachmentName)
        {
            var counter = IncreaseAndGetCounter();
            var formattedNumber = counter.ToString().PadLeft(2, '0');

            var builder = new StringBuilder();
            builder.Append("SFPP3");
            if (attachmentName.Contains("EUR", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append("0X4");
            }
            else
            {
                builder.Append("0WQ");
            }

            builder.Append("0.");
            var prefix = builder.ToString();

            return prefix + DateTime.Now.ToString("yyyyMMdd") + formattedNumber + ".pgp";
        }

        public static int IncreaseAndGetCounter()
        {
            var counterFilePath = Path.Combine(D365Handler.BasePath, JPMorganCounterFile);
            string date;
            int counter;

            if (File.Exists(counterFilePath))
            {
                var fileContent = File.ReadAllText(counterFilePath);
                string[] values = fileContent.Split(',');
                date = values[0];
                counter = int.Parse(values[1]);

                if (date.Equals(DateTime.Now.ToString("yyyyMMdd")))
                {
                    counter++;
                }
                else
                {
                    date = DateTime.Now.ToString("yyyyMMdd");
                    counter = 1;
                }
            }
            else
            {
                date = DateTime.Now.ToString("yyyyMMdd");
                counter = 1;
            }

            string content = $"{date},{counter}";
            File.WriteAllText(counterFilePath, content);
            return counter;
        }

        public override string GetPath()
        {
            string path = "";
            path = Environment.GetEnvironmentVariable("JPMorganOutputFolder");
            return path;
        }

        public override async Task<byte[]> ProcessFile(byte[] bytes, string inputFileName, string outputFileName)
        {
            var pgp = await LoadJpMorganPgpEncryption();

            using (MemoryStream outputStream = new MemoryStream())
            {
                var importPath = Path.Combine(D365Handler.BasePath, inputFileName);
                var exportPath = Path.Combine(D365Handler.BasePath, outputFileName);
                await File.WriteAllBytesAsync(importPath, bytes);
                var importInfo = new FileInfo(importPath);
                var exportInfo = new FileInfo(exportPath);
                await pgp.EncryptFileAndSignAsync(importInfo, exportInfo);

                var outputBytes = await File.ReadAllBytesAsync(exportPath);

                File.Delete(importPath);
                File.Delete(exportPath);
                return outputBytes;
            }
        }

        public static async Task<PGP> LoadJpMorganPgpEncryption()
        {
            var pubKeyFile = JPMorganPgpEncryptionPubKeyPath;
            var signatureKeyFile = JPMorganPgpSignatureKeyPath;

            var pgp = new PGP(new EncryptionKeys(
                await File.ReadAllTextAsync(pubKeyFile),
                await File.ReadAllTextAsync(signatureKeyFile), JPMorganPgPSignatureKeyPassword));
            return pgp;
        }

        public static PGP Pgp { get; set; }

    }
    public class BnpSftpHandler : SftpHandler
    {
        public static string PgPEncryptionKeyName = Environment.GetEnvironmentVariable("PgPEncryptionKeyName");
        public static string PgPSignatureKeyName = Environment.GetEnvironmentVariable("PgPSignatureKeyName");
        public static string BnPPgPSignatureKeyPassword = Environment.GetEnvironmentVariable("BnPPgPSignatureKeyPassword");
        public static string PgpEncryptionPubKeyPath => Path.Combine(D365Handler.BasePath, PgPEncryptionKeyName);
        public static string BnPCounterFile = Environment.GetEnvironmentVariable("BnPCounterFile");
        public static string PgpSignatureKeyPath => Path.Combine(D365Handler.BasePath, PgPSignatureKeyName);

        public BnpSftpHandler(string username, string keyFile, string host, int port) : base(username, keyFile, host, port)
        {
        }

        public override string GetName(string attachmentName)
        {
            var counter = IncreaseAndGetCounter();
            var formattedNumber = counter.ToString().PadLeft(2, '0');

            var builder = new StringBuilder();
            builder.Append("SFPP3");
            if (attachmentName.Contains("EUR", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append("0X4");
            }
            else
            {
                builder.Append("0WQ");
            }

            builder.Append("0.");
            var prefix = builder.ToString();

            return prefix + DateTime.Now.ToString("yyyyMMdd") + formattedNumber + ".pgp";
        }

        public static int IncreaseAndGetCounter()
        {
            var counterFilePath = Path.Combine(D365Handler.BasePath, BnPCounterFile);
            string date;
            int counter;

            if (File.Exists(counterFilePath))
            {
                var fileContent = File.ReadAllText(counterFilePath);
                string[] values = fileContent.Split(',');
                date = values[0];
                counter = int.Parse(values[1]);

                if (date.Equals(DateTime.Now.ToString("yyyyMMdd")))
                {
                    counter++;
                }
                else
                {
                    date = DateTime.Now.ToString("yyyyMMdd");
                    counter = 1;
                }
            }
            else
            {
                date = DateTime.Now.ToString("yyyyMMdd");
                counter = 1;
            }

            string content = $"{date},{counter}";
            File.WriteAllText(counterFilePath, content);
            return counter;
        }

        public override string GetPath()
        {
            return "upload";
        }

        public override async Task<byte[]> ProcessFile(byte[] bytes, string inputFileName, string outputFileName)
        {
            var pgp = await LoadPgpEncryption();

            using (MemoryStream outputStream = new MemoryStream())
            {
                var importPath = Path.Combine(D365Handler.BasePath, inputFileName);
                var exportPath = Path.Combine(D365Handler.BasePath, outputFileName);
                await File.WriteAllBytesAsync(importPath, bytes);
                var importInfo = new FileInfo(importPath);
                var exportInfo = new FileInfo(exportPath);
                await pgp.EncryptFileAndSignAsync(importInfo, exportInfo);

                var outputBytes = await File.ReadAllBytesAsync(exportPath);

                File.Delete(importPath);
                File.Delete(exportPath);
                return outputBytes;
            }
        }

        public static async Task<PGP> LoadPgpEncryption()
        {
            var pubKeyFile = PgpEncryptionPubKeyPath;
            var signatureKeyFile = PgpSignatureKeyPath;

            var pgp = new PGP(new EncryptionKeys(
                await File.ReadAllTextAsync(pubKeyFile),
                await File.ReadAllTextAsync(signatureKeyFile), BnPPgPSignatureKeyPassword));
            return pgp;
        }

        public static PGP Pgp { get; set; }
    }

    public class DnbSftpHandler : SftpHandler
    {

        public DnbSftpHandler(string userName, string keyFileName, string host, int port) : base(userName, keyFileName, host, port)
        {

        }

        public override string GetName(string attachmentName)
        {
            var clientNumber = "222006228".PadLeft(11, '0');
            var divisionNumber = "3";
            var fileCode = "P001";
            var fileType = "B2CKND";

            return $"P.{clientNumber}.00{divisionNumber}.{fileCode}.{fileType}.10";
        }

        public override string GetPath()
        {
            return "Send";
        }

        public override Task<byte[]> ProcessFile(byte[] bytes, string inputFileName, string outputFileName)
        {
            return Task.FromResult(bytes);
        }
    }

    public abstract class SftpHandler : FileHandler
    {
        public static SftpHandler GetForBnPTest()
        {
            return new BnpSftpHandler(Environment.GetEnvironmentVariable("BnpTestUsername"), Environment.GetEnvironmentVariable("BnpTestKeyFile"),
                Environment.GetEnvironmentVariable("BnpTestHost"), Int32.Parse(Environment.GetEnvironmentVariable("BnpPort")));
        }

        public static SftpHandler GetForBnPProd()
        {
            return new BnpSftpHandler(Environment.GetEnvironmentVariable("BnpProdUsername"), Environment.GetEnvironmentVariable("BnpProdKeyFile"),
                Environment.GetEnvironmentVariable("BnpProdHost"), Int32.Parse(Environment.GetEnvironmentVariable("BnpPort")));
        }

        public static SftpHandler GetForDnB()
        {
            return new DnbSftpHandler(Environment.GetEnvironmentVariable("DnbUsername"), Environment.GetEnvironmentVariable("DnbKeyFile"),
                Environment.GetEnvironmentVariable("fgw.dnb.no"), Int32.Parse(Environment.GetEnvironmentVariable("DnbPort")));
        }

        public static SftpHandler GetForJPMorganTest()
        {
            return new JPMorganSftpHandler(Environment.GetEnvironmentVariable("JPMorganTestUsername"), Environment.GetEnvironmentVariable("JPMorganTestKeyFile"),
                Environment.GetEnvironmentVariable("JPMorganTestHost"), Int32.Parse(Environment.GetEnvironmentVariable("JPMorganPort")));
        }
        public static SftpHandler GetForJPMorganProd()
        {
            return new JPMorganSftpHandler(Environment.GetEnvironmentVariable("JPMorganProdUsername"), Environment.GetEnvironmentVariable("JPMorganProdKeyFile"),
                Environment.GetEnvironmentVariable("JPMorganProdHost"), Int32.Parse(Environment.GetEnvironmentVariable("JPMorganPort")));
        }
        public string SftpHost { get; }
        public string SftpUser { get; }
        public string KeyFileResourceName { get; }
        public int SftpPort { get; }

        public SftpHandler(string userName, string keyFileName, string host, int port)
        {
            SftpHost = host;
            SftpUser = userName;
            SftpPort = port;
            KeyFileResourceName = keyFileName;
        }

        public SftpClient Client { get; set; }

        public override void Connect()
        {
            Client = GetConnectedSftpClient();
        }

        public override void Dispose()
        {
            try
            {
                Client.Disconnect();

            }
            catch (Exception)
            {
                // Ignored
            }

            try
            {
                Client.Dispose();
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        public override Task UploadFile(MemoryStream memStream, string name, bool overwrite)
        {
            Client.UploadFile(memStream, name, overwrite);
            return Task.CompletedTask;
        }

        public override Task<List<string>> GetItems(string path)
        {
            return Task.FromResult(Client.ListDirectory(path).Select(c => c.FullName).ToList());
        }

        private SftpClient GetConnectedSftpClient()
        {
            var privateKeyFile = this.GetType().Assembly
                .GetManifestResourceStream(KeyFileResourceName);
            var sftpClient = new SftpClient(SftpHost, SftpPort, SftpUser,
                new PrivateKeyFile(privateKeyFile));
            sftpClient.Connect();
            return sftpClient;
        }
    }
}
