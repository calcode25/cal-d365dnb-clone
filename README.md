1. APPSETTING\_D365AutomationAddress - Automation email address for D365, denna misstänker jag ligger i Dynamics365
automation365@calliditas.com
2. APPSETTING\_D365ReceiptAddress - Receipt email address for D365 – finance mejlen
finance@calliditas.com
3. APPSETTING\_TimerActive - Flag to activate/deactivate the timer (expects "1" for true) 1
1
4. APPSETTING\_CheckIpApiPath - API path for checking IP address
https://api.ipify.org/?format=json
5. ResponseEmailAddress - Email address for responses
finance@calliditas.com
6. BnPReceiptAddress - Receipt address for BnP (BNP Paribas)
finance.fr@calliditas.com
7. CalliditasDynamics365Address - Calliditas Dynamics 365 email address
calliditas.dynamics365@calliditas.com
8. GraphClientSignature - Signature for Graph client authentication
AZRcN8FjU5V/pFcsl7GEIFJJ12dmMQGZWj2Wh6qcdDflzXtbSfr7ignwyoynqeXxQngZWH3GfYbiXWNJBjS5V3rb2ri7ygxTcf8ylxdxgxsRaBcoojzi1rFAfYgZM4rGBEESvaEDCwyJNday2zDEltd4KDKjl8cyQa8N60OUOrE9WGBI99HPXSburs/14Q280bM2xZdDPs/pQAQF7ki5qvyOGRMrMjqX3bjNHNMZ9DdneO8tK3u6CkTOnvkMST4sY19s/MIlHIXq2POZCoVfA3zHjYC3v+yhFODYlVti/gYhVj3BsIJA7Duo8CLKvNq9hDkdQSiw+pt3VajbAieCGw==
9. AppServiceTokenBrokerUrl - URL for the App Service token broker
https://adp-graphaccess-euwe-app-prod.azurewebsites.net/api/Tokens/getBySignature?signature=
10. PgPEncryptionKeyName - Name of the PGP encryption key file
public\_key\_bnp\_PROD.asc
11. PgPSignatureKeyName - Name of the PGP signature key file
pgp-prod-priv.asc
12. BnPPgPSignatureKeyPassword - Password for BnP PGP signature key
C41L!d1745s
13. BnPCounterFile - Counter file name for BnP
Counter.txt
14. BnpTestUsername - BnP test SFTP username
83SAKW
15. BnpTestKeyFile - BnP test SFTP key file
Calliditas.Integration.D365Functions.SftpKeyBnP.ssh
16. BnpTestHost - BnP test SFTP host
cm-sftp-test.bnpparibas.com
17. BnpPort - BnP SFTP port number
10022
18. BnpProdUsername - BnP production SFTP username
83SGES
19. BnpProdKeyFile - BnP production SFTP key file
Calliditas.Integration.D365Functions.SftpKeyBnP.ssh
20. BnpProdHost - BnP production SFTP host
cm-sftp.bnpparibas.com
21. AzureFileShareBaseUrl - Base URL for Azure File Share
https://cald365dnbeuwesaprod.file.core.windows.net
22. StorageSharedKeyCredentialAccountName - Azure Storage account name
cald365dnbeuwesaprod
23. StorageSharedKeyCredentialAccountKey - Azure Storage account key
2x2bCNm0OJLwZspCl6PXr+5V+RYdfqJoziWSIbP8AKn5AIxlT342CBTA8w0nKKxOVnYuy7G+qaBnm4OR+LyhZg==
24. JPMorganReceiptAddress - Receipt address for JP Morgan
25. JPMorganTestUsername - JP Morgan test SFTP username
26. JPMorganTestKeyFile - JP Morgan test SFTP key file
27. JPMorganTestHost - JP Morgan test SFTP host
28. JPMorganPort - JP Morgan SFTP port number
29. JPMorganProdUsername - JP Morgan prod SFTP username
30. JPMorganProdKeyFile - JP Morgan prod SFTP key file
31. JPMorganProdHost - JP Morgan prod SFTP host
32. JPMorganOutputFolder - Jp Morgan output folder

