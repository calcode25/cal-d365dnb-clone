using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Newtonsoft.Json;

namespace Calliditas.Integration.D365Functions
{
    public class D365Handler
    {
        public string? ReceiptAddress { get; set; }

        public static string BasePath { get; set; }

        static D365Handler()
        {
            BasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        }

        

        public D365Handler()
        {
            Errors = new List<Exception>();
            Success = new List<string>();
            ReceiptAddress = Environment.GetEnvironmentVariable("ResponseEmailAddress");
        }

        public async Task HandleD365Mail()
        {
            if (string.IsNullOrWhiteSpace(ReceiptAddress))
            {
                ReceiptAddress = "finance@calliditas.com";
            }

            var list = new List<Message>();
            var graph = await GetMessages(list);

            foreach (var message in list)
            {
                string receiptAddress;
                string receiptName;
                var matchPart = Regex.Match(message.Subject, "^.+?_").Value.Trim().ToLower();

                Func<FileHandler> getSender;
                bool skip;
                switch (matchPart)
                {
                    case "bcge_":
                        getSender = () => new AzureFileShareHandler("bcge");
                        skip = true;
                        receiptAddress = ReceiptAddress;
                        receiptName = "bcge";
                        break;
                    case "bnp_":
                        skip = false;
                        getSender = SftpHandler.GetForBnPProd;
                        receiptAddress = Environment.GetEnvironmentVariable("BnPReceiptAddress");
                        receiptName = "BnP";
                        break;
                    case "jp_":
                        skip = false;
                        getSender = SftpHandler.GetForJPMorganProd;
                        receiptAddress = Environment.GetEnvironmentVariable("JPMorganReceiptAddress");
                        receiptName = "JPMorgan";
                        break;
                    default:
                        skip = false;
                        getSender = SftpHandler.GetForDnB;
                        receiptAddress = ReceiptAddress;
                        receiptName = "DnB";
                        break;
                }

                if (skip)
                {
                    continue;
                }

                using (var client = getSender())
                {
                    try
                    {
                        var sender = message.From?.EmailAddress?.Address?.ToLower() ?? string.Empty;
                        if (sender.Equals(Environment.GetEnvironmentVariable("CalliditasDynamics365Address")) == false && sender.Contains("@itm8.com") == false)
                        {
                            throw new ArgumentException($"Mail från felaktig avsändare: {sender}");
                        }

                        var files = new List<Tuple<string, string, byte[]>>();

                        foreach (var attachment in message.Attachments.OfType<FileAttachment>())
                        {
                            var contentBytes = attachment.ContentBytes;
                            var extension = Path.GetExtension(attachment.Name)?.ToLower() ?? string.Empty;
                            if (extension.EndsWith("zip"))
                            {
                                var zipStream = new MemoryStream(contentBytes);
                                var file = new ZipArchive(zipStream);
                                var entries = file.Entries;
                                foreach (var entry in entries)
                                {
                                    byte[] bytes;
                                    await using (var stream = entry.Open())
                                    {
                                        var l = entry.Length;
                                        bytes = new byte[l];
                                        _ = stream.Read(bytes);
                                    }

                                    files.Add(new Tuple<string, string, byte[]>(entry.Name, attachment.Name, bytes));
                                }
                            }
                            else if (extension.EndsWith("xml") || extension.EndsWith("xct"))
                            {
                                files.Add(new Tuple<string, string, byte[]>(attachment.Name, attachment.Name, contentBytes));
                            }
                        }

                        var id = "okänd";
                        string sum = string.Empty;
                        client.Connect();
                        if (files.Any())
                        {
                            foreach (var file in files)
                            {
                                var extension = Path.GetExtension(file.Item1)?.ToLower() ?? string.Empty;
                                if (extension.EndsWith("xml") || extension.EndsWith("xct"))
                                {
                                    var memStream = new MemoryStream(file.Item3);
                                    memStream.Position = 0;

                                    try
                                    {
                                        var xmlReader = XmlReader.Create(memStream,
                                            new XmlReaderSettings() { Async = true });
                                        xmlReader.ReadToFollowing("MsgId");
                                        id = await xmlReader.ReadElementContentAsStringAsync();
                                        xmlReader.ReadToFollowing("CtrlSum");
                                        sum = await xmlReader.ReadElementContentAsStringAsync();
                                    }
                                    catch
                                    {
                                        // Ignore
                                    }

                                    memStream.Position = 0;

                                    string fileName = "";
                                    if (matchPart=="jp_")
                                    {
                                        fileName = client.GetName(file.Item1);
                                    }
                                    else
                                    {
                                        fileName = client.GetName(file.Item2);
                                    }
                                    var bytes = await client.ProcessFile(memStream.ToArray(), file.Item1, fileName);
                                    var uploadStream = new MemoryStream(bytes);

                                    string path = client.GetPath();
                                    var fullPath = Path.Combine(path, fileName).Replace("\\", "/").TrimStart('/');
                                    await client.UploadFile(uploadStream, $"/{fullPath}", true);
                                    Success.Add(fullPath);
                                }
                            }
                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException("Inga filer finns att skicka i mailet");
                        }

                        var subject = $"Receipt of transfer - {id} - {sum} - {DateTime.Now} to {receiptName}";
                        var receiptMessage = new Message()
                        {
                            ToRecipients = new[]
                            {
                                new Recipient()
                                    { EmailAddress = new EmailAddress() { Address = receiptAddress } },
                            }.ToList(),
                            Body = new ItemBody()
                            {
                                Content =
                                    $"Transfer to {receiptName} for {message.Subject}, sent {message.SentDateTime:yyyy-MM-dd HH:mm:ss}.\r\nTransfer to {receiptName} completed {DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}."
                            },
                            Subject = subject
                        };

                        await graph.Me.SendMail.PostAsync(new SendMailPostRequestBody() { Message = receiptMessage, SaveToSentItems = false });
                    }
                    catch (Exception ex)
                    {
                        Errors.Add(ex);
                        try
                        {
                            await graph.Me.Messages[message.Id].DeleteAsync();
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }

                        var receiptMessage = new Message()
                        {
                            ToRecipients = new[]
                            {
                                new Recipient()
                                    { EmailAddress = new EmailAddress() { Address = receiptAddress } },
                            }.ToList(),
                            Body = new ItemBody()
                            {
                                Content =
                                    $"Transfer to {receiptName} for {message.Subject}, sent {message.SentDateTime:yyyy-MM-dd HH:mm:ss}.\r\nTransfer to {receiptName} failed {DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}.\r\n\r\nFile needs to be handled manually\r\n\r\n{ex.Message}\r\n{ex.StackTrace}\r\n{ex}"
                            },
                            Attachments = new List<Attachment>(),
                            Subject = $"Failure of transfer to {receiptName}"
                        };

                        foreach (var attachment in message.Attachments)
                        {
                            receiptMessage.Attachments.Add(attachment);
                        }

                        await graph.Me.SendMail.PostAsync(new SendMailPostRequestBody() { Message = receiptMessage, SaveToSentItems = false });
                    }
                    finally
                    {
                        try
                        {
                            await graph.Me.Messages[message.Id].DeleteAsync();
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }
                    }
                }
            }
        }

