using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace PrototypeConsole
{
    public class AzureStorageUtils
    {
        private static CloudBlobClient GetCloudBlobClient()
        {
            string storageName = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountName"];
            string storageKey = System.Configuration.ConfigurationManager.AppSettings["WamsStorageAccountKey"];
            string connectionString = string.Format("DefaultEndpointsProtocol={0};AccountName={1};AccountKey={2}", "https", storageName, storageKey);

            CloudStorageAccount objCloudStorageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient objCloudBlobClient = objCloudStorageAccount.CreateCloudBlobClient();
            return objCloudBlobClient;
        }

        public static CloudBlockBlob CreateContainerUploadFile(string containerName, string blobName)
        {
            CloudBlobClient objCloudBlobClient = GetCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(containerName);
            objCloudBlobContainer.CreateIfNotExists();

            CloudBlockBlob objCloudBlockBlob = objCloudBlobContainer.GetBlockBlobReference(blobName);

            //Upload text to the blob. If the blob does not yet exist, it will be created. If the blob does exist, its existing content will be overwritten.
            string blobContent = "This blob will be accessible to clients via a SAS URI.";
            MemoryStream objMemoryStream = new MemoryStream(Encoding.UTF8.GetBytes(blobContent));
            objMemoryStream.Position = 0;
            using (objMemoryStream)
            {
                objCloudBlockBlob.UploadFromStream(objMemoryStream);
            }

            return objCloudBlockBlob;
        }

        public static string GetBlobSasUri(string containerName, string blobName)
        {
            CloudBlobClient objCloudBlobClient = GetCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(containerName);
            //objCloudBlobContainer.CreateIfNotExists();

            CloudBlockBlob objCloudBlockBlob = objCloudBlobContainer.GetBlockBlobReference(blobName);

            //all of the 3 properties are required when creating a SharedAccessBlobPolicy. Otherwise error occurred.
            SharedAccessBlobPolicy objShareAccessBlobPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessStartTime  = DateTime.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(4),
                Permissions            = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write
            };

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = objCloudBlockBlob.GetSharedAccessSignature(objShareAccessBlobPolicy);

            //Return the URI string for the container, including the SAS token.
            return objCloudBlockBlob.Uri + sasBlobToken;
        }

        public static string GetContainerSasUri(string containerName)
        {
            CloudBlobClient objCloudBlobClient = GetCloudBlobClient();
            CloudBlobContainer objCloudBlobContainer = objCloudBlobClient.GetContainerReference(containerName);

            SharedAccessBlobPolicy objShareAccessBlobPolicy = new SharedAccessBlobPolicy()
            {
                //SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),  //unless you are explicitly trying to set a start time to a time in the future, you should not set the SharedAccessStartTime property otherwise you run the risk of clock skew causing authentication failures
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(4),
                Permissions = SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Delete | SharedAccessBlobPermissions.Read
            };

            string sasContainerToken = objCloudBlobContainer.GetSharedAccessSignature(objShareAccessBlobPolicy);

            //Return the URI string for the container, including the SAS token.
            return objCloudBlobContainer.Uri + sasContainerToken;
        }

        public static void UseContainerSAS(string sas)
        {
            //Try performing container operations with the SAS provided.

            //Return a reference to the container using the SAS URI.
            CloudBlobContainer container = new CloudBlobContainer(new Uri(sas));

            //Create a list to store blob URIs returned by a listing operation on the container.
            List<Uri> blobUris = new List<Uri>();

            try
            {
                //Write operation: write a new blob to the container. 
                CloudBlockBlob blob = container.GetBlockBlobReference("blobCreatedViaSAS.txt");
                string blobContent = "This blob was created with a shared access signature granting write permissions to the container. ";
                MemoryStream msWrite = new MemoryStream(Encoding.UTF8.GetBytes(blobContent));
                msWrite.Position = 0;
                using (msWrite)
                {
                    blob.UploadFromStream(msWrite);
                }
                Console.WriteLine("Write operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Write operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }

            try
            {
                //List operation: List the blobs in the container, including the one just added.
                foreach (ICloudBlob blobListing in container.ListBlobs())
                {
                    blobUris.Add(blobListing.Uri);
                }
                Console.WriteLine("List operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("List operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }

            try
            {
                //Read operation: Get a reference to one of the blobs in the container and read it. 
                CloudBlockBlob blob = container.GetBlockBlobReference(blobUris[0].ToString());
                MemoryStream msRead = new MemoryStream();
                msRead.Position = 0;
                using (msRead)
                {
                    blob.DownloadToStream(msRead);
                    Console.WriteLine(msRead.Length);
                }
                Console.WriteLine("Read operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Read operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }
            Console.WriteLine();

            try
            {
                //Delete operation: Delete a blob in the container.
                CloudBlockBlob blob = container.GetBlockBlobReference(blobUris[0].ToString());
                blob.Delete();
                Console.WriteLine("Delete operation succeeded for SAS " + sas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {
                Console.WriteLine("Delete operation failed for SAS " + sas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();
            }
        }
    }  //class
}   //namespace
