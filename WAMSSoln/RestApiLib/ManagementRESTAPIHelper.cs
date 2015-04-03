using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using System.Text;


namespace RestApiLib
{
    public class ManagementRESTAPIHelper
    {
        public string Endpoint { get; set; }
        public string CertThumbprint { get; set; }
        public string SubscriptionId { get; set; }

        public ManagementRESTAPIHelper(string endpoint, string thumbprint, string subscriptionID)
        {
            Endpoint = endpoint;
            CertThumbprint = thumbprint;
            SubscriptionId = subscriptionID;
        }

        private X509Certificate2 GetClientCertificate()
        {
            List<StoreLocation> locations = new List<StoreLocation> 
    { 
        StoreLocation.CurrentUser, 
        StoreLocation.LocalMachine 
    };

            foreach (var location in locations)
            {
                X509Store store = new X509Store("My", location);
                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    X509Certificate2Collection certificates = store.Certificates.Find(
                        X509FindType.FindByThumbprint, CertThumbprint, false);
                    if (certificates.Count == 1)
                    {
                        return certificates[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }

            throw new ArgumentException(string.Format(
                "A Certificate with thumbprint '{0}' could not be located.",
                CertThumbprint));
        }

        public string CreateMediaServiceAccountUsingXmlContentType(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts",
                Endpoint, SubscriptionId));


            // Create the request XML document
            XDocument requestBody = new XDocument(
                new XDeclaration("1.0", "UTF-8", "no"),
                new XElement(
                    "AccountCreationRequest",
                    new XElement("AccountName", accountInfo.MediaServicesAccountName),
                    new XElement("StorageAccountKey", accountInfo.StorageAccountKey),
                    new XElement("StorageAccountName", accountInfo.StorageAccountName),
                    new XElement("BlobStorageEndpointUri", accountInfo.BlobStorageEndpoint),
                    new XElement("Region", accountInfo.Region)));

            XDocument responseBody;
            responseBody = null;
            string requestId = String.Empty;

            request.Method = "POST";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.ClientCertificates.Add(clientCert);
            request.ContentType = "application/xml";

            if (requestBody != null)
            {
                using (Stream requestStream = request.GetRequestStream())
                {
                    using (StreamWriter streamWriter = new StreamWriter(
                        requestStream, System.Text.UTF8Encoding.UTF8))
                    {
                        requestBody.Save(streamWriter, SaveOptions.DisableFormatting);
                    }
                }
            }

            HttpWebResponse response;
            HttpStatusCode statusCode = HttpStatusCode.Unused;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                // GetResponse throws a WebException for 4XX and 5XX status codes
                response = (HttpWebResponse)ex.Response;
            }

            try
            {
                statusCode = response.StatusCode;
                if (response.ContentLength > 0)
                {
                    using (XmlReader reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        responseBody = XDocument.Load(reader);
                        Console.WriteLine(responseBody.ToString());

                    }
                }

                if (response.Headers != null)
                {
                    requestId = response.Headers["x-ms-request-id"];
                }
            }
            finally
            {
                response.Close();
            }

            if (!statusCode.Equals(HttpStatusCode.Created))
            {
                throw new ApplicationException(string.Format(
                    "Call to CreateAccount returned an error. Status Code: {0}",
                    statusCode
                    ));
            }

            return requestId;
        }