        public List<Exception> Errors { get; }

        public List<string> Success { get; set; }

        public static async Task<GraphServiceClient> GetMessages(List<Message> list)
        {
            var graph = await GetGraphClient();
            list.AddRange((await graph.Me.MailFolders["Inbox"].Messages
                    .GetAsync(o => o.QueryParameters.Expand = new[] { "Attachments" }))?.Value?
                .ToList());
            return graph;
        }

        private static async Task<GraphServiceClient> GetGraphClient()
        {
            var sig = Environment.GetEnvironmentVariable("GraphClientSignature");
            var appServiceTokenBrokerUrl = Environment.GetEnvironmentVariable("AppServiceTokenBrokerUrl");

            if (string.IsNullOrEmpty(sig))
            {
                throw new ArgumentNullException("Appsetting GraphClientSignature is missing");
            }
            if (string.IsNullOrEmpty(appServiceTokenBrokerUrl))
            {
                throw new ArgumentNullException("Appsetting AppServiceTokenBrokerUrl is missing");
            }

            var client = new HttpClient();

            var authCodeResp = await client.GetAsync(appServiceTokenBrokerUrl + Uri.EscapeDataString(sig));
            var authCode = await authCodeResp.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<GraphToken>(authCode);

            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + obj.access_token);
            var graph = new GraphServiceClient(client);
            return graph;
        }

        public async Task HandleDnbToD365Mail()
        {
            var graph = await GetGraphClient();
            using (var sftp = SftpHandler.GetForDnB())
            {
                sftp.Connect();
                var files = sftp.Client.ListDirectory("/Inbox").ToList();
                foreach (var sFile in files)
                {
                    string subject;

                    var sftpBytes = new byte[100000];
                    int read;
                    await using (var fs = sftp.Client.OpenRead(sFile.FullName))
                    {
                        read = fs.Read(sftpBytes, 0, sftpBytes.Length);
                    }

                    if (sFile.Name.Contains(".P002."))
                    {
                        var mem = new MemoryStream(sftpBytes.Take(read).ToArray(), 0, read);
                        mem.Seek(0, SeekOrigin.Begin);
                        var receipt = "Okänd";
                        using (var xmlReader = XmlReader.Create(mem, new XmlReaderSettings() { Async = true }))
                        {
                            while (await xmlReader.ReadAsync())
                            {
                                if (xmlReader.IsStartElement() && xmlReader.Name.Equals("OrgnlMsgId"))
                                {
                                    receipt = await xmlReader.ReadElementContentAsStringAsync();
                                }
                            }
                        }

                        subject = $"Receipt received from DnB for {receipt}";
                    }
                    else
                    {
                        subject = "Miscellaneous file from DnB";
                    }

                    var receiptMessage = new Message()
                    {
                        ToRecipients = new[]
                        {
                            new Recipient()
                                {EmailAddress = new EmailAddress() {Address = ReceiptAddress}},
                        }.ToList(),
                        Body = new ItemBody()
                        {
                            Content =
                                string.Empty
                        },
                        Attachments = new List<Attachment>(),
                        Subject = subject
                    };

                    var attachment = new FileAttachment();
                    attachment.ContentType = "application/xml";
                    attachment.ContentBytes = sftpBytes;
                    attachment.Name = sFile.Name;
                    receiptMessage.Attachments.Add(attachment);

                    await graph.Me.SendMail.PostAsync(new SendMailPostRequestBody() { Message = receiptMessage, SaveToSentItems = false });

                }

                foreach (var sFile in files)
                {
                    sftp.Client.Delete(sFile.FullName);
                }
            }
        }
    }
}