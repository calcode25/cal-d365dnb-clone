using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Calliditas.Integration.D365Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace Calliditas.Integration.D365Api;

public class D365DnBTimer
{
    public D365DnBTimer()
    {
        EmailAddress = Environment.GetEnvironmentVariable("APPSETTING_D365AutomationAddress");
        var receiptMail = Environment.GetEnvironmentVariable("APPSETTING_D365ReceiptAddress");

        if (string.IsNullOrWhiteSpace(receiptMail))
        {
            ReceiptMail = "finance@calliditas.com";
        }
        else
        {
            ReceiptMail = receiptMail;
        }

        TimerActive = Environment.GetEnvironmentVariable("APPSETTING_TimerActive")?.Equals("1") ?? false;
    }

    public bool TimerActive { get; }

    public string EmailAddress { get; set; }

    public string ReceiptMail { get; set; }

    [FunctionName("GetMessages")]
    public async Task<JsonResult> GetMessages(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetMessages")]
        HttpRequest req, ILogger log)
    {
        var i = 0;
        try
        {
            var list = new List<Message>();
            var graphClient = await D365Handler.GetMessages(list);
            var encryption = await BnpSftpHandler.LoadPgpEncryption();
            var keyCount = encryption.EncryptionKeys.EncryptKeys.Count();
            ++i;
            var client = SftpHandler.GetForBnPProd();
            client.Connect();
            ++i;
            var items = await client.GetItems("/upload/");
            ++i;
            return new JsonResult(new { Files = items, List = list.Select(c => new
            {
                Folder = items, KeyCount = keyCount, Subject = c.Subject,
                A = c.Attachments.OfType<FileAttachment>()
                    .Select(a => new { a.Name, Bytes = Convert.ToBase64String(a.ContentBytes) }).ToList()
            })});

        }
        catch (Exception ex)
        {
            return new JsonResult(new{ i, ex});
        }
    }

    [FunctionName("CheckVariables")]
    public JsonResult CheckVariables(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "CheckVariables")]
        HttpRequest req, ILogger log)
    {
        var value = new
        {
            Counter = BnpSftpHandler.IncreaseAndGetCounter(),
            EmailAddress, ReceiptMail, TimerActive,
            BnPPublicKeyExists = System.IO.File.Exists(BnpSftpHandler.PgpEncryptionPubKeyPath),
            BnPPrivateKeyExists = System.IO.File.Exists(BnpSftpHandler.PgpSignatureKeyPath),
            BnpEncryptionKeyPath = BnpSftpHandler.PgpEncryptionPubKeyPath,
            BnpSignatureKeyPath = BnpSftpHandler.PgpSignatureKeyPath
        };

        return new JsonResult(value);
    }

    [FunctionName("D365DnBTimer")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
    {
        if (TimerActive)
        {
            var p = new D365Handler();
            p.ReceiptAddress = ReceiptMail;

            await p.HandleD365Mail();
            await p.HandleDnbToD365Mail();
        }
    }

    [FunctionName("D365DnBHttp")]
    public async Task<IActionResult> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "CallD365")]
        HttpRequest req,
        ILogger log)
    {
        var handler = new D365Handler();
        handler.ReceiptAddress = ReceiptMail;

        await handler.HandleD365Mail();

        return new JsonResult(new {Result = "OK", Success = handler.Success, Errors = handler.Errors});
    }

    [FunctionName("CheckIP")]
    public async Task<string> RunHttpIP(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "CheckIP")]
        HttpRequest req,
        ILogger log)
    {
        var client = new HttpClient();
        var resp = await client.GetAsync(Environment.GetEnvironmentVariable("APPSETTING_CheckIpApiPath"));
        var str = await resp.Content.ReadAsStringAsync();
        return str;
    }

    [FunctionName("GetSftpList")]
    public async Task<List<string>> RunHttpCheckSftpList(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "CheckSftpList")]
        HttpRequest req,
        ILogger log)
    {
        try
        {
            var client = SftpHandler.GetForBnPProd();
            client.Connect();
            var paths = await client.GetItems("/");
            var paths2 = await client.GetItems("/upload");
            return paths.Concat(paths2).ToList();

        }
        catch (Exception ex)
        {
            return new List<string>() { ex.ToString() };
        }
    }
}