        public void AttachStorageAccountToMediaServiceAccount(MediaServicesAccountInfo accountInfo, AttachStorageAccountRequest storageaccount)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}/StorageAccounts",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Headers.Add("Accept-Encoding: gzip, deflate");
            request.ClientCertificates.Add(clientCert);

            string jsonString;

            using (MemoryStream ms = new MemoryStream())
            {
                DataContractJsonSerializer serializer
                        = new DataContractJsonSerializer(typeof(AttachStorageAccountRequest));

                serializer.WriteObject(ms, storageaccount);

                jsonString = Encoding.Default.GetString(ms.ToArray());

            }

            using (Stream requestStream = request.GetRequestStream())
            {
                var requestBytes = System.Text.Encoding.ASCII.GetBytes(jsonString);
                requestStream.Write(requestBytes, 0, requestBytes.Length);
                requestStream.Close();
            }


            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                    Console.WriteLine("The primary key was regenerated.");
            }
        }

        public void DeleteAccount(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "DELETE";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.ClientCertificates.Add(clientCert);

            Console.WriteLine(string.Format("Try to delete account \"{0}\".", accountInfo.MediaServicesAccountName));
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                // "using" used just to dispose the response received.
            }

            try
            {
                AccountDetails detail = GetAccountDetails(accountInfo);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("(404) Not Found"))
                {
                    Console.WriteLine("Account[{0}] has been deleted successfully", accountInfo.MediaServicesAccountName);
                }
                else
                {
                    Console.WriteLine("Deleted Account[{0}] Failed", accountInfo.MediaServicesAccountName);
                }
            }
        }

        public List<SupportedRegion> ListAvailableRegions(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/SupportedRegions",
                Endpoint, SubscriptionId));

            request.Method = "GET";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.ClientCertificates.Add(clientCert);

            List<SupportedRegion> regions;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var stream1 = response.GetResponseStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<SupportedRegion>));
                regions = (List<SupportedRegion>)ser.ReadObject(stream1);

                foreach (var r in regions)
                {
                    Console.WriteLine(r.RegionName);
                }
            }

            return regions;
        }

        public AccountDetails GetAccountDetails(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "GET";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.ClientCertificates.Add(clientCert);

            AccountDetails accountDetails = null;

            Console.WriteLine(string.Format("Try to get the details of accountName[{0}].", accountInfo.MediaServicesAccountName));
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var stream1 = response.GetResponseStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AccountDetails));
                accountDetails = (AccountDetails)ser.ReadObject(stream1);
                Console.WriteLine("Deserialized back:");
                Console.WriteLine(accountDetails.AccountName);
                Console.WriteLine(accountDetails.StorageAccountName);
            }

            Console.WriteLine("Got account detail successfully");
            return accountDetails;
        }

        public void SynchronizeStorageAccountKey(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}/StorageAccounts/{3}/Key",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName, accountInfo.StorageAccountName));
            request.Method = "PUT";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Headers.Add("Accept-Encoding: gzip, deflate");
            request.ClientCertificates.Add(clientCert);


            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write("\"");
                streamWriter.Write(accountInfo.StorageAccountKey);
                streamWriter.Write("\"");
                streamWriter.Flush();
            }

            AccountDetails details = GetAccountDetails(accountInfo);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                string jsonResponse;
                Stream receiveStream = response.GetResponseStream();
                Encoding encode = Encoding.GetEncoding("utf-8");
                if (receiveStream != null)
                {
                    var readStream = new StreamReader(receiveStream, encode);
                    jsonResponse = readStream.ReadToEnd();
                }
            }
        }

        public List<StorageAccountDetails> ListStorageAccountDetails(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}/StorageAccounts",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "GET";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.ClientCertificates.Add(clientCert);

            List<StorageAccountDetails> storageAccounts;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var stream1 = response.GetResponseStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<StorageAccountDetails>));
                storageAccounts = (List<StorageAccountDetails>)ser.ReadObject(stream1);

                foreach (var r in storageAccounts)
                {
                    Console.WriteLine("Account name: {0}", r.StorageAccountName);
                    Console.WriteLine("IsDefault: {0}", r.IsDefault);
                }
            }

            return storageAccounts;
        }


        public List<AzureMediaServicesResource> ListSubscriptionAccounts(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts",
                Endpoint, SubscriptionId));
            request.Method = "GET";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.ClientCertificates.Add(clientCert);

            List<AzureMediaServicesResource> accounts;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var stream1 = response.GetResponseStream();
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<AzureMediaServicesResource>));
                accounts = (List<AzureMediaServicesResource>)ser.ReadObject(stream1);

                foreach (var r in accounts)
                {
                    Console.WriteLine("Name: {0}", r.Name);
                }
            }

            return accounts;
        }


        public void RegeneratePrimaryAccountKey(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}/AccountKeys/Primary/Regenerate",
                    Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "Post";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Headers.Add("Accept-Encoding: gzip, deflate");
            request.Accept = "application/json";
            request.ClientCertificates.Add(clientCert);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write("\"");
                streamWriter.Write(accountInfo.MediaServicesAccountName);
                streamWriter.Write("\"");
                streamWriter.Flush();
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                    Console.WriteLine("The primary key was regenerated.");
            }
        }

        public void RegenerateSecondaryAccountKey(MediaServicesAccountInfo accountInfo)
        {
            var clientCert = GetClientCertificate();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}/{1}/services/mediaservices/Accounts/{2}/AccountKeys/Secondary/Regenerate",
                Endpoint, SubscriptionId, accountInfo.MediaServicesAccountName));
            request.Method = "Post";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("x-ms-version", "2011-10-01");
            request.Headers.Add("Accept-Encoding: gzip, deflate");
            request.Accept = "application/json";
            request.ClientCertificates.Add(clientCert);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write("\"");
                streamWriter.Write(accountInfo.MediaServicesAccountName);
                streamWriter.Write("\"");
                streamWriter.Flush();
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                    Console.WriteLine("The secondary key was regenerated.");
            }
        }

    }

    #region SerializationClasses

    public class AccountCreationResult
    {
        public Guid AccountId { get; set; }
        public string AccountName { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public string Subscription { get; set; }
    }

    public class AccountCreationRequest
    {
        public string AccountName { get; set; }
        public string BlobStorageEndpointUri { get; set; }
        public string Region { get; set; }
        public string StorageAccountKey { get; set; }
        public string StorageAccountName { get; set; }
    }

    public class AttachStorageAccountRequest
    {
        public string StorageAccountName { get; set; }
        public string StorageAccountKey { get; set; }
        public string BlobStorageEndpointUri { get; set; }
    }

    public class AzureMediaServicesResource
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
        public Uri SelfLink { get; set; }
        public Uri ParentLink { get; set; }
        public Guid AccountId { get; set; }
    }

    public class SupportedRegion
    {
        public string RegionName { get; set; }
    }

    public class StorageAccountDetails
    {
        public string StorageAccountName { get; set; }
        public string BlobStorageEndPoint { get; set; }
        public bool IsDefault { get; set; }
    }

    public class AccountDetails
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        public AccountKeys AccountKeys { get; set; }
        public string StorageAccountName { get; set; }
        public string AccountRegion { get; set; }
    }

    public class AccountKeys
    {
        public string Primary { get; set; }
        public string Secondary { get; set; }
    }

    public class MediaServicesAccountInfo
    {
        public string MediaServicesAccountName { get; set; }
        public string Region { get; set; }
        public string StorageAccountName { get; set; }
        public string StorageAccountKey { get; set; }
        public string BlobStorageEndpoint { get; set; }
    }

    #endregion


}
